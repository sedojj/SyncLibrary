using IntercomSearchProjectCore.AlgoliaModels;

namespace IntercomSearchProjectCore.KontentModels
{
    public partial class UserModel
    {
        public string IntercomLink { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Type { get; set; }
        public string Id { get; set; }

        public static explicit operator SearchUser(UserModel user)
        {
            SearchUser searchUser = new SearchUser();
            searchUser.Email = user.Email;
            searchUser.Name = user.Name;
            return searchUser;
        }

    }
}
