using IntercomSearchProjectCore.AlgoliaModels;
using IntercomSearchProjectCore.KontentModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace IntercomSearchProjectCore.AlgoliaModels
{
    public class SearchConversation
    {
        private const int MAXOBJECTSIZE = 9000;
        public string ObjectID { get; set; }
        public string IntercomLink { get; set; }
        public List<SearchUser> Assignee { get; set; }
        public List<SearchUser> Participants { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long DateTimestamp { get; set; }
        public string SearchText { get; set; }
        public int MessageCount { get; set; }
        public string Tags { get; set; }
        public int RatingValue { get; set; }
        public string RatingRemark { get; set; }
        private bool IsTooBig { get; }
        public string ConversationSplit { get; set; }

        public SearchConversation()
        {

        }

        public SearchConversation(SearchConversation original)
        {
            ObjectID = original.ObjectID;
            IntercomLink = original.IntercomLink;
            Assignee = original.Assignee;
            Participants = original.Participants;
            CreatedAt = original.CreatedAt;
            DateTimestamp = original.DateTimestamp;
            SearchText = original.SearchText;
            MessageCount = original.MessageCount;
            Tags = original.Tags;
            RatingValue = original.RatingValue;
            RatingRemark = original.RatingRemark;
            IsTooBig = original.IsTooBig;
            ConversationSplit = original.ConversationSplit;
        }

        public SearchConversation(ConversationModel conversation, SearchUser assignee, List<SearchUser> participants)
        {
            ObjectID = conversation.ConversationId;
            IntercomLink = Regex.Match(conversation.IntercomLink, "href\\s*=\\s*\"(?<url>.*?)\"").Groups["url"].Value;
            Assignee = new List<SearchUser>() { assignee };
            Participants = participants;
            CreatedAt = conversation.CreatedAt;
            DateTimestamp = ((DateTimeOffset)conversation.CreatedAt).ToUnixTimeSeconds();
            SearchText = conversation.SearchBody;
            MessageCount = conversation.MessageCount;
            Tags = conversation.Tags;
            RatingValue = string.IsNullOrEmpty(conversation.RatingValue) ? 0 : int.Parse(conversation.RatingValue);
            RatingRemark = conversation.RatingNote;
            IsTooBig = GetSearchConversationSize() > MAXOBJECTSIZE;
            if (IsTooBig)
            {
                ConversationSplit = "ConversationPart";
            }
        }

        private int GetSearchConversationSize()
        {
            int length = 0;
            PropertyInfo[] info = this.GetType().GetProperties();
            foreach (PropertyInfo property in info)
            {

                if (property.Name == "ObjectID")
                {
                    length += 2;
                    continue;
                }

                length += 2;
                length += Encoding.UTF8.GetByteCount(property.Name);

                if (property.PropertyType == typeof(int))
                {
                    length += 1;
                }

                if (property.PropertyType == typeof(long))
                {
                    length += 1;
                }

                if (property.PropertyType == typeof(string))
                {
                    string value = property.GetValue(this, null) as string;
                    if (!String.IsNullOrEmpty(value))
                        length += Encoding.UTF8.GetByteCount(value);
                }

                if (property.PropertyType == typeof(List<SearchUser>))
                {
                    length += 1;
                    List<SearchUser> value = property.GetValue(this, null) as List<SearchUser>;
                    foreach (var user in value)
                    {
                        length += 2;
                        length += Encoding.UTF8.GetByteCount("Email") + Encoding.UTF8.GetByteCount("Name");
                        if (!String.IsNullOrEmpty(user.Email))
                            length += Encoding.UTF8.GetByteCount(user.Email);
                        if (!String.IsNullOrEmpty(user.Name))
                            length += Encoding.UTF8.GetByteCount(user.Name);
                    }
                }

                if (property.PropertyType == typeof(DateTime?))
                {
                    length += 20;
                }
            }

            // possibly a better way to calculate size, even though it is not correct
            var test = Newtonsoft.Json.JsonConvert.SerializeObject(this);

            return length;

        }

        public List<SearchConversation> SplitConversation()
        {
            int splitSize = (CalculateMaxSearchTextSize() / 100) * 85;
            List<SearchConversation> searchConversations = new List<SearchConversation> { };
            if (IsTooBig)
            {

                var partialSearchTexts = SplitToChunks(SearchText, splitSize);

                int enumerator = 1;
                foreach (var partitalText in partialSearchTexts)
                {
                    SearchConversation partialSearchconversation = new SearchConversation(this);

                    if (enumerator > 1)
                        partialSearchconversation.ObjectID = ObjectID + "-" + enumerator.ToString();

                    enumerator++;
                    partialSearchconversation.SearchText = partitalText;
                    searchConversations.Add(partialSearchconversation);
                }
            }
            else
            {
                searchConversations.Add(this);
            }
            return searchConversations;
        }

        static IEnumerable<string> SplitToChunks(string str, int maxChunkSize)
        {
            for (int i = 0; i < str.Length; i += maxChunkSize)
            {
                yield return str.Substring(i, Math.Min(maxChunkSize, str.Length - i));
            }
        }

        private int CalculateMaxSearchTextSize()
        {
            return MAXOBJECTSIZE - (GetSearchConversationSize() - Encoding.UTF8.GetByteCount(SearchText));
        }

    }
}
