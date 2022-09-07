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
        public override Applet GetApplet()
        {
            return (new HasExif());
        }

        private string DateTimeFormat = "yyyy-MM-dd HH:mm:ss.fffzzz";
        private string SearchScope = "All";
        private string SearchTerm = string.Empty;
        private CompareMode Mode = CompareMode.VALUE;

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
            if (Mode == CompareMode.VALUE)
                return (src.ToString(DateTimeFormat));
            else
            {
                var status = false;
                DateTime dt;
                var y = double.NaN;
                var m = double.NaN;
                var d = double.NaN;
                var w = double.NaN;
                var h = double.NaN;
                var n = double.NaN;
                var s = double.NaN;
                var ms = double.NaN;

                if (DateTime.TryParse(dst, out dt))
                {
                    // YYYY-MM-DDTHH:MM:SS
                    if (Regex.IsMatch(dst, @"^\d{4}(/-, 年)?\d{2}(/-, 月)?\d{2}日?[ T]?\d{2}[点时/,:_- ]\d{2}[分/,:_- ]\d{2}秒?$", RegexOptions.IgnoreCase))
                    {
                        y = dt.Year;
                        m = dt.Month;
                        d = dt.Day;
                        h = dt.Hour;
                        n = dt.Minute;
                        s = dt.Second;
                    }
                    // YYYY-MM-DDTHH:MM
                    else if (Regex.IsMatch(dst, @"^\d{4}(/-, 年)?\d{2}(/-, 月)?\d{2}日?[ T]?\d{2}[点时/,:_- ]\d{2}分?$", RegexOptions.IgnoreCase))
                    {
                        y = dt.Year;
                        m = dt.Month;
                        d = dt.Day;
                        h = dt.Hour;
                        n = dt.Minute;
                    }
                    // YYYY-MM-DDTMM:SSs
                    else if (Regex.IsMatch(dst, @"^\d{4}(/-, 年)?\d{2}(/-, 月)?\d{2}日?[ T]?\d{2}[分/,:_- ]\d{2}[s秒]$", RegexOptions.IgnoreCase))
                    {
                        y = dt.Year;
                        m = dt.Month;
                        d = dt.Day;
                        n = dt.Minute;
                        s = dt.Second;
                    }
                    // YYYY-MM-DDTHHh
                    else if (Regex.IsMatch(dst, @"^\d{4}(/-, 年)?\d{2}(/-, 月)?\d{2}日?[ T]?\d{2}[h点时]$", RegexOptions.IgnoreCase))
                    {
                        y = dt.Year;
                        m = dt.Month;
                        d = dt.Day;
                        h = dt.Hour;
                    }
                    // YYYY-MM-DDTMMm
                    else if (Regex.IsMatch(dst, @"^\d{4}(/-, 年)?\d{2}(/-, 月)?\d{2}日?[ T]?\d{2}[m分]$", RegexOptions.IgnoreCase))
                    {
                        y = dt.Year;
                        m = dt.Month;
                        d = dt.Day;
                        n = dt.Minute;
                    }
                    // YYYY-MM-DDTSSs
                    else if (Regex.IsMatch(dst, @"^\d{4}(/-, 年)?\d{2}(/-, 月)?\d{2}日?[ T]?\d{2}[s秒]$", RegexOptions.IgnoreCase))
                    {
                        y = dt.Year;
                        m = dt.Month;
                        d = dt.Day;
                        s = dt.Second;
                    }

                    // YYYY-MMTHH:MM:SS
                    else if (Regex.IsMatch(dst, @"^\d{4}(/-, 年)?\d{2}(/-, 月)?[ T]?\d{2}[点时/,:_- ]\d{2}[分/,:_- ]\d{2}秒?$", RegexOptions.IgnoreCase))
                    {
                        y = dt.Year;
                        m = dt.Month;
                        h = dt.Hour;
                        n = dt.Minute;
                        s = dt.Second;
                    }
                    // YYYY-MMTHH:MM
                    else if (Regex.IsMatch(dst, @"^\d{4}(/-, 年)?\d{2}(/-, 月)?[ T]?\d{2}[点时/,:_- ]\d{2}[分/,:_- ]?$", RegexOptions.IgnoreCase))
                    {
                        y = dt.Year;
                        m = dt.Month;
                        h = dt.Hour;
                        n = dt.Minute;
                    }
                    // YYYY-MMTMM:SSs
                    else if (Regex.IsMatch(dst, @"^\d{4}(/-, 年)?\d{2}(/-, 月)?[ T]?\d{2}[分/,:_- ]\d{2}[s秒]$", RegexOptions.IgnoreCase))
                    {
                        y = dt.Year;
                        m = dt.Month;
                        n = dt.Minute;
                        s = dt.Second;
                    }

                    // YYYY-DDdTHH:MM:SS
                    else if (Regex.IsMatch(dst, @"^\d{4}(/-, 年)?\d{2}[d日][ T]?\d{2}[点时/,:_- ]\d{2}[分/,:_- ]\d{2}秒?$", RegexOptions.IgnoreCase))
                    {
                        y = dt.Year;
                        d = dt.Day;
                        h = dt.Hour;
                        n = dt.Minute;
                        s = dt.Second;
                    }
                    // YYYY-DDdTHH:MM:SS
                    else if (Regex.IsMatch(dst, @"^\d{4}(/-, 年)?\d{2}[d日][ T]?\d{2}[点时/,:_- ]\d{2}[分/,:_- ]?$", RegexOptions.IgnoreCase))
                    {
                        y = dt.Year;
                        d = dt.Day;
                        h = dt.Hour;
                        n = dt.Minute;
                    }
                    else if (Regex.IsMatch(dst, @"^\d{4}(/-, 年)?\d{2}[d日][ T]?\d{2}[分/,:_- ]\d{2}秒?$", RegexOptions.IgnoreCase))
                    {
                        y = dt.Year;
                        d = dt.Day;
                        n = dt.Minute;
                        s = dt.Second;
                    }

                    else if (Regex.IsMatch(dst, @"^\d{4}(/-, 年)?\d{2}(/-, 月)?\d{2}日?[ T]?\d{2}[点时/,:_- ]\d{2}[分/,:_- ]\d{2}秒?$", RegexOptions.IgnoreCase))
                    {
                        y = dt.Year;
                        m = dt.Month;
                        d = dt.Day;
                        h = dt.Hour;
                        n = dt.Minute;
                        s = dt.Second;
                    }

                    else if (Regex.IsMatch(dst, @"^\d{4}(/-, 年)?\d{2}[d日][ T]?\d{2}[点时/,:_- ]\d{2}[分/,:_- ]\d{2}秒?$", RegexOptions.IgnoreCase))
                    {
                        y = dt.Year;
                        d = dt.Day;
                        h = dt.Hour;
                        n = dt.Minute;
                        s = dt.Second;
                    }
                    else if (Regex.IsMatch(dst, @"^\d{2}(/-, 月)?\d{2}日?[ T]?\d{2}[点时/,:_- ]\d{2}[分/,:_- ]\d{2}秒?$", RegexOptions.IgnoreCase))
                    {
                        m = dt.Month;
                        d = dt.Day;
                        h = dt.Hour;
                        n = dt.Minute;
                        s = dt.Second;
                    }

                    //else status = Compare(src, dt);
                }
                else
                {
                    //if (Regex.IsMatch(word.)
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
                            if (cats.Contains("date")) status = status || Compare(date, word);
                        }
                        else if (Mode == CompareMode.LT)
                        {
                            var rate_value = string.IsNullOrEmpty(rate) ? rate : rate.PadLeft(16, '0');
                            var rank_value = string.IsNullOrEmpty(rank) ? rank : rank.PadLeft(16, '0');
                            DateTime date_value = default(DateTime);
                            if (!string.IsNullOrEmpty(date)) DateTime.TryParse(date, out date_value);
                            int word_int;
                            var word_value = int.TryParse(word, out word_int) ? word.PadLeft(16, '0') : word;

                            if (cats.Contains("rate")) status = status || Compare(rate_value, word_value);
                            if (cats.Contains("rank")) status = status || Compare(rank_value, word_value);
                            if (cats.Contains("date"))
                            {

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

                    ret = GetReturnValueByStatus(status);
                    result = (T)(object)status;
                }
            }
            catch (Exception ex) { ShowMessage(ex, Name); }
            return (ret);
        }

    }
}
