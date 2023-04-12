﻿//PixivUniversal
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
    public class ImageUrls
    {
        [JsonProperty("px_128x128")]
        public string Px128x128 { get; set; }

        [JsonProperty("small")]
        public string Small { get; set; }

        [JsonProperty("medium")]
        public string Medium { get; set; }

        [JsonProperty("large")]
        public string Large { get; set; }

        [JsonProperty("px_480mw")]
        public string Px480mw { get; set; }
        [JsonProperty("square_medium")]
        public string SquareMedium { get; set; }
        [JsonProperty("original")]
        public string Original { get; set; }
    }

    public class FavoritedCount
    {
        [JsonProperty("public")]
        public int? Public { get; set; }

        [JsonProperty("private")]
        public int? Private { get; set; }
    }

    public class WorkStats
    {
        [JsonProperty("scored_count")]
        public int? ScoredCount { get; set; }

        [JsonProperty("score")]
        public int? Score { get; set; }

        [JsonProperty("views_count")]
        public int? ViewsCount { get; set; }

        [JsonProperty("favorited_count")]
        public FavoritedCount FavoritedCount { get; set; }

        [JsonProperty("commented_count")]
        public int? CommentedCount { get; set; }
    }

    public class Page
    {
        [JsonProperty("image_urls")]
        public ImageUrls ImageUrls { get; set; }
    }

    public class Metadata
    {
        [JsonProperty("pages")]
        public IList<Page> Pages { get; set; }
    }

    public class UgoiraFrame
    {
        [JsonProperty("file")]
        public string File { get; set; }
        [JsonProperty("delay")]
        public int Delay { get; set; }
    }

    public class UgoiraInfo
    {
        [JsonProperty("src")]
        public string Src { get; set; }

        [JsonProperty("originalSrc")]
        public string OriginalSrc { get; set; }

        [JsonProperty("mime_type")]
        public string MimeType { get; set; }

        [JsonProperty("zip_urls")]
        public ImageUrls Urls { get; set; }

        [JsonProperty("frames")]
        public IList<UgoiraFrame> Frames { get; set; }
    }

    public class UgoiraMetadata
    {
        [JsonProperty("ugoira_metadata")]
        public UgoiraInfo Metadata { get; set; }
    }

    public class UgoiraAjaxMetadata
    {
        [JsonProperty("error")]
        public bool Error { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("body")]
        public UgoiraInfo Meta { get; set; }
    }

    public class NormalWork : Work
    {
        [JsonProperty("tags")]
        public override IList<string> Tags { get; set; }
        [JsonProperty("created_time")]
        public DateTimeOffset CreatedTime { get; set; }
        [JsonProperty("favorite_id")]
        public long? FavoriteId { get; set; }

        [JsonProperty("user")]
        public User user { get; set; }
        public override UserBase User => user;
        public bool BookMarked
        {
            get
            {
                return IsBookMarked();
            }
        }

        public override bool IsBookMarked()
        {
            if (FavoriteId == 0)
                return false;
            return true;
        }

        public override DateTime GetCreatedDate()
        {
            return CreatedTime.LocalDateTime;
        }

        public override void SetBookMarkedValue(bool value)
        {
            FavoriteId = value ? -1 : 0;
        }
    }

    public abstract class Work : IWorkExtended
    {

        [JsonProperty("id")]
        public long? Id { get; set; }
        public abstract UserBase User { get; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("caption")]
        public string Caption { get; set; }

        public virtual IList<string> Tags { get; set; }

        [JsonProperty("tools")]
        public IList<string> Tools { get; set; }

        [JsonProperty("image_urls")]
        public ImageUrls ImageUrls { get; set; }

        [JsonProperty("width")]
        public int? Width { get; set; }

        [JsonProperty("height")]
        public int? Height { get; set; }

        [JsonProperty("stats")]
        public WorkStats Stats { get; set; }

        [JsonProperty("publicity")]
        public int? Publicity { get; set; }

        [JsonProperty("age_limit")]
        public string AgeLimit { get; set; }

        [JsonProperty("reuploaded_time")]
        public string ReuploadedTime { get; set; }

        [JsonProperty("is_manga")]
        public bool? IsManga { get; set; }

        [JsonIgnore]
        public bool IsUgoira { get { return (!string.IsNullOrEmpty(Type) && Type.Equals("ugoira", StringComparison.CurrentCultureIgnoreCase) ? true : false); } }

        [JsonIgnore]
        public UgoiraInfo UgoiraMeta { get; set; } = null;

        [JsonProperty("is_liked")]
        public bool? IsLiked { get; set; }

        [JsonProperty("page_count")]
        public int? PageCount { get; set; }

        [JsonProperty("book_style")]
        public string BookStyle { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("metadata")]
        public Metadata Metadata { get; set; }

        //[JsonProperty("aiType")]
        //public int AIType { get; set; } = 0;

        [JsonProperty("illust_ai_type")]
        public int AIType { get; set; } = 0;

        [JsonProperty("content_type")]
        public string ContentType { get; set; }
        public abstract void SetBookMarkedValue(bool value);
        public abstract bool IsBookMarked();
        public abstract DateTime GetCreatedDate();
    }
}
