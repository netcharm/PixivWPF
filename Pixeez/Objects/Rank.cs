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
    public class Rank
    {

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("mode")]
        public string Mode { get; set; }

        [JsonProperty("date")]
        public DateTimeOffset Date { get; set; }

        [JsonProperty("works")]
        public IList<RankWork> Works { get; set; }
    }

    public class RankWork
    {

        [JsonProperty("rank")]
        public int? Rank { get; set; }

        [JsonProperty("previous_rank")]
        public int? PreviousRank { get; set; }

        [JsonProperty("work")]
        public NormalWork Work { get; set; }
    }
}
