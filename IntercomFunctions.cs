using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Intercom.Clients;
using Intercom.Core;
using Intercom.Data;
using Intercom.Factories;
using IntercomSearchProjectCore.IntercomModels;
using IntercomSearchProjectCore.KontentModels;

namespace IntercomSearchProjectCore
{
    public class IntercomFunctions
    {
        private readonly Authentication auth;
        private readonly RestClientFactory factory;
        private readonly ConversationsClient conversationsClient;
        private readonly UsersClient usersClient;
        private readonly AdminsClient adminsClient;

        private Dictionary<string, IntercomUser> userList;

        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        public IntercomFunctions(string authApiKey)
        {
            auth = new Authentication(authApiKey);
            factory = new RestClientFactory(auth);

            conversationsClient = new ConversationsClient(factory);
            usersClient = new UsersClient(factory);
            adminsClient = new AdminsClient(factory);

            userList = new Dictionary<string, IntercomUser> { };
        }

        internal List<Conversation> GetAllConversations()
        {
            Dictionary<String, String> firstparameters = new Dictionary<string, string>
                {
                    { "per_page", "60" }
                };

            List<Conversation> result = new List<Conversation> { };

            logger.Debug("Requesting all conversations from intercom.");
            Conversations conversations = conversationsClient.ListAll(firstparameters);
            int currentPage = conversations.pages.page;
            int lastPage = conversations.pages.total_pages;
            result.AddRange(conversations.conversations);
            foreach (var conversation in conversations.conversations)
            {
                logger.Trace(conversation.id);
            }


            logger.Debug("Received page " + currentPage + " out of " + lastPage + ".");

            while (currentPage++ != lastPage)
            {
                Dictionary<String, String> parameters = new Dictionary<string, string>
                {
                    { "page", currentPage.ToString() },
                    { "per_page", "60" }
                };
                conversations = conversationsClient.ListAll(parameters);
                result.AddRange(conversations.conversations);
                foreach (var conversation in conversations.conversations)
                {
                    logger.Trace(conversation.id);
                }

                logger.Debug("Received page " + currentPage + " out of " + lastPage + ".");
            }

            return result;

        }

        internal Conversation GetConversation(string convoId)
        {
            logger.Debug("Getting conversation from Intercom: " + convoId);
            return conversationsClient.View(convoId);
        }

        internal static string GetIntercomConversationLink(Conversation convo)
        {
            return "https://app.intercom.io/a/apps/e42kus8l/conversations/" + convo.id;
        }

        internal static string GetIntercomUserLink(string userId, string type)
        {
            if (type == "admin") return "https://app.intercom.io/a/apps/e42kus8l/admins/" + userId;
            return "https://app.intercom.io/a/apps/e42kus8l/users/" + userId;
        }

        internal static int GetConversationMessageCount(Conversation conversation)
        {
            int count = 1;
            foreach (var part in conversation.conversation_parts)
            {
                if (part.body != null && part.author.type != "bot") ++count;
            }

            return count;
        }

        internal IntercomUser GetIntercomUser(GenericIntercomUser genericUser)
        {
            if (!userList.ContainsKey(genericUser.Id))
            {
                IntercomUser newUser;
                switch (genericUser.Type)
                {
                    case "admin":
                        Admin admin;
                        try
                        {
                            logger.Trace("Downloading admin from Intercom: " + genericUser.Id);
                            admin = adminsClient.View(genericUser.Id);
                            newUser = new IntercomUser(admin);
                        }
                        catch (Intercom.Exceptions.ApiException e)
                        {
                            if (e.StatusCode == 404)
                            {
                                logger.Trace("Admin request returned 404 for: " + genericUser.Id);
                                newUser = new IntercomUser()
                                {
                                    Id = genericUser.Id,
                                    Type = genericUser.Type,
                                    Name = "Non-existing admin",
                                    Email = ""
                                };

                            }
                            else
                            {
                                logger.Error(e, "Couldn't download admin from Intercom.");
                                throw e;
                            }
                                
                        }
                        userList.Add(genericUser.Id, newUser);
                        break;
                    case "user":
                    case "lead":
                        User user;
                        try
                        {
                            logger.Trace("Downloading user from Intercom: " + genericUser.Id);
                            user = usersClient.View(genericUser.Id);
                            newUser = new IntercomUser(user);
                        }
                        catch (Intercom.Exceptions.ApiException e)
                        {
                            if (e.StatusCode == 404)
                            {
                                logger.Trace("User request returned 404 for: " + genericUser.Id);
                                newUser = new IntercomUser()
                                {
                                    Id = genericUser.Id,
                                    Type = genericUser.Type,
                                    Name = "Non-existing user",
                                    Email = ""
                                };

                            }
                            else
                            {
                                logger.Error(e, "Couldn't download user from Intercom.");
                                throw e;
                            }

                        }
                        userList.Add(genericUser.Id, newUser);
                        break;
                    default:
                        logger.Warn("Unknown type of user requested.");
                        break;
                }
            }
            return userList[genericUser.Id];
        }

        internal static List<GenericIntercomUser> GetAllConversationParticipants(Conversation conversation)
        {
            List<GenericIntercomUser> participantsList = new List<GenericIntercomUser> { };

            //add assignee - convo can be unassigned
            if (conversation.assignee.type != "nobody_admin")
                participantsList.Add(new GenericIntercomUser(conversation.assignee.id, conversation.assignee.type));

            //add author
            participantsList.Add(new GenericIntercomUser(conversation.conversation_message.author.id, conversation.conversation_message.author.type));

            //add targeted user, as he might have never replied
            participantsList.Add(new GenericIntercomUser(conversation.user.id, conversation.user.type));

            //add all people in conversation that are not bots
            foreach (var conversationPart in conversation.conversation_parts)
            {
                if (conversationPart.author.type != "bot")
                {
                    participantsList.Add(new GenericIntercomUser(conversationPart.author.id, conversationPart.author.type));
                }
            }

            return participantsList.Distinct(new GenericIntercomUserComparer()).ToList();
        }

    }
}
