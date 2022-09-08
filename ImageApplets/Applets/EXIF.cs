using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Mono.Options;
using CompactExifLib;

namespace ImageApplets.Applets
{
    class EXIF : Applet
    {
        internal protected class DateValue
        {
            internal protected double y { get; set; } = double.NaN;
            internal protected double m { get; set; } = double.NaN;
            internal protected double d { get; set; } = double.NaN;
            internal protected double w { get; set; } = double.NaN;
            internal protected double h { get; set; } = double.NaN;
            internal protected double n { get; set; } = double.NaN;
            internal protected double s { get; set; } = double.NaN;

            private string _text_ { get; set; } = string.Empty;

            internal protected DateValue(string text)
            {
                Parsing(text);
            }

            internal protected void Parsing(string text)
            {
                if (!string.IsNullOrEmpty(text))
                {
                    var word = text.Trim();
                    if (Regex.IsMatch(word, @"/(.+?)/i?", RegexOptions.IgnoreCase))
                    {
                        word = word.Trim(new char[] { 'i', '/' });
                    }
                    _text_ = word;

                    #region Date Attribures
                    y = double.NaN;
                    m = double.NaN;
                    d = double.NaN;
                    w = double.NaN;
                    h = double.NaN;
                    n = double.NaN;
                    s = double.NaN;
                    #endregion

                    #region Parsing Date
                    if (Regex.IsMatch(_text_, @"^(\d{2,4})(/-,\. 年)(\d{1,2})(/-,\. 月)(\d{1,2})日?", RegexOptions.IgnoreCase))
                    {
                        var match = Regex.Match(_text_, @"^(\d{2,4})(/-,\. 年)?(\d{1,2})(/-,\. 月)?(\d{1,2})日?", RegexOptions.IgnoreCase);
                        if (match.Groups[1].Success) y = Convert.ToInt32(match.Groups[1].Value.Trim());
                        if (match.Groups[2].Success) m = Convert.ToInt32(match.Groups[2].Value.Trim());
                        if (match.Groups[3].Success) d = Convert.ToInt32(match.Groups[3].Value.Trim());
                    }
                    else if (Regex.IsMatch(_text_, @"^(\d{2,4})(/-,\. 年)(\d{1,2})(/-,\. 月)?", RegexOptions.IgnoreCase))
                    {
                        var match = Regex.Match(_text_, @"^(\d{2,4})(/-,\. 年)?(\d{1,2})(/-,\. 月)?", RegexOptions.IgnoreCase);
                        if (match.Groups[1].Success) y = Convert.ToInt32(match.Groups[1].Value.Trim());
                        if (match.Groups[2].Success) m = Convert.ToInt32(match.Groups[2].Value.Trim());
                    }
                    else if (Regex.IsMatch(_text_, @"^(\d{1,2})(/-,\. 月)(\d{1,2})日?", RegexOptions.IgnoreCase))
                    {
                        var match = Regex.Match(_text_, @"^(\d{1,2})(/-,\. 月)(\d{1,2})日?", RegexOptions.IgnoreCase);
                        if (match.Groups[1].Success) m = Convert.ToInt32(match.Groups[1].Value.Trim());
                        if (match.Groups[2].Success) d = Convert.ToInt32(match.Groups[2].Value.Trim());
                    }
                    else
                    {
                        if (Regex.IsMatch(_text_, @"(\d{2,4})(y(ear)?|年)", RegexOptions.IgnoreCase))
                        {
                            var match = Regex.Match(_text_, @"(\d{2,4})(y(ear)?|年)", RegexOptions.IgnoreCase);
                            if (match.Groups[1].Success) y = Convert.ToInt32(match.Groups[1].Value.Trim());
                        }
                        if (Regex.IsMatch(_text_, @"(\d{1,2})(m(onth)?|月)", RegexOptions.IgnoreCase))
                        {
                            var match = Regex.Match(_text_, @"(\d{1,2})(m(onth)?|月)", RegexOptions.IgnoreCase);
                            if (match.Groups[1].Success) m = Convert.ToInt32(match.Groups[1].Value.Trim());
                        }
                        if (Regex.IsMatch(_text_, @"(\d{1,2})(d(ay)?|日|号)", RegexOptions.IgnoreCase))
                        {
                            var match = Regex.Match(_text_, @"(\d{1,2})(d(ay)?|日|号)", RegexOptions.IgnoreCase);
                            if (match.Groups[1].Success) d = Convert.ToInt32(match.Groups[1].Value.Trim());
                        }
                        if (Regex.IsMatch(_text_, @"(星期|周)?(\d)(w(eek(day)?)?)?", RegexOptions.IgnoreCase))
                        {
                            var match = Regex.Match(_text_, @"(星期|周)?(\d)(w(eek(day)?)?)?", RegexOptions.IgnoreCase);
                            if ((match.Groups[1].Success || match.Groups[3].Success) && match.Groups[2].Success) w = Convert.ToInt32(match.Groups[2].Value.Trim());
                        }
                    }
                    if (!double.IsNaN(y) && y <= 0) y = double.NaN;
                    if (!double.IsNaN(m) && (m <= 0 || m > 12)) m = double.NaN;
                    if (!double.IsNaN(y) && (d <= 0 || d > 31)) d = double.NaN;
                    if (!double.IsNaN(m) && (w <= 0 || w > 7)) w = double.NaN;
                    #endregion

                    #region Parsing Time
                    if (Regex.IsMatch(_text_, @"(\d{1,2})[点时:](\d{{1,2})[分:](\d{1,2})秒?$", RegexOptions.IgnoreCase))
                    {
                        var match = Regex.Match(_text_, @"(\d{1,2})[点时:](\d{{1,2})[分:](\d{1,2})秒?$", RegexOptions.IgnoreCase);
                        if (match.Groups[1].Success) h = Convert.ToInt32(match.Groups[1].Value.Trim());
                        if (match.Groups[2].Success) n = Convert.ToInt32(match.Groups[2].Value.Trim());
                        if (match.Groups[3].Success) s = Convert.ToInt32(match.Groups[3].Value.Trim());
                    }
                    else if (Regex.IsMatch(_text_, @"(\d{1,2})[点时:](\d{{1,2})[分:]?$", RegexOptions.IgnoreCase))
                    {
                        var match = Regex.Match(_text_, @"(\d{1,2})[点时:](\d{{1,2})[分:]?$", RegexOptions.IgnoreCase);
                        if (match.Groups[1].Success) h = Convert.ToInt32(match.Groups[1].Value.Trim());
                        if (match.Groups[2].Success) n = Convert.ToInt32(match.Groups[2].Value.Trim());
                    }
                    else if (Regex.IsMatch(_text_, @"(\d{{1,2})[分:](\d{1,2})秒?$", RegexOptions.IgnoreCase))
                    {
                        var match = Regex.Match(_text_, @"(\d{{1,2})[分:](\d{1,2})秒?$", RegexOptions.IgnoreCase);
                        if (match.Groups[1].Success) n = Convert.ToInt32(match.Groups[1].Value.Trim());
                        if (match.Groups[2].Success) s = Convert.ToInt32(match.Groups[2].Value.Trim());
                    }
                    else
                    {
                        if (Regex.IsMatch(_text_, @"(\d{1,2})(h(our)?|点|小?时)", RegexOptions.IgnoreCase))
                        {
                            var match = Regex.Match(_text_, @"(\d{1,2})(h(our)?|点|小?时)", RegexOptions.IgnoreCase);
                            if (match.Groups[1].Success) h = Convert.ToInt32(match.Groups[1].Value.Trim());
                        }
                        if (Regex.IsMatch(_text_, @"(\d{1,2})(min(ute)?|分钟?)", RegexOptions.IgnoreCase))
                        {
                            var match = Regex.Match(_text_, @"(\d{1,2})(min(ute)?|分钟?)", RegexOptions.IgnoreCase);
                            if (match.Groups[1].Success) n = Convert.ToInt32(match.Groups[1].Value.Trim());
                        }
                        if (Regex.IsMatch(_text_, @"(\d{1,2})(s(ec(ond)?)?|秒)", RegexOptions.IgnoreCase))
                        {
                            var match = Regex.Match(_text_, @"(\d{1,2})(s(ec(ond)?)?|秒)", RegexOptions.IgnoreCase);
                            if (match.Groups[1].Success) s = Convert.ToInt32(match.Groups[1].Value.Trim());
                        }
                    }
                    if (!double.IsNaN(y) && (h < 0 || h > 23)) h = double.NaN;
                    if (!double.IsNaN(m) && (n < 0 || m > 59)) n = double.NaN;
                    if (!double.IsNaN(y) && (s < 0 || d > 59)) s = double.NaN;
                    #endregion
                }
            }
        }

