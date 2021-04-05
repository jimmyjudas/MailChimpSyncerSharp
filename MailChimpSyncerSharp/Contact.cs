using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MailChimpSyncerSharp
{
    public class Contact
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public Dictionary<string, string> AdditionalMergeFields { get; set; }
        public AddressDictionary Address { get; private set; }

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

        public Contact(string firstName, string lastName, string email, params (string Key, string Value)[] additionalMergeFields)
        {
            FirstName = firstName;
            LastName = lastName;
            Email = email;

            if (additionalMergeFields != null)
            {
                AdditionalMergeFields = new Dictionary<string, string>();
                foreach (var field in additionalMergeFields)
                {
                    AdditionalMergeFields.Add(field.Key, field.Value);
                }
            }
		}

        public Contact(string firstName, string lastName, string email,
            string addr1 = null, string addr2 = null, string city = null, string state = null, string zip = null, string country = null)
        {
            FirstName = firstName;
            LastName = lastName;
            Email = email;

            AddAddress(addr1, addr2, city, state, zip, country);
        }

        public void AddAddress(string addr1 = null, string addr2 = null, string city = null, string state = null, string zip = null, string country = null)
        { 
            if (addr1 != null || addr2 != null || city != null || state != null || zip != null || country != null)
            {
                Address = new AddressDictionary();
                Address.Add("addr1", addr1 ?? string.Empty);
                if (!string.IsNullOrEmpty(addr2)) Address.Add("addr2", addr2); //Optional
                Address.Add("city", city ?? string.Empty);
                Address.Add("state", state ?? string.Empty);
                Address.Add("zip", zip ?? string.Empty);
                if (!string.IsNullOrEmpty(country)) Address.Add("country", country); //Optional
            }
        }

        public override string ToString()
        {
            return $"{FullName}: {Email}";
        }
    }

    public class AddressDictionary : Dictionary<string, string>
    {
        public override string ToString()
        {
            return string.Join(", ", Values.Where(x => !string.IsNullOrEmpty(x)));
        }
    }
}
