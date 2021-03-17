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
            List<Contact> contactsToSync = new List<Contact>
            {
                leslie,
                new Contact("April", "Ludgate", "april.ludgate@parksdept.com"),
                new Contact("Ron", "Swanson", "ron.swanson@parksdept.com"),
                new Contact("Andy", "Dwyer", "andy.dwyer@parksdept.com"),
                new Contact("Ann", "Perkins", "ann.perkins@parksdept.com"),
                new Contact("Tom", "Haverford", "tom.haverford@parksdept.com"),
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
            Output("Updating MailChimp again...");
            await mailChimpSyncer.UpdateMailChimp(contactsToSync, tagName, listName);

            //Finally, re-add a contact to check that we can add tags to existing contacts
            contactsToSync.Add(leslie);
            Output("Updating one more time...");
            await mailChimpSyncer.UpdateMailChimp(contactsToSync, tagName, listName);

            Output("Done!");
            Output("Press enter to exit...");
            Console.ReadLine();
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
