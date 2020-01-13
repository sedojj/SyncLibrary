using KenticoCloud.ContentManagement.Models.Items;
using System;
using System.Collections.Generic;

namespace IntercomSearchProjectCore.KontentModels
{
    public class ConversationModel
    {
        public string ConversationId { get; set; }
        public string IntercomLink { get; set; }
        public List<ContentItemIdentifier> Author { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? LastUpdated { get; set; }
        public List<ContentItemIdentifier> Assignee { get; set; }
        public string Messages { get; set; }
        public string RatingValue { get; set; }
        public string RatingNote { get; set; }
        public string Tags { get; set; }
        public List<ContentItemIdentifier> Participants { get; set; }
        public string SearchBody { get; set; }
        public int MessageCount { get; set; }

    }
}
