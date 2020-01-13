using Intercom.Data;
using IntercomSearchProjectCore.AlgoliaModels;
using IntercomSearchProjectCore.IntercomModels;
using IntercomSearchProjectCore.KontentModels;
using KenticoCloud.ContentManagement.Exceptions;
using KenticoCloud.ContentManagement.Models.Items;
using KenticoCloud.ContentManagement.Models.StronglyTyped;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace IntercomSearchProjectCore
{
    public class SearchProjectClient
    {
        private readonly IntercomFunctions intercom;
        private readonly KontentFunctions kontent;
        private readonly AlgoliaFunctions algolia;

        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
         
        private Dictionary<string, bool> processedItems;

        private readonly bool emptyProject;
        private readonly IEnumerable<string> bannedConversations;

        private Dictionary<string, SearchConversation> searchConversations;
        private Dictionary<string, SearchUser> searchUsers;

        public SearchProjectClient(string intercomApiAuthKey, KontentFunctionsSettings kontentSettings, AlgoliaSettings algoliaSettings)
        {
            intercom = new IntercomFunctions(intercomApiAuthKey);
            kontent = new KontentFunctions(kontentSettings);
            algolia = new AlgoliaFunctions(algoliaSettings);
            processedItems = new Dictionary<string, bool>();

            emptyProject = kontentSettings.CleanProject;
            bannedConversations = kontentSettings.BannedConversations.Split(',');

            searchConversations = new Dictionary<string, SearchConversation>() { };
            searchUsers = new Dictionary<string, SearchUser>() { };

        }

        public async Task SyncAll()
        {
            var intercomConversations = intercom.GetAllConversations();
            logger.Debug("Succesfully retrieved " + intercomConversations.Count.ToString() + " conversations.");

            foreach (var conversation in intercomConversations)
            {
                await SyncSingle(conversation.id);
            }
        }

        public async Task<bool> SyncSingle(string conversationId)
        {
            if (bannedConversations.Contains(conversationId))
            {
                // This is currently for conversations with big data, which fails in KK
                logger.Info("Conversation is banned: " + conversationId);
                return false;
            }
                
            Conversation intercomConversation;

            // Get the conversation from Intercom
            try
            {
                intercomConversation = intercom.GetConversation(conversationId);
                if (intercomConversation == null)
                {
                    logger.Error("Conversation was returned empty: " + conversationId);
                    return false;
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Wasn't able to get the conversation from intercom: " + conversationId);
                return false;
            }

            // Try to get conversation from existing project
            ContentItemVariantModel<ConversationModel> conversationVariant = null;
            if (!emptyProject)
            {
                conversationVariant = await kontent.TryGetExistingConversationVariant(intercomConversation.id);
            }

            logger.Debug("Synchronizing conversation to Kentico Kontent: " + conversationId);
            if (!ConversationNeedsUpdate(intercomConversation, conversationVariant))
            {
                return true;
            }
            else
            {
                if (conversationVariant != null)
                {
                    await kontent.UnpublishItemVariant(conversationVariant.Item.Id.ToString());
                }              
            }

            // Extract all generic users participating in the conversation (id/type)
            var genericParticipants = IntercomFunctions.GetAllConversationParticipants(intercomConversation);
            List<ContentItemVariantModel<UserModel>> conversationUsers = new List<ContentItemVariantModel<UserModel>> { };

            logger.Debug("Trying to get existing user variants from Kontent.");
            foreach (var genericParticipant in genericParticipants)
            {
                ContentItemVariantModel<UserModel> userVariant;
                userVariant = await kontent.TryGetExistingUserVariant(genericParticipant.Id);
                if (userVariant == null)
                {
                    var intercomUser = intercom.GetIntercomUser(genericParticipant);
                    userVariant = await kontent.CreateUser(intercomUser);
                }
                conversationUsers.Add(userVariant);
            }

            var result = await kontent.SyncSingle(intercomConversation, conversationVariant, conversationUsers);

            if (!result.success)
                return false;

            conversationVariant = result.variant;

            // synchronizovat do algolie
            logger.Debug("Synchronizing object to Algolia: " + conversationId);
            List<SearchUser> searchUserParticipants = new List<SearchUser>() { };
            SearchUser assignee;
            foreach (var user in conversationUsers)
            {
                searchUserParticipants.Add(new SearchUser(user.Elements.Name, user.Elements.Email));
            }
            if (intercomConversation.assignee.id != null)
            {
                var searchAssignee = intercom.GetIntercomUser(new GenericIntercomUser(intercomConversation.assignee.id, intercomConversation.assignee.type));
                 assignee = new SearchUser(searchAssignee.Name, searchAssignee.Email);
            }
            else
            {
                assignee = new SearchUser("unassigned", "");
            }

            var searchConversation = new SearchConversation(conversationVariant.Elements, assignee, searchUserParticipants);

            await algolia.UpdateObject(searchConversation);

            return true;
        }

        public async Task CleanProject()
        {
            await kontent.CleanProject();
        }

        private bool ConversationNeedsUpdate(Conversation intercomConversation, ContentItemVariantModel<ConversationModel> conversationVariant)
        {
            // we don't want to synchronize open/snoozed conversations
            if (intercomConversation.state != "closed")
            {
                logger.Debug("Skipping conversation because it's not closed: " + intercomConversation.id);
                return false;
            }

            // conversation doesn't exist yet
            if (conversationVariant == null)
            {
                logger.Trace("Conversation doesn't exist in KK but does in intercom: " + intercomConversation.id);
                return true;
            }


            // if timestamps match, there is no reason to update
            logger.Trace("IC date -> " + intercomConversation.updated_at + " " + long.Parse(Tools.ToUnixTimestamp(conversationVariant.Elements.LastUpdated)) + " <- KK date:");
            if (intercomConversation.updated_at == long.Parse(Tools.ToUnixTimestamp(conversationVariant.Elements.LastUpdated)))
            {
                logger.Debug("Conversation is up to date: " + conversationVariant.Elements.ConversationId);          
                return false;
            }

            return true;
        }

        private bool UserNeedsUpdate(dynamic user, ContentItemVariantModel<UserModel> userVariant)
        {
            if (userVariant == null)
                return true;

            if (userVariant != null && user == null)
            {
                //this user doesn't exist anymore in intercom???
            }


            //match fields

            return true;
        }


    }
}
