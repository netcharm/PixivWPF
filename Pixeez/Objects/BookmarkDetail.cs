using System;
using System.Collections.Generic;
using System.Text;

namespace Pixeez.Objects
{

    public class BookmarkDetailRootobject
    {
        public BookmarkDetail bookmark_detail { get; set; }
    }

    public class BookmarkDetail
    {
        public bool is_bookmarked { get; set; }
        public BookmarkDetailTag[] tags { get; set; }
        public string restrict { get; set; }
    }

    public class BookmarkDetailTag:Tag
    {
        public bool? is_registered { get; set; }
    }

    public class BookmarkTagsRootobject: ListRootObject
    {
        public Bookmark_Tags[] bookmark_tags { get; set; }
    }

    public class Bookmark_Tags:Tag
    {
        public int? count { get; set; }
    }

}
