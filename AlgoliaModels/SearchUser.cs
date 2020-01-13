using System;
using System.Collections.Generic;

namespace IntercomSearchProjectCore.AlgoliaModels
{
    public partial class SearchUser
    {
        public string Name { get; set; }
        public string Email { get; set; }

        public SearchUser()
        {

        }
        public SearchUser(string name, string email)
        {
            Name = name;
            Email = email;
        }

    }

}
