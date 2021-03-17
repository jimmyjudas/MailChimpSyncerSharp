using System;
using System.Collections.Generic;
using System.Text;

namespace MailChimpSyncerSharp
{
    public class Contact
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }

        public string FullName
        {
            get
            {
                string name = string.Empty;

                if (!string.IsNullOrWhiteSpace(FirstName))
                {
                    name = FirstName;
                }

                if (!string.IsNullOrWhiteSpace(LastName))
                {
                    if (!name.EndsWith(" "))
                    {
                        name += " ";
                    }
                    name += LastName;
                }

                return name;
            }
        }

        public Contact(string firstName, string lastName, string email)
        {
            FirstName = firstName;
            LastName = lastName;
            Email = email;
        }

        public override string ToString()
        {
            return $"{FullName}: {Email}";
        }
    }
}