        public override Applet GetApplet()
        {
            return (new HasExif());
        }

        private string DateTimeFormat = "yyyy-MM-dd HH:mm:ss.fffzzz";
        private string SearchScope = "All";
        private string SearchTerm = string.Empty;
        private CompareMode Mode = CompareMode.VALUE;
        private DateValue _date_ = null;

        public EXIF()
        {
            Category = AppletCategory.ImageAttribure;

            var opts = new OptionSet()
            {
                { "m|mode=", "EXIF Search Mode {<EQ|NEQ|LT|LE|GT|GE|AND|OR|NOT|VALUE>}", v => { if (v != null) Enum.TryParse(v.ToUpper(), out Mode); } },
                { "c|category=", "EXIF Search Fron {Artist|Author|Title|Suject|Comment|Keyword|Tag|Copyright|Software|Rate|Date|All}", v => { if (v != null) SearchScope = v.Trim().Trim('"'); } },
                { "s|search=", "EXIF Search {Term}", v => { if (v != null) SearchTerm = v.Trim().Trim('"'); } },
                { "" },
            };
            AppendOptions(opts);
        }

        public override List<string> PharseOptions(IEnumerable<string> args)
        {
            var extras = base.PharseOptions(args);

            _date_ = new DateValue(ConvertChineseNumberString(SearchTerm));

            return (extras);
        }

