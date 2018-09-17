using System;
using System.Collections.Generic;
using System.Text;

namespace Pixeez.Objects
{
    public class Comment
    {
        public int id { get; set; }
        public string comment { get; set; }
        public DateTime date { get; set; }
        public NewUser user { get; set; }
        public Comment parent_comment { get; set; }
    }

    public class IllustCommentObject: ListRootObject
    {
        public int total_comments { get; set; }
        public Comment[] comments { get; set; }
    }
}
