using Intercom.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntercomSearchProjectCore.IntercomModels
{
    public class IntercomUser : GenericIntercomUser
    {
        public string Email { get; set; }
        public string Name { get; set; }

        public IntercomUser()
        {

        }

        public IntercomUser(Admin admin)
        {
            Id = admin.id;
            Type = admin.type;
            Email = admin.email;
            Name = admin.name;
        }

        public IntercomUser(User user)
        {
            Id = user.id;
            Type = user.type;
            Email = user.email;
            Name = user.name;
        }

    }

}