        private string GetUnicodeString(ExifData exif, ExifTag tag, bool raw = false, bool id = false, bool ignore_endian = true)
        {
            var result = string.Empty;
            if (exif is ExifData && exif.TagExists(tag))
            {
                if (raw)
                {
                    ExifTagType type;
                    int count;
                    int index;
                    byte[] bytes;
                    exif.GetTagRawData(tag, out type, out count, out bytes, out index);
                    if (!ignore_endian && exif.ByteOrder == ExifByteOrder.BigEndian)
                        result = Encoding.BigEndianUnicode.GetString(bytes.Skip(index).Take(count).ToArray());
                    else
                        result = Encoding.Unicode.GetString(bytes.Skip(index).Take(count).ToArray());
                }
                else
                {
                    exif.GetTagValue(tag, out result, id ? StrCoding.IdCode_Utf16 : StrCoding.Utf16Le_Byte);
                }
            }
            return (result);
        }

        private string GetUTF8String(ExifData exif, ExifTag tag, bool raw = false)
        {
            var result = string.Empty;
            if (exif is ExifData && exif.TagExists(tag))
            {
                try
                {
                    if (raw)
                    {
                        ExifTagType type;
                        int count;
                        int index;
                        byte[] bytes;
                        exif.GetTagRawData(tag, out type, out count, out bytes, out index);
                        result = Encoding.UTF8.GetString(bytes);
                    }
                    else
                    {
                        exif.GetTagValue(tag, out result, StrCoding.Utf8);
                    }
                }
                catch { }
            }
            return (result);
        }

