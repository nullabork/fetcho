using System;
using System.Collections.Generic;

namespace Fetcho.Common.Entities
{
    public class Account
    {
        public const int MinKeyLength = 12;

        public string Name { get; set; }

        public DateTime Created { get; set; }

        public bool IsActive { get; set; }

        public List<AccessKey> AccessKeys { get; set; }

        public List<AccountProperty> Properties { get; set; }
        
        public Account()
        {
            Created = DateTime.UtcNow;
            IsActive = true;
            Name = Utility.GetRandomHashString();
            AccessKeys = new List<AccessKey>();
            Properties = new List<AccountProperty>();
        }

        public static bool IsValid(Account key)
        {
            if (string.IsNullOrWhiteSpace(key.Name))
                return false;
            if (key.Name.Length < MinKeyLength)
                return false;
            return true;
        }

        public static void Validate(Account key)
        {
            if (string.IsNullOrWhiteSpace(key.Name))
                throw new InvalidObjectFetchoException("No key set");
            if (key.Name.Length < MinKeyLength)
                throw new InvalidObjectFetchoException("Key too short");
            if (!Account.IsValid(key))
                throw new InvalidObjectFetchoException("Object is invalid");
        }
    }
}
