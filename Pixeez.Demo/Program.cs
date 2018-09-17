using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pixeez.Demo
{
    class Program
    {
        static void Main(string[] args)
        {
            Task.Run(async () => await Program.PixivDemo()).Wait();
        }

        static async Task PixivDemo()
        {
            // Create Tokens
            var tokens = await Pixeez.Auth.AuthorizeAsync("username", "password");

            /*var work = await tokens.GetWorksAsync(51796422);
            var user = await tokens.GetUsersAsync(11972);
            var myfeeds = await tokens.GetMyFeedsAsync(true);
            var usersWorks = await tokens.GetUsersWorksAsync(11972);
            var usersFavoriteWorks = await tokens.GetUsersFavoriteWorksAsync(11972);
            var ranking = await tokens.GetRankingAllAsync();
            var search = await tokens.SearchWorksAsync("フランドール・スカーレット", mode: "tag");*/

            var results = new List<Pixeez.Objects.Work>();

            int page = 1, imageCount = int.MaxValue;
            while (true)
            {
                var search = await tokens.SearchWorksAsync("フランドール・スカーレット", page: page, perPage: PerPage, mode: "exact_tag");
                imageCount = search.Pagination.Total.Value;
                foreach (var s in search)
                    results.Add(s);

                if (search.Pagination.Next.HasValue)
                    page = search.Pagination.Next.Value;
                else
                    break;

                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(10));
            }
            if (imageCount > 20000)
            {
                page = 1;
                while (true)
                {
                    bool f = false;
                    var search = await tokens.SearchWorksAsync("フランドール・スカーレット", page: page, perPage: PerPage, mode: "exact_tag", order: "asc");
                    imageCount = search.Pagination.Total.Value;
                    foreach (var s in search)
                    {
                        if (results.Any(x => x.Id == s.Id))
                        {
                            f = true;
                            break;
                        }

                        results.Add(s);
                    }

                    if (f)
                        break;

                    if (search.Pagination.Next.HasValue)
                        page = search.Pagination.Next.Value;
                    else
                        break;

                    System.Threading.Thread.Sleep(TimeSpan.FromSeconds(10));
                }
            }

            var flanChan = results.Where(x => x.Stats?.FavoritedCount?.Private + x.Stats?.FavoritedCount?.Public > 500)
                .OrderByDescending(x => x.Stats?.FavoritedCount?.Private + x.Stats?.FavoritedCount?.Public)
                .Select(x => new { x.Caption, x.CreatedTime, x.Title, UserName = x.User.Name, FavoritedCount = x.Stats?.FavoritedCount?.Private + x.Stats?.FavoritedCount?.Public, ImageUrl = x.ImageUrls.Large, Url = "http://www.pixiv.net/member_illust.php?mode=medium&illust_id=" + x.Id });

            using (StreamWriter file = new StreamWriter(@"FlanchanRanking.txt"))
            {
                file.WriteLine("計" + flanChan.Count().ToString() + "件");
                file.WriteLine();
                file.WriteLine();

                foreach (var item in flanChan)
                {
                    file.WriteLine(item.Title + " : " + item.UserName);
                    file.WriteLine(item.Caption?.Replace("\r\n", ""));
                    file.WriteLine(item.Url);
                    file.WriteLine(item.ImageUrl);
                    file.WriteLine(item.CreatedTime + " , " + item.FavoritedCount.ToString() + " favorites");
                    file.WriteLine();
                }
            }
        }
    }
}