        private string GetValueString(ExifData exif, ExifTag tag, bool raw = false)
        {
            var result = string.Empty;
            if (exif is ExifData && exif.TagExists(tag))
            {
                try
                {
                    if (raw)
                    {
                        ExifTagType type;
                        int count;
                        int index;
                        byte[] bytes;
                        exif.GetTagRawData(tag, out type, out count, out bytes, out index);
                        result = Encoding.UTF8.GetString(bytes);
                    }
                    else
                    {
                        int value;
                        exif.GetTagValue(tag, out value);
                        result = $"{value}";
                    }
                }
                catch { }
            }
            return (result);
        }

        private string GetDateString(ExifData exif, ExifTag tag, bool raw = false)
        {
            var result = string.Empty;
            if (exif is ExifData && exif.TagExists(tag))
            {
                try
                {
                    if (raw)
                    {
                        ExifTagType type;
                        int count;
                        int index;
                        byte[] bytes;
                        exif.GetTagRawData(tag, out type, out count, out bytes, out index);
                        result = Encoding.UTF8.GetString(bytes);
                    }
                    else
                    {
                        DateTime value;
                        exif.GetTagValue(tag, out value);
                        result = value.ToString(DateTimeFormat);
                    }
                }
                catch { }
            }
            return (result);
        }

        static private Dictionary<string, string> num_chs = new Dictionary<string, string>()
        {
            { "一", "1" }, { "二", "2" }, { "三", "3" }, { "四", "4" }, { "五", "5" }, { "六", "6" }, { "七", "7" }, { "八", "8" }, { "九", "9" }, { "零", "0" },
            { "壹", "1" }, { "贰", "2" }, { "叁", "3" }, { "肆", "4" }, { "伍", "5" }, { "陆", "6" }, { "柒", "7" }, { "捌", "8" }, { "玖", "9" },
        };

        private string ConvertChineseNumberString(string text)
        {
            var result = text;

            foreach (var kv in num_chs)
            {
                result = result.Replace(kv.Key, kv.Value); ;
            }
            if (result.StartsWith("十")) result = $"1{result}";
            var matches_w = Regex.Matches(result, @"((\d+)(万|千|百|十)?)+", RegexOptions.IgnoreCase);
            foreach(Match mw in matches_w)
            {
                if (mw.Success)
                {
                    var nums = new List<double>();
                    var last_unit = string.Empty;
                    var matches_s = Regex.Matches(mw.Value, @"(\d+)(万|千|百|十)?", RegexOptions.IgnoreCase);
                    foreach (Match ms in matches_s)
                    {
                        var factor = 1;
                        if (ms.Groups[2].Success)
                        {
                            if (ms.Groups[2].Value.Equals("万")) factor = 10000;
                            else if (ms.Groups[2].Value.Equals("千")) factor = 1000;
                            else if (ms.Groups[2].Value.Equals("百")) factor = 100;
                            else if (ms.Groups[2].Value.Equals("十")) factor = 10;
                            last_unit = ms.Groups[2].Value;
                        }
                        else
                        {
                            if (last_unit.Equals("万")) factor = 1000;
                            else if (last_unit.Equals("千")) factor = 100;
                            else if (last_unit.Equals("百")) factor = 10;
                            else if (last_unit.Equals("十")) factor = 1;
                        }

                        var num = ms.Groups[1].Success ? Convert.ToInt32(ms.Groups[1].Value) : 1;
                        nums.Add(num);
                        nums.Add(factor);
                    }
                    var value = .0;
                    for (var i = 0; i < nums.Count; i += 2)
                    {
                        value += nums[i] * nums[i + 1];
                    }
                    result = result.Replace(mw.Value, $"{value}");
                }
            }

            return (result);
        } 

        private DateTime? ConvertFromDateValue(DateTime src, DateValue dst)
        {
            DateTime? result = null;
            try
            {
                var y = (int)(double.IsNaN(dst.y) ? src.Year : dst.y);
                var m = (int)(double.IsNaN(dst.m) ? src.Month : dst.m);
                var d = (int)(double.IsNaN(dst.d) ? src.Day : dst.d);
                var h = (int)(double.IsNaN(dst.h) ? src.Hour : dst.h);
                var n = (int)(double.IsNaN(dst.n) ? src.Minute : dst.n);
                var s = (int)(double.IsNaN(dst.s) ? src.Second : dst.s);
                var dt = new DateTime(y, m, d, h, n, s);
                result = dt;
            }
            catch { }
            return (result);
        }

