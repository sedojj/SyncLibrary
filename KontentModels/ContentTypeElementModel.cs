using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntercomSearchProjectCore.KontentModels
{
    public class ContentTypeElementModel
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("codename")]
        public string Codename { get; set; }
        [JsonProperty("type")]
        public string Type { get; set; }

        public ContentTypeElementModel()
        { }

        public ContentTypeElementModel(string name, string codename, string type)
        {
            Name = name;
            Codename = codename;
            Type = type;
        }
    }
}
