//PixivUniversal
//Copyright(C) 2017 Pixeez Plus Project

//This program is free software; you can redistribute it and/or
//modify it under the terms of the GNU General Public License
//as published by the Free Software Foundation; version 2
//of the License.

//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
//GNU General Public License for more details.

//You should have received a copy of the GNU General Public License
//along with this program; if not, write to the Free Software
//Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
namespace Pixeez.Objects
{
    public class ListRootObject
    {
        public string next_url { get; set; }
    }

    public class Illusts: ListRootObject
    {
        public IllustWork[] illusts { get; set; }
    }

    public class RecommendedRootobject: Illusts
    {
        public object[] ranking_illusts { get; set; }
    }

    public class IllustWork : Work
    {
        [JsonProperty("user")]
        public NewUser user { get; set; }
        [JsonProperty("restrict")]
        public int Restrict { get; set; }
        public Tag[] tags { get; set; }
        [JsonProperty("sanity_level")]
        public string SanityLevel { get; set; }
        public MetaSinglePage meta_single_page { get; set; }
        public MetaPages[] meta_pages { get; set; }
        public int total_view { get; set; }
        public int total_bookmarks { get; set; }
        public bool? is_bookmarked { get; set; }
        [JsonProperty("visible")]
        public bool Visible { get; set; }
        public bool is_muted { get; set; }
        [JsonProperty("create_date")]
        public DateTime CreatedTime
        {
            get; set;
        }
        public override IList<string> Tags
        {
            get
            {
                List<string> tg = new List<string>();
                foreach (var one in tags)
                {
                    tg.Add(one.Name);
                }
                return tg;
            }
        }

        public override UserBase User => user;

        public override bool IsBookMarked()
        {
            return is_bookmarked ?? true;
        }

        public override DateTime GetCreatedDate()
        {
            return CreatedTime;
        }

        public override void SetBookMarkedValue(bool value)
        {
            is_bookmarked = value;
        }
    }

    public class MetaSinglePage
    {
        [JsonProperty("original_image_url")]
        public string OriginalImageUrl { get; set; }
    }

    public class Tag
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class MetaPages
    {
        [JsonProperty("image_urls")]
        public ImageUrls ImageUrls { get; set; }
    }
}


