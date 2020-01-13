using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;

using KenticoCloud.ContentManagement;
using KenticoCloud.ContentManagement.Models;
using KenticoCloud.ContentManagement.Models.Items;
using KenticoCloud.ContentManagement.Models.StronglyTyped;
using KenticoCloud.ContentManagement.Exceptions;

using Intercom.Data;

using IntercomSearchProjectCore.KontentModels;
using IntercomSearchProjectCore.IntercomModels;
using System.Threading;
using System.Net;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;

namespace IntercomSearchProjectCore
{
    public class KontentFunctions
    {
        private readonly ContentManagementOptions options;
        private readonly ContentManagementClient managementClient;

        private readonly IEnumerable<string> bannedConversations;
        private readonly bool cleanProject;

        private readonly Guid conversationTypeGuid;
        private readonly Guid userTypeGuid;

        private Dictionary<string, ContentItemVariantModel<UserModel>> userItemVariants;

        private string currentConversationId;
        private List<UserModel> currentConversationUsers;

        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public KontentFunctions(KontentFunctionsSettings settings)
        {
            options = new ContentManagementOptions
            {
                ProjectId = settings.ProjectId,
                ApiKey = settings.CMApiKey
            };
            managementClient = new ContentManagementClient(options);

            cleanProject = settings.CleanProject;

            userItemVariants = new Dictionary<string, ContentItemVariantModel<UserModel>> { };

            conversationTypeGuid = Guid.Parse(settings.ConversationTypeGuid);
            userTypeGuid = Guid.Parse(settings.UserTypeGuid);

            bannedConversations = settings.BannedConversations.Split(',');

            if (!EnsureProject().Result)
                throw new Exception("");
        }

        internal async Task<(ContentItemVariantModel<ConversationModel> variant, bool success)> SyncSingle(Conversation intercomConversation, ContentItemVariantModel<ConversationModel> conversationVariant, List<ContentItemVariantModel<UserModel>> conversationUserVariants)
        {
            currentConversationId = intercomConversation.id;
            currentConversationUsers = new List<UserModel>() { };
            currentConversationUsers.AddRange(conversationUserVariants.Select(x => x.Elements));

            ContentItemModel conversationItem = null;
            Guid itemIdentifier = Guid.Empty;

            // If conversation doesn't exist, create item for it
            if (conversationVariant == null)
            {
                try
                {
                    logger.Debug("Creating item for conversation: " + currentConversationId);
                    conversationItem = await CreateConversationItem(currentConversationId);
                    itemIdentifier = conversationItem.Id;
                }
                catch (ContentManagementException e)
                {
                    logger.Error(e, "Unable to create item for conversation: " + currentConversationId);
                    return (null, false);
                }

            }
            else
            {
                itemIdentifier = conversationVariant.Item.Id;
            }

            // Fill model with data
            var model = CreateConversationModel(intercomConversation);

            logger.Debug("Creating item variant for conversation: " + currentConversationId);
            var variant = await UpsertConversationVariant(model, ContentItemIdentifier.ById(itemIdentifier));
            var publishConversation = await PublishItemVariant(itemIdentifier.ToString());

            return (variant, true);

        }

        internal async Task<ContentItemVariantModel<ConversationModel>> TryGetExistingConversationVariant(string conversationId)
        {
            ContentItemVariantModel<ConversationModel> conversationVariant = null;
            try
            {
                ContentItemVariantIdentifier variantIdentifier = new ContentItemVariantIdentifier(ContentItemIdentifier.ByExternalId(conversationId), LanguageIdentifier.DEFAULT_LANGUAGE);
                conversationVariant = await GetConversationVariant(variantIdentifier);
                logger.Debug("Downloaded existing conversation variant: " + conversationVariant.Elements.ConversationId);
            }
            catch (ContentManagementException e)
            {
                // If it's 404, conversation doesn't exist (yet)
                if (e.StatusCode != HttpStatusCode.NotFound)
                    throw e;
            }
            return conversationVariant;
        }

