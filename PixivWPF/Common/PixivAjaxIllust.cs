using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Windows;

namespace PixivWPF.Common
{
    public class AjaxMetaPageImageUrls
    {
        [JsonProperty("thumb_mini")]
        public string Thumbnail { get; set; }

        [JsonProperty("small")]
        public string Small { get; set; }

        [JsonProperty(nameof(Small))]
        public string Medium { get; set; }

        [JsonProperty("regular")]
        public string Large { get; set; }

        [JsonProperty("original")]
        public string Original { get; set; }
    }

    public class AjaxMetaPage
    {
        [JsonProperty("urls")]
        public AjaxMetaPageImageUrls ImageUrls { get; set; }

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }
    }

    public class AjaxMetaPages
    {
        [JsonProperty("error")]
        public bool Error { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("body")]
        public List<AjaxMetaPage> Pages { get; set; }
    }

    public class AjaxTagTranslation
    {
        [JsonProperty("en")]
        public string En { get; set; }
    }

    public class AjaxTag
    {
        [JsonProperty("userId")]
        public string UserId { get; set; }

        [JsonProperty("userName")]
        public string UserName { get; set; }

        [JsonProperty("locked")]
        public bool IsLocked { get; set; }

        [JsonProperty("deletable")]
        public bool Deletable { get; set; }

        [JsonProperty("tag")]
        public string Tag { get; set; }

        [JsonProperty("translation")]
        public AjaxTagTranslation Translation { get; set; }

        public string TranslationEn { get { return (Translation is AjaxTagTranslation ? Translation.En : string.Empty); } }
    }

    public class AjaxTags
    {
        [JsonProperty("authorId")]
        public string AuthorId { get; set; }

        [JsonProperty("isLocked")]
        public bool IsLocked { get; set; }

        [JsonProperty("writable")]
        public bool Writeable { get; set; }

        [JsonProperty("tags")]
        public List<AjaxTag> Tags { get; set; }

        //[JsonProperty(nameof(Tags))]
        public List<string> SimpleTags { get; set; }
    }

    public class AjaxIllustImageUrls
    {
        [JsonProperty("mini")]
        public string Mini { get; set; }

        [JsonProperty(nameof(Mini))]
        public string Small { get; set; }

        [JsonProperty("thumb")]
        public string Thumbnail { get; set; }

        [JsonProperty("small")]
        public string Medium { get; set; }

        [JsonProperty("regular")]
        public string Large { get; set; }

        [JsonProperty("original")]
        public string Original { get; set; }
    }

    public class AjaxBookmarkData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("private")]
        public bool IsPrivate { get; set; }
    }

    public class AjaxIllustWork
    {
        [JsonProperty("illustId")]
        public string IllustId { get; set; }
        [JsonProperty("Id")]
        public string IdAlt { get; set; }

        [JsonProperty("illustTitle")]
        public string Title { get; set; }
        [JsonProperty("title")]
        public string TitleAlt { get; set; }

        [JsonProperty("illustComment")]
        public string Caption { get; set; }
        [JsonProperty("description")]
        public string CaptionAlt { get; set; }

        [JsonProperty("illustType")]
        public int IllustType { get; set; }

        [JsonProperty("createDate")]
        public DateTime CreateTime { get; set; }

        [JsonProperty("uploadDate")]
        public DateTime UploadTime { get; set; }

        [JsonProperty("restrict")]
        public int Restrict { get; set; }

        [JsonProperty("xRestrict")]
        public int xRestrict { get; set; }

        [JsonProperty("sl")]
        public int SanityLevel { get; set; }

        [JsonProperty("urls")]
        public AjaxIllustImageUrls ImageUrls { get; set; }
        
        [JsonProperty("tags")]
        protected internal AjaxTags _Tags_ { get; set; }

        public IList<string> Tags
        {
            get
            {
                List<string> tg = new List<string>();
                if (_Tags_.SimpleTags != null)
                {
                    foreach (var tag in _Tags_.SimpleTags)
                    {
                        tg.Add(tag);
                    }
                }
                else if (_Tags_.Tags is IEnumerable<AjaxTag>)
                {
                    foreach (var tag in _Tags_.Tags)
                    {
                        tg.Add(tag.Tag);
                    }
                }
                return tg;
            }
        }

        public IList<Pixeez.Objects.MoreTag> MoreTags
        {
            get
            {
                List<Pixeez.Objects.MoreTag> tg = new List<Pixeez.Objects.MoreTag>();
                if (_Tags_.Tags is IEnumerable<AjaxTag>)
                {
                    foreach (var tag in _Tags_.Tags)
                    {
                        tg.Add(new Pixeez.Objects.MoreTag() { Original = tag.Tag, Translated = string.IsNullOrEmpty(tag.TranslationEn) ? string.Empty : tag.TranslationEn });
                    }
                }
                return tg;
            }
        }

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("pageCount")]
        public int PageCount { get; set; }

        [JsonProperty("bookmarkCount")]
        public int BookmarkCount { get; set; }
        [JsonProperty("likeCount")]
        public int LikeCount { get; set; }
        [JsonProperty("commentCount")]
        public int CommentCount { get; set; }
        [JsonProperty("viewCount")]
        public int ViewCount { get; set; }
        [JsonProperty("responseCount")]
        public int ResponseCount { get; set; }

        [JsonProperty("bookmarkData")]
        public AjaxBookmarkData BookmarkData { get; set; }

        [JsonProperty("userId")]
        public string UserId { get; set; }

        [JsonProperty("userName")]
        public string UserName { get; set; }

        [JsonProperty("userAccount")]
        public string UserAccount { get; set; }

        [JsonProperty("userIllusts")]
        public Dictionary<string, AjaxIllustSimpleWork> UserIllusts { get; set; }
    }

    public class AjaxIllustSimpleWork : AjaxIllustWork
    {
        [JsonProperty("tags")]
        protected internal new List<string> Tags { get; set; }
    }

    public class AjaxIllust
    {
        [JsonProperty("error")]
        public bool Error { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("body")]
        public AjaxIllustWork Illust { get; set; }
    }

    static class PixivAjaxHelper
    {
        #region Ugoira Helper
        public static async Task<Pixeez.Objects.UgoiraInfo> GetUgoiraMeta(this Pixeez.Objects.Work Illust, bool ajax = false)
        {
            Pixeez.Objects.UgoiraInfo info = null;
            long id = 0;
            try
            {
                if (Illust.IsUgoira)
                {
                    id = Illust.Id.Value;
                    if (Illust.UgoiraMeta is Pixeez.Objects.UgoiraInfo)
                        info = Illust.UgoiraMeta;
                    else
                    {
                        if (ajax)
                        {
                            info = await GetUgoiraMetaInfo(id);
                        }
                        else
                        {
                            var tokens = await CommonHelper.ShowLogin();
                            var ugoira_meta = await tokens.GetUgoiraMetadata(id);
                            info = ugoira_meta is Pixeez.Objects.UgoiraMetadata ? ugoira_meta.Metadata : null;
                        }
                        if (info is Pixeez.Objects.UgoiraInfo) Illust.UgoiraMeta = info;
                    }
                }
            }
            catch (Exception ex) { ex.ERROR($"GetUgoiraMeta_{id}"); }
            return (info);
        }

        public static async Task<Pixeez.Objects.UgoiraInfo> GetUgoiraMeta(this PixivItem item, bool ajax = false)
        {
            if (ajax)
                return (await GetUgoiraMetaInfo(item));
            else
                return (await GetUgoiraMetaInfo(item.Illust.Id ?? 0, tokens: null));
        }

        public static async Task<Pixeez.Objects.UgoiraInfo> GetUgoiraMetaInfo(this long id, Pixeez.Tokens tokens = null)
        {
            Pixeez.Objects.UgoiraInfo result = null;
            try
            {
                if (tokens == null) tokens = await CommonHelper.ShowLogin();
                var meta = await tokens.GetUgoiraMetadata(id);
                result = meta.Metadata;
                if (string.IsNullOrEmpty(result.Src) && !string.IsNullOrEmpty(result.Urls.Medium))
                    result.Src = result.Urls.Medium;
                if (string.IsNullOrEmpty(result.OriginalSrc) && !string.IsNullOrEmpty(result.Src))
                    result.OriginalSrc = result.Src.Replace("600x600", "1920x1080");
            }
            catch (Exception ex) { ex.ERROR($"GetUgoiraMetaInfo_{id}"); }
            return (result);
        }

        public static async Task<Pixeez.Objects.UgoiraInfo> GetUgoiraMetaInfo(this long id)
        {
            Pixeez.Objects.UgoiraInfo result = null;
            if (id > 0)
            {
                try
                {
                    var url = $"https://www.pixiv.net/ajax/illust/{id}/ugoira_meta";
                    var json_text = await Application.Current.GetRemoteJsonAsync(url);
                    if (!string.IsNullOrEmpty(json_text))
                    {
                        var data = JsonConvert.DeserializeObject<Pixeez.Objects.UgoiraAjaxMetadata>(json_text);
                        if (data is Pixeez.Objects.UgoiraAjaxMetadata) result = data.Meta;
                    }
                    else result = await GetUgoiraMetaInfo(id, tokens: null);
                }
                catch (Exception ex) { ex.ERROR($"GetUgoiraMetaInfo_{id}"); }
            }
            return (result);
        }

        public static async Task<Pixeez.Objects.UgoiraInfo> GetUgoiraMetaInfo(this Pixeez.Objects.Work work)
        {
            Pixeez.Objects.UgoiraInfo result = null;
            if (work is Pixeez.Objects.Work)
            {
                if (work.UgoiraMeta is Pixeez.Objects.UgoiraInfo)
                    result = work.UgoiraMeta;
                else
                {
                    result = await GetUgoiraMetaInfo(work.Id ?? 0);
                    work.UgoiraMeta = result;
                }
            }
            return (result);
        }

        public static async Task<Pixeez.Objects.UgoiraInfo> GetUgoiraMetaInfo(this PixivItem item)
        {
            Pixeez.Objects.UgoiraInfo result = null;
            long id = 0;
            if (item.IsWork() && long.TryParse(item.ID, out id))
            {
                if (item.Ugoira is Pixeez.Objects.UgoiraInfo)
                    result = item.Ugoira;
                else if (item.Illust.UgoiraMeta is Pixeez.Objects.UgoiraInfo)
                {
                    result = item.Illust.UgoiraMeta;
                    item.Ugoira = result;
                }
                else
                {
                    result = await GetUgoiraMetaInfo(id);
                    item.Illust.UgoiraMeta = result;
                    item.Ugoira = result;
                }
            }
            return (result);
        }
        #endregion

        #region Illust and MetaPage Helper
        public static string GetAjaxMetaPageUrl(this string id)
        {
            if (string.IsNullOrEmpty(id)) return (string.Empty);
            else return ($"https://www.pixiv.net/ajax/illust/{id}/pages");
        }

        public static string GetAjaxMetaPageUrl(this long id)
        {
            if (id == 0) return (string.Empty);
            else return (GetAjaxMetaPageUrl(id.ToString()));
        }

        public static string GetAjaxMetaPageUrl(this long? id)
        {
            return (GetAjaxMetaPageUrl((id ?? 0).ToString()));
        }

        public static string GetAjaxIllustUrl(this string id)
        {
            if (string.IsNullOrEmpty(id)) return (string.Empty);
            else return ($"https://www.pixiv.net/ajax/illust/{id}");
        }

        public static string GetAjaxIllustUrl(this long id)
        {
            if (id == 0) return (string.Empty);
            else return (GetAjaxIllustUrl(id.ToString()));
        }

        public static string GetAjaxIllustUrl(this long? id)
        {
            return (GetAjaxIllustUrl((id ?? 0).ToString()));
        }

        public static async Task<List<Pixeez.Objects.Page>> GetMetaPages(this string url, Pixeez.Tokens tokens = null)
        {
            List<Pixeez.Objects.Page> result = null;

            var pages_json_text = await Application.Current.GetRemoteJsonAsync(url);
            if (!string.IsNullOrEmpty(pages_json_text))
            {
                var pages = JToken.Parse(pages_json_text).ToObject<AjaxMetaPages>();

                if (!pages.Error)
                {
                    if (pages.Pages.Count > 0) result = new List<Pixeez.Objects.Page>();
                    foreach (var page in pages.Pages)
                    {
                        var p = new Pixeez.Objects.Page()
                        {
                            ImageUrls = new Pixeez.Objects.ImageUrls()
                            {
                                Px128x128 = page.ImageUrls.Thumbnail,
                                SquareMedium = page.ImageUrls.Thumbnail,
                                Small = page.ImageUrls.Small,
                                Medium = page.ImageUrls.Medium,
                                Px480mw = page.ImageUrls.Medium,
                                Large = page.ImageUrls.Large,
                                Original = page.ImageUrls.Original,
                            }
                        };
                        result.Add(p);
                    }
                }
            }
            return (result);
        }

        public static async Task<List<Pixeez.Objects.Page>> GetMetaPages(this Pixeez.Objects.NormalWork work, Pixeez.Tokens tokens = null)
        {
            if (work is Pixeez.Objects.Work)
            {
                return (await GetMetaPages(GetAjaxMetaPageUrl(work.Id), tokens));
            }
            else return (null);
        }

        public static async Task<List<Pixeez.Objects.MetaPages>> GetMetaPages(this Pixeez.Objects.Work work, Pixeez.Tokens tokens = null)
        {
            List<Pixeez.Objects.MetaPages> result = null;
            if (work is Pixeez.Objects.Work)
            {
                var pages_url = GetAjaxMetaPageUrl(work.Id);
                List<Pixeez.Objects.Page> pages = work.PageCount > 1 ? await GetMetaPages(pages_url, tokens) : null;
                result = pages is List<Pixeez.Objects.Page> ? pages.Select(p => new Pixeez.Objects.MetaPages() { ImageUrls = p.ImageUrls }).ToList() : null;
            }
            return (result);
        }

        public static async Task<Pixeez.Objects.Metadata> GetMetaData(this string url, Pixeez.Tokens tokens = null)
        {
            var pages = await GetMetaPages(url, tokens);
            if (pages is List<Pixeez.Objects.Page> && pages.Count > 0)
                return (new Pixeez.Objects.Metadata() { Pages = pages });
            else 
                return (null);
        }

        public static async Task<Pixeez.Objects.Metadata> GetMetaData(this Pixeez.Objects.Work work, Pixeez.Tokens tokens = null)
        {
            if (work is Pixeez.Objects.Work)
            {
                return (await GetMetaData(GetAjaxMetaPageUrl(work.Id), tokens));
            }
            else return (null);
        }

        public static async Task<List<Pixeez.Objects.Work>> SearchIllustById(this long id, Pixeez.Tokens tokens = null, bool fuzzy = false)
        {
            List<Pixeez.Objects.Work> result = null;

            if (tokens == null) tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return (result);

            var url =GetAjaxIllustUrl(id);
            var json_text = await Application.Current.GetRemoteJsonAsync(url);
            if (!string.IsNullOrEmpty(json_text))
            {
                try
                {
                    var work = JToken.Parse(json_text).ToObject<AjaxIllust>();
                    if (!work.Error)
                    {
                        var illust = work.Illust;

                        #region Get/Set user
                        var userbase = await illust.UserId.GetUser();
                        Pixeez.Objects.NewUser new_user = userbase is Pixeez.Objects.NewUser ? userbase as Pixeez.Objects.NewUser : null;
                        Pixeez.Objects.User user = userbase is Pixeez.Objects.User ? user = userbase as Pixeez.Objects.User : null;
                        if (userbase != null)
                        {
                            var avatar = userbase.GetAvatarUrl();
                            if (new_user == null)
                            {
                                new_user = new Pixeez.Objects.NewUser()
                                {
                                    Id = userbase.Id,
                                    Account = userbase.Account,
                                    Name = userbase.Name,
                                    Email = userbase.Email,
                                    is_followed = userbase.is_followed,
                                    profile_image_urls = new Pixeez.Objects.ImageUrls() { Small = avatar, Medium = avatar },
                                };
                            }
                            if (user == null)
                            {
                                user = new Pixeez.Objects.User()
                                {
                                    Id = userbase.Id,
                                    Account = userbase.Account,
                                    Name = userbase.Name,
                                    Email = userbase.Email,
                                    is_followed = userbase.is_followed,
                                    ProfileImageUrls = new Pixeez.Objects.ProfileImageUrls() { medium = avatar },
                                };
                            }
                        }
                        #endregion

                        #region Set Image Urls
                        var image_urls = new Pixeez.Objects.ImageUrls();
                        if (illust.ImageUrls is AjaxIllustImageUrls)
                        {
                            image_urls.Small = illust.ImageUrls.Mini;
                            image_urls.Px128x128 = illust.ImageUrls.Thumbnail;
                            image_urls.SquareMedium = illust.ImageUrls.Thumbnail;
                            image_urls.Medium = illust.ImageUrls.Medium;
                            image_urls.Px480mw = illust.ImageUrls.Medium;
                            image_urls.Large = illust.ImageUrls.Large;
                            image_urls.Original = illust.ImageUrls.Original;
                        }
                        #endregion

                        #region Get MetaPages
                        var pages_url = GetAjaxMetaPageUrl(id);
                        List<Pixeez.Objects.Page> pages = illust.PageCount > 1 ? await GetMetaPages(pages_url, tokens) : null;
                        var meta_pages = pages is List<Pixeez.Objects.Page> ? pages.Select(p => new Pixeez.Objects.MetaPages() { ImageUrls = p.ImageUrls }) : null;
                        #endregion

                        #region Get Tags
                        var tags = illust.MoreTags.Select(t => new Pixeez.Objects.Tag() { Name = t.Original, TranslatedName = t.Translated }).ToArray();
                        #endregion

                        #region Create IllustWork
                        var i = new Pixeez.Objects.IllustWork()
                        {
                            Type = "illust",
                            Id = id,
                            user = userbase == null ? null : new_user,
                            Title = illust.Title,
                            Caption = illust.Caption,
                            Width = illust.Width,
                            Height = illust.Height,
                            ImageUrls = image_urls,
                            PageCount = illust.PageCount,
                            Metadata = new Pixeez.Objects.Metadata() { Pages = pages },
                            CreatedTime = illust.CreateTime,
                            ReuploadedTime = illust.UploadTime.ToString(),
                            SanityLevel = illust.SanityLevel.ToString(),
                            total_bookmarks = illust.BookmarkCount,
                            total_view = illust.ViewCount,
                            tags = tags,
                            meta_pages = meta_pages is IEnumerable<Pixeez.Objects.MetaPages> ?  meta_pages.ToArray() : null,
                            meta_single_page = new Pixeez.Objects.MetaSinglePage() {  OriginalImageUrl = image_urls.Original },
                        };
                        await i.RefreshIllustBookmarkState();
                        i.Cache();
                        #endregion

                        result = new List<Pixeez.Objects.Work>() { i };
                    }
                    else work.Message.ShowToast("GetIllustById");
                }
                catch (Exception ex) { ex.ERROR("SearchIllustById"); }
            }
            return (result);
        }
        #endregion
    }
}
