using Algolia.Search.Clients;
using IntercomSearchProjectCore.AlgoliaModels;
using IntercomSearchProjectCore.KontentModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntercomSearchProjectCore
{
    public class AlgoliaFunctions
    {
        private readonly SearchClient client;
        private readonly SearchIndex index;

        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        public AlgoliaFunctions(AlgoliaSettings settings)
        {
            client = new SearchClient(settings.ApplicationId, settings.ApiKey);
            index = client.InitIndex(settings.IndexName);
        }

        internal async Task<bool> UpdateObject(SearchConversation conversation)
        {
            try
            {
                if (conversation.ConversationSplit == "ConversationPart")
                {
                    var parts = conversation.SplitConversation();
                    await index.SaveObjectsAsync(parts);
                }
                else
                {
                    await index.SaveObjectAsync(conversation);
                }
                return true;
            }
            catch (Exception e)
            {
                logger.Error(e, "Unable to update object.");
                return false;
            }

        }

        /*internal static SearchUser CreateSearchUser(IntercomUser userObject)
        {
            SearchUser searchUser = new SearchUser();
            if (userObject != null)
            {
                searchUser.Email = userObject.email;
                searchUser.Name = userObject.name;
            }
            return searchUser;
        }*/

    }



    public class AlgoliaSettings
    {
        public string ApplicationId { get; set; }
        public string ApiKey { get; set; }
        public string IndexName { get; set; }
    }

}