        internal async Task<ContentItemVariantModel<UserModel>> TryGetExistingUserVariant(string userId)
        {
            if (!userItemVariants.ContainsKey(userId))
            {
                ContentItemVariantModel<UserModel> userVariant = null;
                try
                {
                    ContentItemIdentifier userItemIdentifier = ContentItemIdentifier.ByExternalId(userId);
                    ContentItemVariantIdentifier variantIdentifier = new ContentItemVariantIdentifier(userItemIdentifier, LanguageIdentifier.DEFAULT_LANGUAGE);
                    userVariant = await GetUserVariant(variantIdentifier);
                    logger.Debug("Downloaded existing user variant: " + userVariant.Elements.Name);
                    userItemVariants.Add(userVariant.Elements.Id, userVariant);
                }
                catch (ContentManagementException e)
                {
                    if (e.StatusCode != HttpStatusCode.NotFound)
                        throw e;

                    return null;
                }
            }
            return userItemVariants[userId];
        }

        private ConversationModel CreateConversationModel(Conversation conversation)
        {
            var messages = CreateRichTextCompatibleConversationString(conversation);
            var tags = CreateTagsString(conversation);
            string intercomConversationLinkRichText = "<p><a href=\"" + IntercomFunctions.GetIntercomConversationLink(conversation) + "\">Link to intercom</a></p>";

            List<ContentItemIdentifier> assignees = new List<ContentItemIdentifier>() { };
            List<ContentItemIdentifier> author = new List<ContentItemIdentifier>() { };

            if (conversation.assignee.id != null) //unassigned conversation
                assignees.Add(ContentItemIdentifier.ByExternalId(currentConversationUsers
                    .Where(x => x.Id.Equals(conversation.assignee.id, StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault()
                    .Id));

            author.Add(ContentItemIdentifier.ByExternalId(currentConversationUsers
                .Where(x => x.Id.Equals(conversation.conversation_message.author.id, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault()
                .Id));

            ConversationModel conversationModel = new ConversationModel
            {
                IntercomLink = intercomConversationLinkRichText,
                Author = author,
                CreatedAt = Tools.FromUnixTimestamp(conversation.created_at),
                LastUpdated = Tools.FromUnixTimestamp(conversation.updated_at),
                Assignee = assignees,
                Messages = messages,
                RatingValue = conversation.conversation_rating.rating,
                RatingNote = conversation.conversation_rating.remark,
                Tags = tags,
                SearchBody = CreateSearchBody(conversation),
                /*Participants = userItemVariants
                    .Where(userReferences => participants.Any(participant => participant.Id.Equals(userReferences.Key, StringComparison.OrdinalIgnoreCase)))
                    .Select(userReference => userReference.Value)
                    .ToList(),*/
                Participants = currentConversationUsers
                    .Select(x => ContentItemIdentifier.ByExternalId(x.Id))
                    .ToList(),
                MessageCount = IntercomFunctions.GetConversationMessageCount(conversation),
                ConversationId = conversation.id
            };

            return conversationModel;
        }

        internal async Task DeleteItems(List<ContentItemIdentifier> itemIdentifiers)
        {
            foreach (var itemIdentifier in itemIdentifiers)
            {
                try
                {
                    logger.Debug("Deleting item: " + itemIdentifier.ExternalId);
                    await managementClient.DeleteContentItemAsync(itemIdentifier);
                }
                catch (Exception e)
                {
                    logger.Error(e, "Deleting of item " + itemIdentifier.Id + "failed.");
                }
                finally
                {
                }
            }
        }

        internal async Task DeleteItems(List<string> externalIds)
        {
            foreach (var itemId in externalIds)
            {
                try
                {
                    await managementClient.DeleteContentItemAsync(ContentItemIdentifier.ByExternalId(itemId));
                    //processedItems.Add(itemId, true);
                }
                catch (Exception e)
                {
                    logger.Error(e, "Deleting of item " + itemId + "failed.");
                }
                finally
                {
                    //processedItems.Add(itemId, false);
                }
            }
        }

        private async Task<ContentItemModel> CreateConversationItem(string conversationId)
        {
            ContentItemModel responseItem;
            ContentItemCreateModel item = new ContentItemCreateModel
            {
                Name = conversationId,
                Type = ContentTypeIdentifier.ByCodename("conversation"),
                ExternalId = conversationId
            };

            responseItem = await managementClient.CreateContentItemAsync(item);
            return responseItem;
        }

        private async Task<ContentItemVariantModel<ConversationModel>> UpsertConversationVariant(ConversationModel conversation, ContentItemIdentifier itemIdentifier)
        {
            LanguageIdentifier languageIdentifier = LanguageIdentifier.ByCodename("default");
            ContentItemVariantIdentifier variantIdentifier = new ContentItemVariantIdentifier(itemIdentifier, languageIdentifier);

            ContentItemVariantModel<ConversationModel> responseVariant = new ContentItemVariantModel<ConversationModel> { };
            try
            {
                responseVariant = await managementClient.UpsertContentItemVariantAsync<ConversationModel>(variantIdentifier, conversation);
            }
            catch (Exception e)
            {
                logger.Debug(e, "Failed to create conversation variant.");
            }
            return responseVariant;
        }

        private async Task<ContentItemVariantModel<ConversationModel>> GetConversationVariant(ContentItemVariantIdentifier variantIdentifier)
        {
            return await managementClient.GetContentItemVariantAsync<ConversationModel>(variantIdentifier);
        }

        private async Task<ContentItemVariantModel<UserModel>> GetUserVariant(ContentItemVariantIdentifier variantIdentifier)
        {
            return await managementClient.GetContentItemVariantAsync<UserModel>(variantIdentifier);
        }

        internal async Task<ContentItemVariantModel<UserModel>> CreateUser(IntercomUser user)
        {
            string itemName;
            if (user.Type == "admin")
                itemName = user.Name;
            else
                itemName = user.Id;

            var userItem = await CreateUserItem(itemName, user.Id);

            UserModel userModel = new UserModel
            {
                Name = user.Name,
                Type = user.Type,
                Email = user.Email,
                Id = user.Id,
                IntercomLink = "<p><a href=\"" + IntercomFunctions.GetIntercomUserLink(user.Id, user.Type) + "\">Link to intercom</a></p>"
            };

            var userVariant = await CreateUserVariant(userModel, ContentItemIdentifier.ById(userItem.Id));
            var publishUser = await PublishItemVariant(userItem.Id.ToString());

            return userVariant;
        }

        private async Task<ContentItemModel> CreateUserItem(string name, string id)
        {

            var item = new ContentItemCreateModel
            {
                Name = name,
                Type = ContentTypeIdentifier.ByCodename("user"),
                ExternalId = id

            };

            ContentItemModel responseItem = await managementClient.CreateContentItemAsync(item);
            return responseItem;
        }

        private async Task<ContentItemVariantModel<UserModel>> CreateUserVariant(UserModel userModel, ContentItemIdentifier itemIdentifier)
        {
            LanguageIdentifier languageIdentifier = LanguageIdentifier.ByCodename("default");
            ContentItemVariantIdentifier variantIdentifier = new ContentItemVariantIdentifier(itemIdentifier, languageIdentifier);

            ContentItemVariantModel<UserModel> responseVariant = await managementClient.UpsertContentItemVariantAsync<UserModel>(variantIdentifier, userModel);

            return responseVariant;
        }

        internal async Task<bool> EnsureProject()
        {
            //get GUIDs for content types from config file, if not available, try to get them from project, if not found, create them
            if (this.conversationTypeGuid != Guid.Empty && this.userTypeGuid != Guid.Empty)
                return true;

            HttpResponseMessage result;
            using (HttpClient httpClient = new HttpClient())
            {
                string[] conversationElementValues =
                {
                    "ConversationId,conversationid,text",
                    "IntercomLink,intercomlink,rich_text",
                    "Messages,messages,rich_text",
                    "CreatedAt,createdat,date_time",
                    "LastUpdated,lastupdated,date_time",
                    "Author,author,modular_content",
                    "Assignee,assignee,modular_content",
                    "Participants,participants,modular_content",
                    "RatingValue,ratingvalue,text",
                    "RatingNote,ratingnote,text",
                    "Tags,tags,text",
                    "MessageCount,messagecount,number",
                    "SearchBody,searchbody,text"
                };

                ContentTypeElementModel func(string x)
                {
                    var splitString = x.Split(',');
                    return new ContentTypeElementModel(splitString[0], splitString[1], splitString[2]);
                }

                List<ContentTypeElementModel> conversationTypeElements = new List<ContentTypeElementModel>(
                        conversationElementValues
                            .Select(x => func(x)));             


                ContentTypeModel conversationModel = new ContentTypeModel()
                {
                    ExternaId = "conversation",
                    Name = "Conversation",
                    Codename = "conversation",
                    Elements = conversationTypeElements
                };

                var payload = JsonConvert.SerializeObject(conversationModel);

                httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + options.ApiKey);
                result = await httpClient.PostAsync("https://manage.kenticocloud.com/v2/projects/" + options.ProjectId +  "/types", new StringContent(payload, Encoding.UTF8, "application/json"));
            }

            var reply = await result.Content.ReadAsStringAsync();

            if (reply == null)
                return false;

            dynamic output = JsonConvert.DeserializeObject(reply);
            Guid conversationTypeGuid = Guid.Parse(output.GetValue("id").ToString());

            using (HttpClient httpClient = new HttpClient())
            {
                string[] userElementValues =
                {
                    "IntercomLink,intercomlink,rich_text",
                    "Name,name,text",
                    "Email,email,text",
                    "Type,type,text",
                    "Id,id,text",
                };

                ContentTypeElementModel func(string x)
                {
                    var splitString = x.Split(',');
                    return new ContentTypeElementModel(splitString[0], splitString[1], splitString[2]);
                }

                List<ContentTypeElementModel> userTypeElements = new List<ContentTypeElementModel>(
                        userElementValues
                            .Select(x => func(x)));


                ContentTypeModel conversationModel = new ContentTypeModel()
                {
                    ExternaId = "user",
                    Name = "User",
                    Codename = "user",
                    Elements = userTypeElements
                };

                var payload = JsonConvert.SerializeObject(conversationModel);

                httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + options.ApiKey);
                result = await httpClient.PostAsync("https://manage.kenticocloud.com/v2/projects/" + options.ProjectId + "/types", new StringContent(payload, Encoding.UTF8, "application/json"));
            }

           reply = await result.Content.ReadAsStringAsync();

            if (reply == null)
                return false;

            output = JsonConvert.DeserializeObject(reply);
            Guid userTypeGuid = Guid.Parse(output.GetValue("id").ToString());

            Guid[] array = new Guid[] { conversationTypeGuid, userTypeGuid };
            return true;
        }

        private async Task<HttpResponseMessage> PublishItemVariant(string itemId)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + options.ApiKey);
                var result = await httpClient.PutAsync("https://manage.kenticocloud.com/v2/projects/" + options.ProjectId + "/items/" + itemId + "/variants/00000000-0000-0000-0000-000000000000/publish", new StringContent(""));
                return result;
            }
        }

