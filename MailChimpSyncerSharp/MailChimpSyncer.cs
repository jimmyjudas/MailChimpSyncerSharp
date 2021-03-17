using MailChimp.Net;
using MailChimp.Net.Interfaces;
using MailChimp.Net.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MailChimpSyncerSharp
{
    public class MailChimpSyncer
    {
        private IMailChimpManager _mailChimpManager;
        private Action<string, bool> _loggerAction;
        
        private readonly (string FieldName, Func<Contact, string> ContactValue)[] _mergeFieldsToSync = new[]
        {
            ("FNAME", new Func<Contact, string>(c => c.FirstName)),
            ("LNAME", new Func<Contact, string>(c => c.LastName))
        };

        /// <param name="apiKey">The API key for your MailChimp account, which can be generated through MailChimp (Account > Extras > API Keys)</param>
        /// <param name="loggerAction">An action that accepts an output message and a boolean determining whether or not it's an error</param>
        public MailChimpSyncer(string apiKey, Action<string, bool> loggerAction)
        {
            _mailChimpManager = new MailChimpManager(apiKey);
            _loggerAction = loggerAction;
        }

        /// <summary>
        /// Updates your MailChimp list's contacts so the only ones with a particular tag are the ones provided by you.<br/>
        /// • If MailChimp does not have the contact already, it will be added and tagged.<br/>
        /// • If MailChimp already has the contact without the tag, the tag will be added.<br/>
        /// • If MailChimp already has the contact tagged, it will ensure the other information for the contact (e.g. name) matches that given in the list to sync.<br/>
        /// • If MailChimp has a tagged contact that doesn't appear in the list to sync, it will be untagged.<br/>
        /// By design, any existing contacts that are Unsubscribed in MailChimp will _not_ be resubscribed by the sync process.
        /// </summary>
        /// <param name="contactsToSync">The contacts to sync</param>
        /// <param name="tagName">The tag name with which the contacts should be tagged</param>
        /// <param name="listName">The name of the MailChimp list to sync with</param>
        /// <returns></returns>
        public async Task UpdateMailChimp(IEnumerable<Contact> contactsToSync, string tagName, string listName)
        {
            //Get all lists
            var mailChimpListCollection = await _mailChimpManager.Lists.GetAllAsync();

            var list = mailChimpListCollection.FirstOrDefault(x => x.Name == listName);
            if (list == null)
            {
                _loggerAction.Invoke($"List \"{listName}\" cannot be found", true);
                return;
            }

            //Get list members
            var mailChimpContacts = await _mailChimpManager.Members.GetAllAsync(list.Id);

            int originalMailChimpContactCount = mailChimpContacts.Count(x => MailChimpContactHasTag(x, tagName));
            _loggerAction.Invoke($"\t\tMailChimp contains {originalMailChimpContactCount} contacts with this tag before this update", false);

            int originalUnsubscribedMailChimpContactCount = mailChimpContacts.Count(x => x.Status == Status.Unsubscribed);

            List<Contact> contactsToSyncCopy = contactsToSync.ToList();

            //Go through all the contacts in MailChimp, adjusting tags if needed
            foreach (Member mailChimpContact in mailChimpContacts)
            {
                Contact matchingContactToSync = contactsToSyncCopy.SingleOrDefault(x => x.Email == mailChimpContact.EmailAddress);

                if (matchingContactToSync != null)
                {
                    //The contact to sync already exists in MailChimp. Make sure it is tagged correctly
                    if (!MailChimpContactHasTag(mailChimpContact, tagName))
                    {
                        await AddOrRemoveTagOnMailChimpContact(list.Id, mailChimpContact, tagName, ModifierFlag.Add);
                    }

                    bool needsUpdate = false;
                    foreach (var field in _mergeFieldsToSync)
                    {
                        needsUpdate |= UpdateMergeFieldIfNeeded(mailChimpContact, field.FieldName, field.ContactValue(matchingContactToSync));
                    }

                    if (needsUpdate)
                    {
                        await _mailChimpManager.Members.AddOrUpdateAsync(list.Id, mailChimpContact);
                    }

                    contactsToSyncCopy.Remove(matchingContactToSync);
                }
                else
                {
                    //This contact is not in the list of contacts to sync but already exists in MailChimp. Remove the tag if it's present
                    if (MailChimpContactHasTag(mailChimpContact, tagName))
                    {
                        await AddOrRemoveTagOnMailChimpContact(list.Id, mailChimpContact, tagName, ModifierFlag.Remove);
                    }
                }
            }

            //Now we are just left with the contacts to sync that weren't in MailChimp. These need adding to MailChimp with the correct tag
            foreach (var contactToSync in contactsToSyncCopy)
            {
                var member = new Member { EmailAddress = contactToSync.Email, StatusIfNew = Status.Subscribed };
                foreach (var field in _mergeFieldsToSync)
                {
                    member.MergeFields.Add(field.FieldName, field.ContactValue(contactToSync));
                }
                await _mailChimpManager.Members.AddOrUpdateAsync(list.Id, member);

                await AddOrRemoveTagOnMailChimpContact(list.Id, member, tagName, ModifierFlag.Add);
            }

            //Check that we have the same contacts in MailChimp with the relevant tag as we did in the list to sync
            await ValidateMailChimpState(list.Id, tagName, contactsToSync, originalMailChimpContactCount, originalUnsubscribedMailChimpContactCount);
        }

        private async Task ValidateMailChimpState(string listId, string tagName, IEnumerable<Contact> contactsToSync,
            int originalMailChimpContactCount, int originalUnsubscribedMailChimpContactCount)
        {
            //First get the MailChimp contacts again to check we have changed the remote list, not just a local copy
            IEnumerable<Member> mailChimpContacts = await _mailChimpManager.Members.GetAllAsync(listId);

            //We should not have any more unsubscribed contacts than we started with
            int newUnsubscribedMailChimpContactCount = mailChimpContacts.Count(x => x.Status == Status.Unsubscribed);
            if (originalUnsubscribedMailChimpContactCount != newUnsubscribedMailChimpContactCount)
            {
                FailAfterMailChimpRun($"Unsubscribed contacts have gone from {originalUnsubscribedMailChimpContactCount} to {newUnsubscribedMailChimpContactCount}. This count should not have changed.");
                return;
            }

            //We should have the same number of contacts tagged in MailChimp than were in the list to sync
            var mailChimpContactsWithRelevantTag = mailChimpContacts.Where(x => MailChimpContactHasTag(x, tagName));
            if (mailChimpContactsWithRelevantTag.Count() != contactsToSync.Count())
            {
                FailAfterMailChimpRun($"Number of MailChimp contacts with relevant tag ({mailChimpContactsWithRelevantTag.Count()}) does not match sync list count ({contactsToSync.Count()})");
                return;
            }

            List<string> extraMailChimpContacts = new List<string>();
            List<string> mismatchedInfo = new List<string>();
            foreach (var mailChimpContact in mailChimpContactsWithRelevantTag)
            {
                //There should no longer be any MailChimp contacts with the relevant tag that were not in the list of contacts to sync
                Contact matchingContactToSync = contactsToSync.SingleOrDefault(x => x.Email == mailChimpContact.EmailAddress);
                if (matchingContactToSync == null)
                {
                    extraMailChimpContacts.Add(mailChimpContact.EmailAddress);
                }

                //Check that the MailChimp contacts' information have been updated to match the info in the sync list
                foreach (var field in _mergeFieldsToSync)
                {
                    if (GetMergeFieldValue(mailChimpContact, field.FieldName) != field.ContactValue(matchingContactToSync))
                    {
                        mismatchedInfo.Add($"{mailChimpContact.EmailAddress}: "
                                          + $"MailChimp field '{field.FieldName}' ({GetMergeFieldValue(mailChimpContact, field.FieldName)}) does not match "
                                          + $"sync list value ({field.ContactValue(matchingContactToSync)})");
                    }
                }
            }

            if (extraMailChimpContacts.Any())
            {
                FailAfterMailChimpRun($"MailChimp contains these contacts for tag '{tagName}' that weren't in the list of contact to sync:\r\n\t'{string.Join("\r\n\t", extraMailChimpContacts)}'");
                return;
            }

            if (mismatchedInfo.Any())
            {
                FailAfterMailChimpRun($"Found some contacts whose info (name etc.) wasn't updated in MailChimp:\r\n\t{string.Join("\r\n\t", mismatchedInfo)}");
                return;
            }

            _loggerAction.Invoke($"Validation passed! MailChimp now contains *{mailChimpContactsWithRelevantTag.Count()}* contacts with this tag. (Was *{originalMailChimpContactCount}*)\r\n", false);
        }

        #region Tags

        private bool MailChimpContactHasTag(Member mailChimpContact, string tagName)
        {
            return mailChimpContact.Tags.Select(x => x.Name).Contains(tagName);
        }

        private async Task AddOrRemoveTagOnMailChimpContact(string listId, Member mailChimpContact, string tagName, ModifierFlag flag)
        {
            Tags tags = new Tags();
            tags.MemberTags.Add(new Tag() { Name = tagName, Status = (flag == ModifierFlag.Add ? "active" : "inactive") }); //active = add. inactive = remove
            await _mailChimpManager.Members.AddTagsAsync(listId, mailChimpContact.EmailAddress, tags);
        }

        enum ModifierFlag
        {
            Add,
            Remove
        }

        #endregion

        #region Merge Fields

        private bool UpdateMergeFieldIfNeeded(Member mailChimpContact, string mergeFieldName, string value)
        {
            if (GetMergeFieldValue(mailChimpContact, mergeFieldName) != value)
            {
                mailChimpContact.MergeFields[mergeFieldName] = value;
                return true;
            }

            return false;
        }

        private string GetMergeFieldValue(Member mailChimpContact, string mergeFieldName)
        {
            return mailChimpContact.MergeFields.ContainsKey(mergeFieldName) ? mailChimpContact.MergeFields[mergeFieldName] as string : null;
        }

        #endregion

        private void FailAfterMailChimpRun(string message)
        {
            _loggerAction.Invoke(message, false);
            _loggerAction.Invoke("THIS ERROR HAS OCCURRED AFTER PROCESSING MAILCHIMP. IT IS NOW IN AN UNKNOWN STATE. Do you need to do a manual MailChimp update?", true);
        }
    }
}
