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
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pixeez.Objects
{
    public class RefWork
    {

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("width")]
        public int? Width { get; set; }

        [JsonProperty("height")]
        public int? Height { get; set; }

        [JsonProperty("image_urls")]
        public ImageUrls ImageUrls { get; set; }

        [JsonProperty("comment")]
        public string Comment { get; set; }

        [JsonProperty("user")]
        public User User { get; set; }
    }

    public class RefBookmark
    {

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("tags")]
        public IList<string> Tags { get; set; }
    }

    public class Feed
    {

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("user")]
        public User User { get; set; }

        [JsonProperty("post_time")]
        public string PostTime { get; set; }

        [JsonProperty("post_date")]
        public DateTimeOffset PostDate { get; set; }

        [JsonProperty("ref_work")]
        public RefWork RefWork { get; set; }

        [JsonProperty("ref_bookmark")]
        public RefBookmark RefBookmark { get; set; }
    }

}