        private dynamic Compare(string text, string word, bool? ignorecase = null)
        {
            if (Mode == CompareMode.VALUE)
                return (text);
            else
            {
                var status = false;
                var regex_ignore = ignorecase ?? false ? RegexOptions.IgnoreCase : RegexOptions.None;

                word = word.Trim();
                if (Regex.IsMatch(word, @"/(.+?)/i?", RegexOptions.IgnoreCase))
                {
                    regex_ignore = ignorecase == null || word.EndsWith("/i", StringComparison.CurrentCultureIgnoreCase) ? RegexOptions.IgnoreCase : RegexOptions.None;
                    word = word.Trim(new char[] { 'i', '/' });
                }
                switch (Mode)
                {
                    case CompareMode.EQ:
                        status = regex_ignore == RegexOptions.IgnoreCase ? text.Equals(word, StringComparison.CurrentCultureIgnoreCase) : text.Equals(word);
                        break;
                    case CompareMode.NEQ:
                        status = regex_ignore == RegexOptions.IgnoreCase ? !text.Equals(word, StringComparison.CurrentCultureIgnoreCase) : !text.Equals(word);
                        break;
                    case CompareMode.HAS:
                        status = Regex.IsMatch(text, word, regex_ignore);
                        break;
                    case CompareMode.NONE:
                        status = !Regex.IsMatch(text, word, regex_ignore);
                        break;
                    default:
                        break;
                }
                return (status);
            }
        }

        private dynamic Compare(DateTime src, DateTime dst)
        {
            if (Mode == CompareMode.VALUE)
                return (src);
            else
            {
                var status = false;

                switch (Mode)
                {
                    case CompareMode.EQ:
                        status = src.Ticks == dst.Ticks;
                        break;
                    case CompareMode.NEQ:
                        status = src.Ticks != dst.Ticks;
                        break;
                    case CompareMode.NOT:
                        status = src.Ticks != dst.Ticks;
                        break;
                    case CompareMode.LT:
                        status = src.Ticks < dst.Ticks;
                        break;
                    case CompareMode.LE:
                        status = src.Ticks <= dst.Ticks;
                        break;
                    case CompareMode.GT:
                        status = src.Ticks > dst.Ticks;
                        break;
                    case CompareMode.GE:
                        status = src.Ticks >= dst.Ticks;
                        break;
                    default:
                        break;
                }
                return (status);
            }
        }

        private dynamic Compare(DateTime src, string dst)
        {
            return (Compare(src, new DateValue(ConvertChineseNumberString(dst))));
        }

