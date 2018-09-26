using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pixeez.Objects
{
    public class TrendingTag
    {
        [JsonProperty("tag")]
        public string tag { get; set; }

        [JsonProperty("illust")]
        public IllustWork illust { get; set; }
    }

    public class TrendingTags
    {
        [JsonProperty("trend_tags")]
        public TrendingTag[] tags { get; set; }
    }

}
