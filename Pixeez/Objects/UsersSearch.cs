using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Pixeez.Objects
{
    public class UserSearchResult
    {
        [JsonProperty("user")]
        public User User;

        [JsonProperty("illusts")]
        public List<IllustWork> Illusts;

        [JsonProperty("novels")]
        public List<IllustWork> Novels;

        [JsonProperty("is_muted")]
        public bool IsMuted;
    }

    public class UsersSearchResult
    {
        [JsonProperty("user_previews")]
        public List<UserSearchResult> Users;

        [JsonProperty("next_url")]
        public string next_url;
    }

    public class UsersSearchResultAlt
    {
        [JsonProperty("users")]
        public List<UserSearchResult> Users;

        [JsonProperty("next_url")]
        public string next_url;
    }
}