        private dynamic Compare(DateTime src, DateValue dst)
        {
            if (Mode == CompareMode.VALUE)
                return (src.ToString(DateTimeFormat));
            else
            {
                var status = false;

                if (dst is DateValue)
                {
                    var dt = ConvertFromDateValue(src, dst);
                    var wd = double.IsNaN(dst.w) ? src.DayOfWeek : (DayOfWeek)Enum.Parse(typeof(DayOfWeek), $"{dst.w - 1}");

                    switch (Mode)
                    {
                        case CompareMode.HAS:
                            if (!double.IsNaN(dst.y)) status = status || dst.y == src.Year;
                            if (!double.IsNaN(dst.m)) status = status || dst.m == src.Month;
                            if (!double.IsNaN(dst.d)) status = status || dst.d == src.Day;
                            if (!double.IsNaN(dst.h)) status = status || dst.h == src.Hour;
                            if (!double.IsNaN(dst.n)) status = status || dst.n == src.Second;
                            if (!double.IsNaN(dst.s)) status = status || dst.s == src.Second;
                            break;
                        case CompareMode.NEQ:
                            if (!double.IsNaN(dst.w)) status = src.DayOfWeek != wd;
                            else status = dt.HasValue && src != dt;
                            break;
                        case CompareMode.EQ:
                            if (!double.IsNaN(dst.w)) status = src.DayOfWeek == wd;
                            else status = dt.HasValue && src == dt;
                            break;
                        case CompareMode.LT:
                            if (!double.IsNaN(dst.w)) status = src.DayOfWeek < wd;
                            else status = dt.HasValue && src < dt;
                            break;
                        case CompareMode.LE:
                            if (!double.IsNaN(dst.w)) status = src.DayOfWeek <= wd;
                            else status = dt.HasValue && src <= dt;
                            break;
                        case CompareMode.GT:
                            if (!double.IsNaN(dst.w)) status = src.DayOfWeek > wd;
                            else status = dt.HasValue && src > dt;
                            break;
                        case CompareMode.GE:
                            if (!double.IsNaN(dst.w)) status = src.DayOfWeek >= wd;
                            else status = dt.HasValue && src >= dt;
                            break;
                        default:
                            break;
                    }
                }
                return (status);
            }
        }