        internal async Task<HttpResponseMessage> UnpublishItemVariant(string itemId)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + options.ApiKey);
                var result = await httpClient.PutAsync("https://manage.kenticocloud.com/v2/projects/" + options.ProjectId + "/items/" + itemId + "/variants/00000000-0000-0000-0000-000000000000/unpublish", new StringContent(""));
                return result;
            }
        }

        private string CreateTagsString(Conversation conversation)
        {
            List<string> tagsList = new List<string> { };

            foreach (var tag in conversation.tags)
            {
                tagsList.Add(tag.name);
            }

            return string.Join(",", tagsList);
        }

        private string CreateSearchBody(Conversation conversation)
        {
            string result = "";

            result += Tools.RemoveParagraphWrapper(conversation.conversation_message.body) + " ";
            foreach (var part in conversation.conversation_parts)
            {
                if (part.body != null && part.author.type != "bot")
                {
                    result += Tools.RemoveParagraphWrapper(part.body) + " ";
                }
            }

            return Tools.SanitizeSearchText(result);
        }

        internal async Task CleanProject()
        {
            var allItems = await GetAllItems();
            var conversations = allItems.Where(x => x.Type.Id == conversationTypeGuid).ToList();
            var users = allItems.Where(x => x.Type.Id == userTypeGuid).ToList();

            try
            {
                await DeleteAllItems(users);
                await DeleteAllItems(conversations);
            }
            catch (Exception e)
            {
                if (e.Source != null)
                    logger.Error(e , "Something went wrong deleting items.");
                throw;
            }
        }

        private async Task<ContentItemModel> GetItem(ContentItemIdentifier itemIdentifier)
        {
            return await managementClient.GetContentItemAsync(itemIdentifier);
        }

        private async Task<ContentItemModel> GetItem(string externalId)
        {
            return await managementClient.GetContentItemAsync(ContentItemIdentifier.ByExternalId(externalId));
        }

        private async Task<List<ContentItemIdentifier>> GetAllItemIdentifiers()
        {
            List<ContentItemIdentifier> listIds = new List<ContentItemIdentifier>();
            ListingResponseModel<ContentItemModel> response = await managementClient.ListContentItemsAsync();

            while (true)
            {
                foreach (var item in response)
                {
                    listIds.Add(ContentItemIdentifier.ById(item.Id));
                }

                if (!response.HasNextPage())
                {
                    break;
                }
                response = await response.GetNextPage();
            }

            return listIds;
        }

        private async Task<List<ContentItemIdentifier>> GetAllUserItemIdentifiers()
        {
            List<ContentItemIdentifier> listIds = new List<ContentItemIdentifier>();
            ListingResponseModel<ContentItemModel> response = await managementClient.ListContentItemsAsync();

            // this is guid of Content Type "User" in the KC project
            ObjectIdentifier userContentType = new ObjectIdentifier() { Id = Guid.Parse("eaf7e4c9-0a4d-46dc-b787-ef02c9860592") };

            while (true)
            {
                foreach (var item in response)
                {
                    if (item.Type == userContentType)
                        listIds.Add(ContentItemIdentifier.ById(item.Id));
                }

                if (!response.HasNextPage())
                {
                    break;
                }
                response = await response.GetNextPage();
            }

            return listIds;
        }

        private async Task<List<ContentItemModel>> GetAllItems()
        {
            int count = 0;
            List<ContentItemModel> itemList = new List<ContentItemModel> { };

            logger.Debug("Request to get items from Kontent project..");
            ListingResponseModel<ContentItemModel> response = await managementClient.ListContentItemsAsync();
            logger.Debug("Got " + response.Count() + " items.");
            count += response.Count();

            while (true)
            {
                foreach (var item in response)
                {
                    itemList.Add(item);
                }

                if (!response.HasNextPage())
                {
                    logger.Debug("There are no more items to get.");
                    break;
                }

                logger.Debug("Getting next page.");
                response = await response.GetNextPage();
                count += response.Count();
                logger.Debug("Got " + response.Count() + " items. Total: " + count);
            }


            return itemList;
        }

        private async Task DeleteItem(ContentItemIdentifier contentItem)
        {
            await managementClient.DeleteContentItemAsync(contentItem);
        }

        private async Task DeleteAllItems(List<ContentItemModel> contentItems)
        {
            var allTasks = new List<Task>();
            var throttler = new SemaphoreSlim(initialCount: 2);
            foreach (var item in contentItems)
            {
                await throttler.WaitAsync();
                allTasks.Add(
                    Task.Run(async () =>
                    {
                        try
                        {
                            logger.Info("Deleting item:" + item.Id);
                            await managementClient.DeleteContentItemAsync(ContentItemIdentifier.ById(item.Id));
                        }
                        catch (KenticoCloud.ContentManagement.Exceptions.ContentManagementException e)
                        {
                            logger.Error(e, "Something went wrong deleting item.");
                        }
                        finally
                        {
                            throttler.Release();
                        }
                    }));
            }

            await Task.WhenAll(allTasks);
        }

        private async Task DeleteAllItems(List<ContentItemIdentifier> contentItems)
        {
            var allTasks = new List<Task>();
            var throttler = new SemaphoreSlim(initialCount: 10);
            int i = 10;
            foreach (var item in contentItems)
            {
                await throttler.WaitAsync();
                allTasks.Add(
                    Task.Run(async () =>
                    {
                        try
                        {
                            if (i<10)
                            {
                                i--;
                                await Task.Delay(100);
                            }
                            Console.WriteLine("Deleting item:" + item.Id);
                            await managementClient.DeleteContentItemAsync(item);
                        }
                        catch (KenticoCloud.ContentManagement.Exceptions.ContentManagementException e)
                        {
                            logger.Error(e, "Error deleting.");
                        }
                        finally
                        {
                            throttler.Release();
                        }
                    }));
            }

            await Task.WhenAll(allTasks);
        }

        private string CreateRichTextCompatibleConversationString(Conversation conversation)
        {
            string result = "";

            // poresit attachment
            result += "<p><em>message@";
            result += Tools.FromUnixTimestamp(conversation.created_at);
            result += "</em><strong> ";
            result += GetConversationUsernamePlaceholder(conversation.conversation_message.author.id, conversation.conversation_message.author.type);
            result += "</strong>:</p>";
            result += Tools.WrapInParagraphIfNeeded(Tools.SanitizeRichText(Tools.RemoveParagraphWrapper(conversation.conversation_message.body)));
            result += "<p></p>"; ;

            foreach (var part in conversation.conversation_parts)
            {
                switch (part.part_type)
                {
                    case "assignment":
                        if (part.body != null)
                        {
                            result += "<p><em>message@";
                            result += Tools.FromUnixTimestamp(part.created_at);
                            result += "</em><strong> ";
                            result += GetConversationUsernamePlaceholder(part.author.id, part.author.type);
                            result += "</strong>:</p>";
                            result += Tools.WrapInParagraphIfNeeded(Tools.SanitizeRichText(Tools.RemoveParagraphWrapper(part.body)));
                            result += "<p></p>"; ;
                            result += "<p><em>system_message@";
                            result += Tools.FromUnixTimestamp(part.created_at);
                            result += "</em>:</p><p>";
                            result += GetConversationUsernamePlaceholder(part.author.id, part.author.type);
                            result += " has assigned the conversation to " + GetConversationUsernamePlaceholder(part.assigned_to.id, part.assigned_to.type) + " ";
                            result += "</p>";
                            result += "<p></p>"; ;
                        }
                        else
                        {
                            result += "<p><em>system_message@";
                            result += Tools.FromUnixTimestamp(part.created_at);
                            result += "</em>:</p><p>";
                            result += GetConversationUsernamePlaceholder(part.author.id, part.author.type);
                            result += " has assigned the conversation to " + GetConversationUsernamePlaceholder(part.assigned_to.id, part.assigned_to.type) + " ";
                            result += "</p>";
                            result += "<p></p>"; ;
                        }
                        break;

                    case "away_mode_assignment":
                        result += "<p><em>system_message@";
                        result += Tools.FromUnixTimestamp(part.created_at);
                        result += "</em>:</p><p>";
                        result += "Unassigned because " + GetConversationUsernamePlaceholder(part.author.id, part.author.type) + " turned on away mode and reassignment.";
                        result += "</p>";
                        result += "<p></p>"; ;
                        break;

                    case "comment":
                        result += "<p><em>message@";
                        result += Tools.FromUnixTimestamp(part.created_at);
                        result += "</em><strong> ";
                        result += GetConversationUsernamePlaceholder(part.author.id, part.author.type);
                        result += "</strong>:</p>";
                        if (part.author.type == "bot" && part.body == null)
                        {
                            result += "<p><em>Internal bot notification, didn't figure out the way to retrieve it by API. (likely \"user left their email\")</em></p>";
                        }
                        else
                        {
                            result += Tools.WrapInParagraphIfNeeded(Tools.SanitizeRichText(Tools.RemoveParagraphWrapper(part.body)));
                        }
                        result += "<p></p>"; ;
                        break;

                    case "note":
                        result += "<p><em>note@";
                        result += Tools.FromUnixTimestamp(part.created_at);
                        result += "</em><strong> ";
                        result += GetConversationUsernamePlaceholder(part.author.id, part.author.type);
                        result += "</strong>:</p>";
                        result += Tools.WrapInParagraphIfNeeded(Tools.SanitizeRichText(Tools.RemoveParagraphWrapper(part.body)));
                        result += "<p></p>"; ;
                        break;

                    case "open":
                        result += "<p><em>message@";
                        result += Tools.FromUnixTimestamp(part.created_at);
                        result += "</em><strong> ";
                        result += GetConversationUsernamePlaceholder(part.author.id, part.author.type);
                        result += "</strong>:</p>";
                        result += Tools.WrapInParagraphIfNeeded(Tools.SanitizeRichText(Tools.RemoveParagraphWrapper(part.body)));
                        result += "<p></p>"; ;
                        result += "<p><em>system_message@";
                        result += Tools.FromUnixTimestamp(part.created_at);
                        result += "</em>:</p><p>";
                        result += GetConversationUsernamePlaceholder(part.author.id, part.author.type);
                        result += " has replied and reopened the conversation.";
                        result += "</p>";
                        result += "<p></p>"; ;
                        break;

                    case "note_and_reopen":
                        break;

                    case "close":
                        if (part.body != null)
                        {
                            result += "<p><em>message@";
                            result += Tools.FromUnixTimestamp(part.created_at);
                            result += "</em><strong> ";
                            result += GetConversationUsernamePlaceholder(part.author.id, part.author.type);
                            result += "</strong>:</p>";
                            result += Tools.WrapInParagraphIfNeeded(Tools.SanitizeRichText(Tools.RemoveParagraphWrapper(part.body)));
                            result += "<p></p>"; ;
                            result += "<p><em>system_message@";
                            result += Tools.FromUnixTimestamp(part.created_at);
                            result += "</em>:</p><p>";
                            result += GetConversationUsernamePlaceholder(part.author.id, part.author.type);
                            result += " has replied and closed the conversation.";
                            result += "</p>";
                            result += "<p></p>"; ;

                        }
                        else
                        {
                            result += "<p><em>system_message@";
                            result += Tools.FromUnixTimestamp(part.created_at);
                            result += "</em>:</p><p>";
                            result += GetConversationUsernamePlaceholder(part.author.id, part.author.type);
                            result += " has closed the conversation.";
                            result += "</p>";
                            result += "<p></p>"; ;

                        }
                        break;

                    case "conversation_rating_changed":
                        result += "<p><em>system_message@";
                        result += Tools.FromUnixTimestamp(part.created_at);
                        result += "</em>:</p><p>";
                        result += "Conversation rating changed.";
                        result += "</p>";
                        result += "<p></p>"; ;
                        break;

                    case "conversation_rating_remark_added":
                        result += "<p><em>system_message@";
                        result += Tools.FromUnixTimestamp(part.created_at);
                        result += "</em>:</p><p>";
                        result += "Conversation rating message added.";
                        result += "</p>";
                        result += "<p></p>"; ;
                        break;

                    case "participant_added":
                        break;
                    case "participant_removed":
                        break;

                    case "snoozed":
                        break;
                    case "unsnoozed":
                        break;
                    case "assign_and_unsnooze":
                        break;
                    case "timer_unsnooze":
                        break;

                    default:
                        break;

                }

            }


            return result;
        }

        private string GetConversationUsernamePlaceholder(string userId, string userType)
        {
            if (userType == "bot")
                return "bot";

            if (userType == "nobody_admin")
                return "unassigned";

            var user = currentConversationUsers.Where(x => x.Id == userId).FirstOrDefault();

            if (user == null)
                return "Non-existing user";

            if (user.Name != string.Empty)
                return user.Name;
            else if (user.Email != string.Empty)
                return user.Email;
            else return "lead";
        }

    }

    public class KontentFunctionsSettings
    {
        /// <summary>
        /// Project ID of Kontent project.
        /// </summary>
        public string ProjectId { get; set; }
        /// <summary>
        /// Content Management API key, obtainable from UI.
        /// </summary>
        public string CMApiKey { get; set; }
        /// <summary>
        /// If the project is empty, save time but not trying to gets stuff from KK
        /// </summary>
        public bool CleanProject { get; set; }
        /// <summary>
        /// Guid of Content Type "Conversation".
        /// </summary>
        public string ConversationTypeGuid { get; set; }
        /// <summary>
        /// Guid of Content Type "User".
        /// </summary>
        public string UserTypeGuid { get; set; }
        public string SecureApiKey { get; set; }
        public string BannedConversations { get; set; }
    }
    

}
