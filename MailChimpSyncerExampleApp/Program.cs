using MailChimpSyncerSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MailChimpSyncerExampleApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            //Create some new contacts to sync
            Contact leslie = new Contact("Leslie", "Knope", "leslie.knope@parksdept.com");
            Contact ben = new Contact("Ben", "Wyatt", "ben.wyatt@parksdept.com", addr1: "ABC Street", city: "Partridge", state: "MN", zip: "12345", country: "US");
            List<Contact> contactsToSync = new List<Contact>
            {
                leslie,
                new Contact("April", "Ludgate", "april.ludgate@parksdept.com"),
                new Contact("Ron", "Swanson", "ron.swanson@parksdept.com"),
                new Contact("Andy", "Dwyer", "andy.dwyer@parksdept.com"),
                new Contact("Ann", "Perkins", "ann.perkins@parksdept.com"),
                new Contact("Tom", "Haverford", "tom.haverford@parksdept.com"),
                ben
            };

            string tagName = "TestTag";
            string listName = "LIST NAME GOES HERE";

            //To create an instance of MailChimpSyncer, you need to pass in an API Key for your account, which can be generated
            //through MailChimp (Account > Extras > API Keys)
            MailChimpSyncer mailChimpSyncer = new MailChimpSyncer("API KEY GOES HERE", (message, isError) => Output(message, isError));

            //Add the new contacts
            Output("Updating MailChimp...");
            await mailChimpSyncer.UpdateMailChimp(contactsToSync, tagName, listName);

            //Now make some changes to the list of contacts and resync. MailChimpSyncer will verify that these changes have been made before
            //returning successfully
            
            contactsToSync.Remove(leslie); //This contact will be untagged
            contactsToSync.Single(x => x.LastName == "Ludgate").LastName = "Ludgate-Dwyer"; //This contact will remain tagged, and have its last name updated

            //This contact will remain tagged, and have its address updated
            contactsToSync.Remove(ben);
            ben = new Contact(ben.FirstName, ben.LastName, ben.Email, addr1: "Bealey Ave", city: "Pawnee", state: "IN", zip: "6789", country: "US");
            contactsToSync.Add(ben); 

            Output("Updating MailChimp again...");
            await mailChimpSyncer.UpdateMailChimp(contactsToSync, tagName, listName);

            //A few final changes
            
            contactsToSync.Add(leslie); //Re-add a contact to check that we can add tags to existing contacts

            //This contact will remain tagged, and have its address cleared
            contactsToSync.Remove(ben);
            ben = new Contact(ben.FirstName, ben.LastName, ben.Email);
            contactsToSync.Add(ben);

            contactsToSync.Add(new Contact("Burt", "Macklin", "hdsjkfnd@sjknjkadd")); //Also try and add an invalid email address

            Output("Updating one more time...");
            MailChimpUpdateReport report = await mailChimpSyncer.UpdateMailChimp(contactsToSync, tagName, listName);

            //Note that MailChimpSyncer also returns a report about which email addresses were invalid, which emails already existed in MailChimp but 
            //were unsubscribed, and which emails had previously been permanently deleted from the MailChimp audience and so can not be re-added. For
            //example, we would expect there to have been 1 invalid email in the last sync:
            Output($"The last sync contained {report.InvalidEmails.Count} invalid email that was ignored by MailChimp");
            Output("");

            Output("Done!");
            Output("Press enter to exit...");
            Console.ReadLine();

            //Note it is also possible to set up additional fields in MailChimp and then set those fields through MailChimpSyncer. For
            //example, if you've set up additional text fields in Mailchimp with Merge Tags "CITY" and "JOB" you can set those fields
            //when creating a Contact as follows:
            //
            //new Contact("Ben", "Wyatt", "ben.wyatt@parksdept.com", ("CITY", "Partridge"), ("JOB", "Mayor"));
        }

        static void Output(string message, bool isTerminal = false)
        {
            Console.WriteLine(message);

            if (isTerminal)
            {
                Console.WriteLine("Press enter to exit...");
                Console.ReadLine();
                Environment.Exit(0);
            }
        }
    }
}
