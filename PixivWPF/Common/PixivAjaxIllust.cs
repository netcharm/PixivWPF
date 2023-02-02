using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Windows;
using System.Globalization;

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

        [JsonProperty("url")]
        public string Url { get; set; }

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

    public class AjaxIllustNoLoginData
    {
       // noLogin
    }

    public class AjaxIllustData
    {
        [JsonProperty("error")]
        public bool Error { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("body")]
        public AjaxIllustWork Illust { get; set; }
    }

    public class AjaxUserBackground
    {
        [JsonProperty("repeat")]
        public bool? Repeat { get; set; }

        [JsonProperty("color")]
        public System.Windows.Media.Color? Color { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("isPrivate")]
        public bool? IsPrivate { get; set; }
    }

    public class AjaxUser
    {
        [JsonProperty("userId")]
        public string UserId { get; set; }
        public long? Id
        {
            get
            {
                long id = 0;
                if (!string.IsNullOrEmpty(UserId) && long.TryParse(UserId, out id))
                    return (id);
                else return (null);
            }
        }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("image")]
        public string Image { get; set; }

        [JsonProperty("imageBig")]
        public string ImageBig { get; set; }

        [JsonProperty("premium")]
        public bool Premium { get; set; }

        [JsonProperty("isFollowed")]
        public bool IsFollowed { get; set; }

        [JsonProperty("isMypixiv")]
        public bool IsMypixiv { get; set; }

        [JsonProperty("isBlocking")]
        public bool IsBlocking { get; set; }

        [JsonProperty("background")]
        public AjaxUserBackground Background { get; set; }

        [JsonProperty("sketchLiveId")]
        public string SketchLiveId { get; set; }

        [JsonProperty("partial")]
        public int Partial { get; set; }

        [JsonProperty("acceptRequest")]
        public bool AcceptRequest { get; set; }

        [JsonProperty("sketchLives")]
        public List<object> SketchLives { get; set; }
    }

    public class AjaxUserData
    {
        [JsonProperty("error")]
        public bool Error { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("body")]
        public AjaxUser User { get; set; }
    }

    public class AjaxUserProfileBookmarkCountType
    {
        [JsonProperty("illust")]
        public int Illust { get; set; }

        [JsonProperty("novel")]
        public int Novel { get; set; }
    }

    public class AjaxUserProfileBookmarkCount
    {
        [JsonProperty("public")]
        public AjaxUserProfileBookmarkCountType Public { get; set; }

        [JsonProperty("private")]
        public AjaxUserProfileBookmarkCountType Private { get; set; }
    }

    public class AjaxUserProfileExternalSiteWorksStatus
    {
        [JsonProperty("booth")]
        public bool Booth { get; set; }

        [JsonProperty("sketch")]
        public bool Sketch { get; set; }

        [JsonProperty("vroidHub")]
        public bool VroidHub { get; set; }
    }

    public class AjaxUserProfileRequestPostWorks
    {
        [JsonProperty("artworks")]
        public List<AjaxIllustWork> Artworks { get; set; }

        [JsonProperty("novels")]
        public List<AjaxIllustWork> Novels { get; set; }
    }

    public class AjaxUserProfileRequest
    {
        [JsonProperty("showRequestTab")]
        public bool ShowRequestTab { get; set; }

        [JsonProperty("showRequestSentTab")]
        public bool ShowRequestSentTab { get; set; }

        [JsonProperty("postWorks")]
        public AjaxUserProfileRequestPostWorks PostWorks { get; set; }
    }

    public class AjaxUserProfile
    {
        [JsonProperty("illusts")]
        public Dictionary<string, AjaxIllustWork> Illusts { get; set; }

        [JsonProperty("manga")]
        public Dictionary<string, AjaxIllustWork> Manga { get; set; }

        [JsonProperty("novels")]
        public Dictionary<string, AjaxIllustWork> Novels { get; set; }

        [JsonProperty("mangaSeries")]
        public Dictionary<string, AjaxIllustWork> MangaSeries { get; set; }

        [JsonProperty("novelSeries")]
        public Dictionary<string, AjaxIllustWork> NovelSeries { get; set; }

        [JsonProperty("pickup")]
        public List<AjaxIllustWork> Pickup { get; set; }

        [JsonProperty("bookmarkCount")]
        public AjaxUserProfileBookmarkCount BookmarkCount { get; set; }

        [JsonProperty("externalSiteWorksStatus")]
        public AjaxUserProfileExternalSiteWorksStatus ExternalSiteWorksStatus { get; set; }

        [JsonProperty("request")]
        public AjaxUserProfileRequest Request { get; set; }
    }

    public class AjaxUserProfileData
    {
        [JsonProperty("error")]
        public bool Error { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("body")]
        public AjaxUserProfile Profile { get; set; }
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

        /// <summary>
        /// Will 404 when not login, so function failed :-(
        /// </summary>
        /// <param name="url"></param>
        /// <param name="tokens"></param>
        /// <returns></returns>
        public static async Task<List<Pixeez.Objects.Page>> GetMetaPages(this string url, Pixeez.Tokens tokens = null)
        {
            List<Pixeez.Objects.Page> result = null;

            url.DEBUG("GetMetaPages");
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
            $"GetMetaData_{url}".DEBUG();
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
            List<Pixeez.Objects.Work> result = new List<Pixeez.Objects.Work>();

            if (tokens == null) tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return (result);

            var url = GetAjaxIllustUrl(id);
            url.DEBUG("SearchIllustById");
            var json_text = await Application.Current.GetRemoteJsonAsync(url);
            if (!string.IsNullOrEmpty(json_text))
            {
                try
                {
                    var work = JToken.Parse(json_text).ToObject<AjaxIllustData>();
                    if (!work.Error)
                    {
                        var illust = work.Illust;
                        var user_orig = new Pixeez.Objects.NewUser()
                        {
                            Account = illust.UserAccount,
                            Id = long.Parse(illust.UserId),
                            Name = illust.UserName,
                            profile_image_urls = new Pixeez.Objects.ImageUrls(),
                        };

                        #region Get/Set user
                        var userbase = await illust.UserId.GetUser();// ?? await illust.UserId.GetAjaxUser();
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
                                user_orig = new Pixeez.Objects.NewUser()
                                {
                                    Id = userbase.Id,
                                    Account = userbase.Account,
                                    Name = userbase.Name,
                                    Email = userbase.Email,
                                    is_followed = userbase.is_followed,
                                    profile_image_urls = new Pixeez.Objects.ImageUrls() { Small = avatar, Medium = avatar },
                                };
                            }
                        }
                        #endregion

                        #region Set Image Urls
                        var image_urls = illust.ImageUrls is AjaxIllustImageUrls ? new Pixeez.Objects.ImageUrls() : null;
                        var self_illsuts = illust.UserIllusts.Where(s => s.Key.Equals($"{id}"));
                        var self_illust = self_illsuts.Count()>0 ? self_illsuts.First().Value : null;
                        if (illust.ImageUrls is AjaxIllustImageUrls)
                        {
                            image_urls.Small = illust.ImageUrls.Mini;
                            image_urls.Px128x128 = illust.ImageUrls.Thumbnail ?? (self_illust != null ? self_illust.Url : string.Empty);
                            image_urls.SquareMedium = illust.ImageUrls.Thumbnail ?? (self_illust != null ? self_illust.Url : string.Empty);
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
                            //user = userbase == null ? null : new_user,
                            user = userbase == null ? (new_user == null ? user_orig : new_user) : user_orig,
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
                        //if (i is Pixeez.Objects.IllustWork) i.Id = id;
                        #endregion

                        //if (i.ImageUrls is Pixeez.Objects.ImageUrls &&
                        //    (i.PageCount == 1 || (i.PageCount > 1 && pages is List<Pixeez.Objects.Page>)))
                            i.Cache();
                        result = new List<Pixeez.Objects.Work>() { i };
                        await i.RefreshIllustBookmarkState();
                    }
                    else work.Message.ShowToast("GetIllustById");
                }
                catch (Exception ex) { ex.ERROR("SearchIllustById"); }
            }
            return (result);
        }
        #endregion

        #region User and Profile Helper
        public static string GetAjaxUserUrl(this string id)
        {
            if (string.IsNullOrEmpty(id)) return (string.Empty);
            else return ($"https://www.pixiv.net/ajax/user/{id}");
        }

        public static string GetAjaxUserUrl(this long id)
        {
            if (id == 0) return (string.Empty);
            else return (GetAjaxUserUrl(id.ToString()));
        }

        public static string GetAjaxUserUrl(this long? id)
        {
            return (GetAjaxUserUrl((id ?? 0).ToString()));
        }

        public static string GetAjaxUserProfileUrl(this string id)
        {
            if (string.IsNullOrEmpty(id)) return (string.Empty);
            else return ($"https://www.pixiv.net/ajax/user/{id}/profile/all?lang={CultureInfo.CurrentCulture.TwoLetterISOLanguageName}");
        }

        public static string GetAjaxUserProfileUrl(this long id)
        {
            if (id == 0) return (string.Empty);
            else return (GetAjaxUserProfileUrl(id.ToString()));
        }

        public static string GetAjaxUserProfileUrl(this long? id)
        {
            return (GetAjaxUserProfileUrl((id ?? 0).ToString()));
        }

        public static async Task<Pixeez.Objects.UserBase> GetAjaxUser(this string id, Pixeez.Tokens tokens = null)
        {
            long uid = 0;
            $"{uid}".DEBUG("GetAjaxUser");
            if (!string.IsNullOrEmpty(id) && long.TryParse(id, out uid)) return (await GetAjaxUser(uid, tokens));
            else return (null);
        }

        public static async Task<Pixeez.Objects.UserBase> GetAjaxUser(this long id, Pixeez.Tokens tokens = null)
        {
            Pixeez.Objects.UserBase result = null;
            if (tokens == null) tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return (result);

            var url = GetAjaxUserUrl(id);
            var json_text = await Application.Current.GetRemoteJsonAsync(url);
            if (!string.IsNullOrEmpty(json_text))
            {
                try
                {
                    var user = JToken.Parse(json_text).ToObject<AjaxUserData>();
                    if (!user.Error)
                    {
                        var info = await tokens.GetUserInfoAsync(id.ToString());
                        if (info is Pixeez.Objects.UserInfo)
                        {
                            info.Cache();
                            info.user.Cache();
                            result = info.user;
                        }
                    }
                }
                catch (Exception ex) { ex.ERROR("GetAjaxUser"); }
            }
            return (result);
        }

        public static async Task<Pixeez.Objects.UserBase> GetAjaxUserProfile(this string id, Pixeez.Tokens tokens = null)
        {
            long uid = 0;
            if (!string.IsNullOrEmpty(id) && long.TryParse(id, out uid)) return (await GetAjaxUserProfile(uid, tokens));
            else return (null);
        }

        public static async Task<Pixeez.Objects.UserBase> GetAjaxUserProfile(this long id, Pixeez.Tokens tokens = null)
        {
            Pixeez.Objects.UserBase result = null;
            if (tokens == null) tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return (result);

            var url = GetAjaxUserProfileUrl(id);
            var json_text = await Application.Current.GetRemoteJsonAsync(url);
            if (!string.IsNullOrEmpty(json_text))
            {
                try
                {
                    var user = JToken.Parse(json_text).ToObject<AjaxUserProfileData>();
                    if (!user.Error)
                    {
                        var profile = user.Profile;
                        if (profile is AjaxUserProfile)
                        {
                            var user_profile = new Pixeez.Objects.Profile();
                            user_profile.Contacts = new Pixeez.Objects.Contacts();
                            //user_profile.id

                            //result = profile.user;
                        }
                    }
                }
                catch (Exception ex) { ex.ERROR("GetAjaxUserProfile"); }
            }
            return (result);
        }

        public static async Task<List<Pixeez.Objects.UserBase>> SearchUserById(this long id, Pixeez.Tokens tokens = null)
        {
            var result = new List<Pixeez.Objects.UserBase>();

            if (tokens == null) tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return (result);

            var user = await GetAjaxUser(id, tokens);
            if (user is Pixeez.Objects.UserBase) result.Add(user);

            return (result);
        }
        #endregion
    }
}
