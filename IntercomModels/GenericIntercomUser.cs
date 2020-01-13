using System;
using System.Collections.Generic;

namespace IntercomSearchProjectCore.IntercomModels
{
    public class GenericIntercomUser
    {
        public string Id { get; set; }
        public string Type { get; set; }

        public GenericIntercomUser()
        {

        }

        public GenericIntercomUser(string id, string type)
        {
            Id = id;
            Type = type;
        }
    }

    // Custom comparer for the GenericIntercomUser class
    public class GenericIntercomUserComparer : IEqualityComparer<GenericIntercomUser>
    {
        // Products are equal if their names and product numbers are equal.
        public bool Equals(GenericIntercomUser x, GenericIntercomUser y)
        {

            //Check whether the compared objects reference the same data.
            if (Object.ReferenceEquals(x, y)) return true;

            //Check whether any of the compared objects is null.
            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
                return false;

            //Check whether the products' properties are equal.
            return x.Id == y.Id && x.Type == y.Type;
        }

        // If Equals() returns true for a pair of objects 
        // then GetHashCode() must return the same value for these objects.

        public int GetHashCode(GenericIntercomUser user)
        {
            //Check whether the object is null
            if (Object.ReferenceEquals(user, null)) return 0;

            //Get hash code for the Id field if it is not null.
            int hashUserId = user.Id.GetHashCode();

            //Get hash code for the Type field.
            int hashUsertype = user.Type.GetHashCode();

            //Calculate the hash code for the product.
            return hashUserId ^ hashUsertype;
        }

    }

}
