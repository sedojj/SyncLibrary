using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntercomSearchProjectCore.KontentModels
    {
    public class ContentTypeModel
    {
        [JsonProperty("external_id")]
        public string ExternaId { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("codename")]
        public string Codename { get; set; }
        [JsonProperty("elements")]
        public List<ContentTypeElementModel> Elements { get; set; }
    }

}