        public override bool Execute<T>(ExifData exif, out T result, params object[] args)
        {
            var ret = false;
            result = default(T);
            try
            {
                if (exif is ExifData)
                {
                    dynamic status = false;

                    #region Get exif attributes
                    var title = GetUnicodeString(exif, ExifTag.XpTitle, raw: true);
                    if (string.IsNullOrEmpty(title)) GetUTF8String(exif, ExifTag.ImageDescription);

                    var subject = GetUnicodeString(exif, ExifTag.XpSubject, raw: true);

                    var keywords = GetUnicodeString(exif, ExifTag.XpKeywords, raw: true);

                    var comments = GetUnicodeString(exif, ExifTag.XpComment, raw: true);
                    if (string.IsNullOrEmpty(comments)) GetUnicodeString(exif, ExifTag.UserComment, id: true);

                    var artist = GetUnicodeString(exif, ExifTag.XpAuthor, raw: true);
                    if (string.IsNullOrEmpty(artist)) artist = GetUTF8String(exif, ExifTag.Artist);

                    var copyright = GetUTF8String(exif, ExifTag.Copyright);

                    var software = GetUTF8String(exif, ExifTag.Software);

                    var rate = GetValueString(exif, ExifTag.RatingPercent);
                    var rank = GetValueString(exif, ExifTag.Rating);

                    var date = GetDateString(exif, ExifTag.GpsDateStamp);
                    if (string.IsNullOrEmpty(date)) date = GetDateString(exif, ExifTag.DateTimeOriginal);
                    if (string.IsNullOrEmpty(date)) date = GetDateString(exif, ExifTag.DateTimeDigitized);
                    if (string.IsNullOrEmpty(date)) date = GetDateString(exif, ExifTag.DateTime);
                    if (string.IsNullOrEmpty(date)) date = exif.LastWriteTime.ToString(DateTimeFormat);
                    if (string.IsNullOrEmpty(date)) date = exif.CreateTime.ToString(DateTimeFormat);
                    if (string.IsNullOrEmpty(date)) date = exif.LastAccessTime.ToString(DateTimeFormat);
                    #endregion

                    var cats = SearchScope.Split(',').Select(c => c.Trim().ToLower()).ToList();
                    if (!string.IsNullOrEmpty(SearchTerm))
                    {
                        var word = SearchTerm;
                        if(_date_ == null) _date_ = new DateValue(ConvertChineseNumberString(word));

                        if (Mode == CompareMode.VALUE) Mode = CompareMode.HAS;

                        #region Comparing attribute
                        if (cats.Contains("all"))
                        {
                            status = status || Compare(title, word);
                            status = status || Compare(subject, word);
                            status = status || Compare(keywords, word);
                            status = status || Compare(comments, word);
                            status = status || Compare(artist, word);
                            status = status || Compare(copyright, word);
                            status = status || Compare(software, word);
                            status = status || Compare(rate, word);
                            status = status || Compare(rank, word);
                            status = status || Compare(date, word);
                        }
                        else if (Mode == CompareMode.AND)
                        {
                            if (cats.Contains("title")) status = status && Compare(title, word);
                            if (cats.Contains("subject")) status = status && Compare(subject, word);
                            if (cats.Contains("keywords")) status = status && Compare(keywords, word);
                            if (cats.Contains("tag")) status = status && Compare(keywords, word);
                            if (cats.Contains("comments")) status = status && Compare(comments, word);
                            if (cats.Contains("artist")) status = status && Compare(artist, word);
                            if (cats.Contains("author")) status = status && Compare(artist, word);
                            if (cats.Contains("copyright")) status = status && Compare(copyright, word);
                            if (cats.Contains("software")) status = status && Compare(software, word);
                            if (cats.Contains("rate")) status = status && Compare(rate, word);
                            if (cats.Contains("rank")) status = status && Compare(rank, word);
                            if (cats.Contains("date")) status = status && Compare(date, word);
                        }
                        else if (Mode == CompareMode.OR)
                        {
                            if (cats.Contains("title")) status = status || Compare(title, word);
                            if (cats.Contains("subject")) status = status || Compare(subject, word);
                            if (cats.Contains("keywords")) status = status || Compare(keywords, word);
                            if (cats.Contains("tag")) status = status || Compare(keywords, word);
                            if (cats.Contains("comments")) status = status || Compare(comments, word);
                            if (cats.Contains("artist")) status = status || Compare(artist, word);
                            if (cats.Contains("author")) status = status || Compare(artist, word);
                            if (cats.Contains("copyright")) status = status || Compare(copyright, word);
                            if (cats.Contains("software")) status = status || Compare(software, word);
                            if (cats.Contains("rate")) status = status || Compare(rate, word);
                            if (cats.Contains("rank")) status = status || Compare(rank, word);
                            if (cats.Contains("date")) status = status || Compare(date, word);
                        }
                        else if (Mode == CompareMode.NOT)
                        {
                            status = status || (Compare(title, word) ? !cats.Contains("title") : true);
                            status = status || (Compare(subject, word) ? !cats.Contains("subject") : true);
                            status = status || (Compare(keywords, word) ? !cats.Contains("keywords") : true);
                            status = status || (Compare(keywords, word) ? !cats.Contains("tag") : true);
                            status = status || (Compare(comments, word) ? !cats.Contains("comments") : true);
                            status = status || (Compare(artist, word) ? !cats.Contains("artist") : true);
                            status = status || (Compare(artist, word) ? !cats.Contains("author") : true);
                            status = status || (Compare(copyright, word) ? !cats.Contains("copyright") : true);
                            status = status || (Compare(software, word) ? !cats.Contains("software") : true);
                            status = status || (Compare(rate, word) ? !cats.Contains("rate") : true);
                            status = status || (Compare(rank, word) ? !cats.Contains("rank") : true);
                            status = status || (Compare(date, word) ? !cats.Contains("date") : true);
                        }
                        else if (Mode == CompareMode.HAS || Mode == CompareMode.NONE || Mode == CompareMode.EQ || Mode == CompareMode.NEQ)
                        {
                            if (cats.Contains("title")) status = status || Compare(title, word);
                            if (cats.Contains("subject")) status = status || Compare(subject, word);
                            if (cats.Contains("keywords")) status = status || Compare(keywords, word);
                            if (cats.Contains("tag")) status = status || Compare(keywords, word);
                            if (cats.Contains("comments")) status = status || Compare(comments, word);
                            if (cats.Contains("artist")) status = status || Compare(artist, word);
                            if (cats.Contains("author")) status = status || Compare(artist, word);
                            if (cats.Contains("copyright")) status = status || Compare(copyright, word);
                            if (cats.Contains("software")) status = status || Compare(software, word);
                            if (cats.Contains("rate")) status = status || Compare(rate, word);
                            if (cats.Contains("rank")) status = status || Compare(rank, word);
                            if (cats.Contains("date"))
                            {
                                DateTime date_value = default(DateTime);
                                if (!string.IsNullOrEmpty(date)) DateTime.TryParse(date, out date_value);
                                status = status || Compare(date_value, _date_);
                            }
                        }
                        else if (Mode == CompareMode.LT || Mode == CompareMode.LE || Mode == CompareMode.GT || Mode == CompareMode.GE)
                        {
                            int word_int;
                            var word_value = int.TryParse(word, out word_int) ? word.PadLeft(16, '0') : word;

                            var rate_value = string.IsNullOrEmpty(rate) ? rate : rate.PadLeft(16, '0');
                            var rank_value = string.IsNullOrEmpty(rank) ? rank : rank.PadLeft(16, '0');
 
                            if (cats.Contains("rate")) status = status || Compare(rate_value, word_value);
                            if (cats.Contains("rank")) status = status || Compare(rank_value, word_value);
                            if (cats.Contains("date"))
                            {
                                DateTime date_value = default(DateTime);
                                if (!string.IsNullOrEmpty(date)) DateTime.TryParse(date, out date_value);
                                status = status || Compare(date_value, _date_);
                            }
                        }
                    }
                    else
                    {
                        StringBuilder sb = new StringBuilder();
                        if (cats.Contains("title") && !string.IsNullOrEmpty(title)) sb.AppendLine($"{"".PadLeft(ValuePaddingLeft)}{title}");
                        if (cats.Contains("subject") && !string.IsNullOrEmpty(subject)) sb.AppendLine($"{"".PadLeft(ValuePaddingLeft)}{subject}");
                        if (cats.Contains("keywords") && !string.IsNullOrEmpty(keywords)) sb.AppendLine($"{"".PadLeft(ValuePaddingLeft)}{keywords}");
                        else if (cats.Contains("tags") && !string.IsNullOrEmpty(keywords)) sb.AppendLine($"{"".PadLeft(ValuePaddingLeft)}{keywords}");
                        if (cats.Contains("comments") && !string.IsNullOrEmpty(comments)) sb.AppendLine($"{"".PadLeft(ValuePaddingLeft)}{comments}");
                        if (cats.Contains("artist") && !string.IsNullOrEmpty(artist)) sb.AppendLine($"{"".PadLeft(ValuePaddingLeft)}{artist}");
                        else if (cats.Contains("author") && !string.IsNullOrEmpty(artist)) sb.AppendLine($"{"".PadLeft(ValuePaddingLeft)}{artist}");
                        if (cats.Contains("copyright") && !string.IsNullOrEmpty(copyright)) sb.AppendLine($"{"".PadLeft(ValuePaddingLeft)}{copyright}");
                        if (cats.Contains("software") && !string.IsNullOrEmpty(software)) sb.AppendLine($"{"".PadLeft(ValuePaddingLeft)}{software}");
                        if (cats.Contains("rate") && !string.IsNullOrEmpty(rate)) sb.AppendLine($"{"".PadLeft(ValuePaddingLeft)}{rate}");
                        if (cats.Contains("rank") && !string.IsNullOrEmpty(rank)) sb.AppendLine($"{"".PadLeft(ValuePaddingLeft)}{rank}");
                        if (cats.Contains("date") && !string.IsNullOrEmpty(date)) sb.AppendLine($"{"".PadLeft(ValuePaddingLeft)}{date}");
                        status = sb.ToString().Trim();
                    }
                    #endregion

                    //var t = ConvertChineseNumberString("4千30百3");
                    //var t = ConvertChineseNumberString("四千三百二");
                    //t = ConvertChineseNumberString("十二月");

                    ret = GetReturnValueByStatus(status);
                    result = (T)(object)status;
                }
            }
            catch (Exception ex) { ShowMessage(ex, Name); }
            return (ret);
        }

    }
}
