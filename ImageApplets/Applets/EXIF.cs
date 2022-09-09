using System;
using System.Collections.Generic;
using System.Globalization;
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
            private string _text_ { get; set; } = string.Empty;

            internal protected double y { get; set; } = double.NaN;
            internal protected double m { get; set; } = double.NaN;
            internal protected double d { get; set; } = double.NaN;
            internal protected double w { get; set; } = double.NaN;
            internal protected double h { get; set; } = double.NaN;
            internal protected double n { get; set; } = double.NaN;
            internal protected double s { get; set; } = double.NaN;

            internal protected int? Year
            {
                get { return ((double.IsNaN(y) ? null : (int?)Math.Max(01, y) % 9999)); }
            }
            internal protected int? Month
            {
                get { return ((double.IsNaN(m) ? null : (int?)Math.Max(01, m) % 0012)); }
            }
            internal protected int? Day
            {
                get { return ((double.IsNaN(d) ? null : (int?)Math.Max(01, d) % 0031)); }
            }
            internal protected int? Hour
            {
                get { return ((double.IsNaN(h) ? null : (int?)Math.Max(00, h) % 0024)); }
            }
            internal protected int? Minute
            {
                get { return ((double.IsNaN(n) ? null : (int?)Math.Max(00, n) % 0060)); }
            }
            internal protected int? Sewcond
            {
                get { return ((double.IsNaN(s) ? null : (int?)Math.Max(00, s) % 0060)); }
            }
            internal protected DayOfWeek? WeekDay
            {
                get { return ((double.IsNaN(w) ? null : (DayOfWeek?)(Math.Max(1, w) % 7))); }
            }

            private DateTime? _date_ref_ = null;
            internal protected DateTime Refrence
            {
                get { return (_date_ref_ ?? DateTime.Now); }
            }
            internal protected DateTime? Date
            {
                get { return (GetDateTime(Refrence)); }
            }
            internal protected DayOfWeek? DayOfWeek
            {
                get { return (GetWeekDay(Refrence)); }
            }

            internal protected DateTime? GetDateTime(DateTime src)
            {
                DateTime? result = null;
                try
                {
                    var dy = (int)(!double.IsNaN(y) ? y : src.Year);
                    var dm = (int)(!double.IsNaN(m) ? m : src.Month);
                    var dd = (int)(!double.IsNaN(d) ? d : src.Day);
                    var dh = (int)(!double.IsNaN(h) ? h : src.Hour);
                    var dn = (int)(!double.IsNaN(n) ? n : src.Minute);
                    var ds = (int)(!double.IsNaN(s) ? s : src.Second);
                    var dt = new DateTime(dy, dm, dd, dh, dn, ds);
                    result = dt;
                }
                catch { }
                return (result);
            }

            internal protected DayOfWeek? GetWeekDay(DateTime src)
            {
                return ((double.IsNaN(w) ? src.DayOfWeek : (DayOfWeek)Enum.Parse(typeof(DayOfWeek), $"{w - 1}")));
            }

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
                    if (Regex.IsMatch(_text_, @"^(\d{2,4})[/\-,\. 年](\d{1,2})[/\-,\. 月](\d{1,2})日?", RegexOptions.IgnoreCase))
                    {
                        var match = Regex.Match(_text_, @"^(\d{2,4})[/-,\. 年](\d{1,2})[/-,\. 月](\d{1,2})日?", RegexOptions.IgnoreCase);
                        if (match.Groups[1].Success) y = Convert.ToInt32(match.Groups[1].Value.Trim());
                        if (match.Groups[2].Success) m = Convert.ToInt32(match.Groups[2].Value.Trim());
                        if (match.Groups[3].Success) d = Convert.ToInt32(match.Groups[3].Value.Trim());
                    }
                    else if (Regex.IsMatch(_text_, @"^(\d{2,4})[/\-,\. 年](\d{1,2})[/\-,\. 月]?", RegexOptions.IgnoreCase))
                    {
                        var match = Regex.Match(_text_, @"^(\d{2,4})[/\-,\. 年](\d{1,2})[/\-,\. 月]?", RegexOptions.IgnoreCase);
                        if (match.Groups[1].Success) y = Convert.ToInt32(match.Groups[1].Value.Trim());
                        if (match.Groups[2].Success) m = Convert.ToInt32(match.Groups[2].Value.Trim());
                    }
                    else if (Regex.IsMatch(_text_, @"^(\d{1,2})[/\-,\. 月](\d{1,2})日?", RegexOptions.IgnoreCase))
                    {
                        var match = Regex.Match(_text_, @"^(\d{1,2})[/\-,\. 月](\d{1,2})日?", RegexOptions.IgnoreCase);
                        if (match.Groups[1].Success) m = Convert.ToInt32(match.Groups[1].Value.Trim());
                        if (match.Groups[2].Success) d = Convert.ToInt32(match.Groups[2].Value.Trim());
                    }
                    else
                    {
                        if (Regex.IsMatch(_text_, @"(\d{2,4})(y(ear)?|nian|年)", RegexOptions.IgnoreCase))
                        {
                            var match = Regex.Match(_text_, @"(\d{2,4})(y(ear)?|nian|年)", RegexOptions.IgnoreCase);
                            if (match.Groups[1].Success) y = Convert.ToInt32(match.Groups[1].Value.Trim());
                        }
                        else if (Regex.IsMatch(_text_, @"(\d{4})(y(ear)?|nian|年)?", RegexOptions.IgnoreCase))
                        {
                            var match = Regex.Match(_text_, @"(\d{4})(y(ear)?|nian|年)?", RegexOptions.IgnoreCase);
                            if (match.Groups[1].Success) y = Convert.ToInt32(match.Groups[1].Value.Trim());
                        }
                        if (Regex.IsMatch(_text_, @"(\d{1,2})(mo(nth)?|yue|月)", RegexOptions.IgnoreCase))
                        {
                            var match = Regex.Match(_text_, @"(\d{1,2})(mo(nth)?|yue|月)", RegexOptions.IgnoreCase);
                            if (match.Groups[1].Success) m = Convert.ToInt32(match.Groups[1].Value.Trim());
                        }
                        if (Regex.IsMatch(_text_, @"(\d{1,2})(d(ay)?|ri|日|号)", RegexOptions.IgnoreCase))
                        {
                            var match = Regex.Match(_text_, @"(\d{1,2})(d(ay)?|ri|日|号)", RegexOptions.IgnoreCase);
                            if (match.Groups[1].Success) d = Convert.ToInt32(match.Groups[1].Value.Trim());
                        }
                        if (Regex.IsMatch(_text_, @"(星期|周|zhou|xq)?(\d)(w(eek(day)?)?)?", RegexOptions.IgnoreCase))
                        {
                            var match = Regex.Match(_text_, @"(星期|周|zhou|xq)?(\d)(w(eek(day)?)?)?", RegexOptions.IgnoreCase);
                            if ((match.Groups[1].Success || match.Groups[3].Success) && match.Groups[2].Success) w = Convert.ToInt32(match.Groups[2].Value.Trim());
                        }
                    }
                    if (!double.IsNaN(y) && y <= 0) y = double.NaN;
                    if (!double.IsNaN(m) && (m <= 0 || m > 12)) m = double.NaN;
                    if (!double.IsNaN(d) && (d <= 0 || d > 31)) d = double.NaN;
                    if (!double.IsNaN(w) && (w <= 0 || w > 07)) w = double.NaN;
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
                        if (Regex.IsMatch(_text_, @"(\d{1,2})(h(our)?|shi|点|小?时)", RegexOptions.IgnoreCase))
                        {
                            var match = Regex.Match(_text_, @"(\d{1,2})(h(our)?|shi|点|小?时)", RegexOptions.IgnoreCase);
                            if (match.Groups[1].Success) h = Convert.ToInt32(match.Groups[1].Value.Trim());
                        }
                        if (Regex.IsMatch(_text_, @"(\d{1,2})(n|min(ute)?|fen|分钟?)", RegexOptions.IgnoreCase))
                        {
                            var match = Regex.Match(_text_, @"(\d{1,2})(n|min(ute)?|fen|分钟?)", RegexOptions.IgnoreCase);
                            if (match.Groups[1].Success) n = Convert.ToInt32(match.Groups[1].Value.Trim());
                        }
                        if (Regex.IsMatch(_text_, @"(\d{1,2})(s(ec(ond)?)?|miao|秒)", RegexOptions.IgnoreCase))
                        {
                            var match = Regex.Match(_text_, @"(\d{1,2})(s(ec(ond)?)?|miao|秒)", RegexOptions.IgnoreCase);
                            if (match.Groups[1].Success) s = Convert.ToInt32(match.Groups[1].Value.Trim());
                        }
                    }
                    if (!double.IsNaN(h) && (h < 0 || h > 23)) h = double.NaN;
                    if (!double.IsNaN(n) && (n < 0 || n > 59)) n = double.NaN;
                    if (!double.IsNaN(s) && (s < 0 || s > 59)) s = double.NaN;
                    #endregion
                }
            }
        }

        public override Applet GetApplet()
        {
            return (new HasExif());
        }

        static private Dictionary<string, string> num_chs = new Dictionary<string, string>()
        {
            { "一", "1" }, { "二", "2" }, { "三", "3" }, { "四", "4" }, { "五", "5" }, { "六", "6" }, { "七", "7" }, { "八", "8" }, { "九", "9" }, { "零", "0" },
            { "壹", "1" }, { "贰", "2" }, { "叁", "3" }, { "肆", "4" }, { "伍", "5" }, { "陆", "6" }, { "柒", "7" }, { "捌", "8" }, { "玖", "9" },
        };

        private string[] Categories  = new string[] { "Artist", "Author", "Title", "Suject", "Comment", "Keyword", "Keywords", "Tag", "Tags", "Copyright", "Software", "Rate", "Date", "All" };
        private string DateTimeFormat = $"yyyy-MM-dd HH:mm:ss.fffzzz";
        private string DateTimeFormatLocal = $"{CultureInfo.CurrentCulture.DateTimeFormat.LongDatePattern}, ddd";
        private char[] SplitChar = new char[] { '#', ';' };
        private char[] RegexTrimChar = new char[] { 'i', '/' };
        private string IsRegexPattern = @"/(.+?)/i?";

        private DateValue _date_ = null;

        private string SearchScope = "All";
        private string SearchTerm = string.Empty;
        private CompareMode Mode = CompareMode.VALUE;

        public EXIF()
        {
            Category = AppletCategory.ImageAttribure;

            var opts = new OptionSet()
            {
                { "m|mode=", "EXIF Search Mode {<EQ|NEQ|LT|LE|GT|GE|AND|OR|NOT|VALUE>}", v => { if (v != null) Enum.TryParse(v.ToUpper(), out Mode); } },
                { "c|category=", $"EXIF Search Fron {{<{string.Join("|", Categories)}>}}", v => { if (v != null) SearchScope = v.Trim().Trim('"'); } },
                { "s|search=", "EXIF Search {Term}", v => { if (v != null) SearchTerm = v.Trim().Trim('"'); } },
                { "" },
            };
            AppendOptions(opts);
        }

        public override List<string> ParseOptions(IEnumerable<string> args)
        {
            var extras = base.ParseOptions(args);

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

        private DateTime? GetDateTime(ExifData exif, ExifTag tag)
        {
            DateTime? result = null;
            if (exif is ExifData && exif.TagExists(tag))
            {
                try
                {
                    DateTime value;
                    exif.GetTagValue(tag, out value);
                    result = value;
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
                        DateTime? value = GetDateTime(exif, tag);
                        result = GetDateLong(value);
                    }
                }
                catch { }
            }
            return (result);
        }

        private string GetDateLong(DateTime date)
        {
            return (date.ToString($"{DateTimeFormat}, {DateTimeFormatLocal}"));
        }

        private string GetDateLong(DateTime? date)
        {
            return (date.HasValue ? date.Value.ToString($"{DateTimeFormat}, {DateTimeFormatLocal}") : string.Empty);
        }

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
                if (Regex.IsMatch(word, IsRegexPattern, RegexOptions.IgnoreCase))
                {
                    regex_ignore = ignorecase == null || word.EndsWith("/i", StringComparison.CurrentCultureIgnoreCase) ? RegexOptions.IgnoreCase : RegexOptions.None;
                    word = word.Trim(RegexTrimChar);
                }
                switch (Mode)
                {
                    case CompareMode.AND:
                        status = Regex.IsMatch(text, word, regex_ignore);
                        break;
                    case CompareMode.OR:
                        status = Regex.IsMatch(text, word, regex_ignore);
                        break;
                    case CompareMode.NOT:
                        status = !Regex.IsMatch(text, word, regex_ignore);
                        break;
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

        private dynamic Compare(string text, string[] words, bool? ignorecase = null)
        {
            if (Mode == CompareMode.VALUE)
                return (text);
            else
            {
                var status = false;
                foreach (var word in words.Select(w => w.Trim()))
                {
                    switch (Mode)
                    {
                        case CompareMode.AND:
                            status = status || !Compare(text, word, ignorecase);
                            status = !status;
                            break;
                        case CompareMode.OR:
                            status = status || Compare(text, word, ignorecase);
                            break;
                        case CompareMode.NOT:
                            status = status || Compare(text, word, ignorecase);
                            status = !status;
                            break;
                        case CompareMode.EQ:
                            status = status || Compare(text, word, ignorecase);
                            break;
                        case CompareMode.NEQ:
                            status = status || Compare(text, word, ignorecase);
                            status = !status;
                            break;
                        case CompareMode.HAS:
                            status = status || Compare(text, word, ignorecase);
                            break;
                        case CompareMode.NONE:
                            status = status || Compare(text, word, ignorecase);
                            status = !status;
                            break;
                        default:
                            break;
                    }
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
                    var dt = dst.GetDateTime(src);
                    if (dt == null) dt = ConvertFromDateValue(src, dst);
                    var wd = dst.WeekDay ?? (double.IsNaN(dst.w) ? src.DayOfWeek : (DayOfWeek)Enum.Parse(typeof(DayOfWeek), $"{dst.w - 1}"));

                    switch (Mode)
                    {
                        case CompareMode.HAS:
                            if (!double.IsNaN(dst.y)) status = status || dst.y == src.Year;
                            if (!double.IsNaN(dst.m)) status = status || dst.m == src.Month;
                            if (!double.IsNaN(dst.d)) status = status || dst.d == src.Day;
                            if (!double.IsNaN(dst.h)) status = status || dst.h == src.Hour;
                            if (!double.IsNaN(dst.n)) status = status || dst.n == src.Minute;
                            if (!double.IsNaN(dst.s)) status = status || dst.s == src.Second;
                            if (!double.IsNaN(dst.w)) status = status || dst.WeekDay == src.DayOfWeek;
                            break;
                        case CompareMode.NONE:
                            if (!double.IsNaN(dst.y)) status = status || dst.y == src.Year;
                            if (!double.IsNaN(dst.m)) status = status || dst.m == src.Month;
                            if (!double.IsNaN(dst.d)) status = status || dst.d == src.Day;
                            if (!double.IsNaN(dst.h)) status = status || dst.h == src.Hour;
                            if (!double.IsNaN(dst.n)) status = status || dst.n == src.Minute;
                            if (!double.IsNaN(dst.s)) status = status || dst.s == src.Second;
                            if (!double.IsNaN(dst.w)) status = status || dst.WeekDay == src.DayOfWeek;
                            status = !status;
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

                    DateTime? date = GetDateTime(exif, ExifTag.GpsDateStamp);
                    if (date == null) date = GetDateTime(exif, ExifTag.DateTimeOriginal);
                    if (date == null) date = GetDateTime(exif, ExifTag.DateTimeDigitized);
                    if (date == null) date = GetDateTime(exif, ExifTag.DateTime);
                    if (date == null) date = exif.LastWriteTime;
                    if (date == null) date = exif.CreateTime;
                    if (date == null) date = exif.LastAccessTime;
                    var date_string = GetDateLong(date);
                    #endregion

                    var cats = SearchScope.Split(',').Select(c => c.Trim().ToLower()).ToList();
                    if (!string.IsNullOrEmpty(SearchTerm))
                    {
                        var word = SearchTerm;
                        var words = Regex.IsMatch(word, IsRegexPattern, RegexOptions.IgnoreCase) ?  word.Trim(RegexTrimChar).Split(SplitChar) : word.Split(SplitChar);

                        if(_date_ == null) _date_ = new DateValue(ConvertChineseNumberString(word));

                        if (Mode == CompareMode.VALUE) Mode = CompareMode.HAS;

                        #region Comparing attribute
                        if (cats.Contains("all"))
                        {
                            cats.AddRange(Categories);
                            cats = cats.Select(c => c.Trim().ToLower()).Distinct().ToList();
                        }

                        if (Mode == CompareMode.AND)
                        {
                            if (cats.Contains("title")) status = (status || cats.Contains("title")) && Compare(title, word);
                            if (cats.Contains("subject")) status = (status || cats.Contains("subject")) && Compare(subject, word);

                            if (cats.Contains("keyword")) status = (status || cats.Contains("keyword")) && Compare(keywords, words);
                            if (cats.Contains("keywords")) status = (status || cats.Contains("keywords")) && Compare(keywords, words);
                            if (cats.Contains("tag")) status = (status || cats.Contains("tag")) && Compare(keywords, words);
                            if (cats.Contains("tags")) status = (status || cats.Contains("tags")) && Compare(keywords, words);

                            if (cats.Contains("comments")) status = (status || cats.Contains("comments")) && Compare(comments, word);

                            if (cats.Contains("artist")) status = (status || cats.Contains("artist")) && Compare(artist, words);
                            if (cats.Contains("author")) status = (status || cats.Contains("author")) && Compare(artist, words);
                            if (cats.Contains("copyright")) status = (status || cats.Contains("copyright")) && Compare(copyright, words);

                            if (cats.Contains("software")) status = (status || cats.Contains("software")) && Compare(software, word);
                            if (cats.Contains("rate")) status = (status || cats.Contains("rate")) && Compare(rate, word);
                            if (cats.Contains("rank")) status = (status || cats.Contains("rank")) && Compare(rank, word);
                            if (cats.Contains("date")) status = (status || cats.Contains("date")) && Compare(date_string, word);
                        }
                        else if (Mode == CompareMode.OR || Mode == CompareMode.NOT)
                        {
                            if (cats.Contains("title")) status = status || Compare(title, word);
                            if (cats.Contains("subject")) status = status || Compare(subject, word);

                            if (cats.Contains("keyword")) status = status || Compare(keywords, words);
                            if (cats.Contains("keywords")) status = status || Compare(keywords, words);
                            if (cats.Contains("tag")) status = status || Compare(keywords, words);
                            if (cats.Contains("tags")) status = status || Compare(keywords, words);

                            if (cats.Contains("comments")) status = status || Compare(comments, word);

                            if (cats.Contains("artist")) status = status || Compare(artist, words);
                            if (cats.Contains("author")) status = status || Compare(artist, words);
                            if (cats.Contains("copyright")) status = status || Compare(copyright, words);

                            if (cats.Contains("software")) status = status || Compare(software, word);
                            if (cats.Contains("rate")) status = status || Compare(rate, word);
                            if (cats.Contains("rank")) status = status || Compare(rank, word);
                            if (cats.Contains("date")) status = status || Compare(date_string, word);
                            if (Mode == CompareMode.NOT) status = !status;
                        }
                        else if (Mode == CompareMode.LT || Mode == CompareMode.LE ||
                                 Mode == CompareMode.GT || Mode == CompareMode.GE ||
                                 Mode == CompareMode.EQ || Mode == CompareMode.NEQ ||
                                 Mode == CompareMode.HAS || Mode == CompareMode.NONE)
                        {
                            if (cats.Contains("title")) status = status || Compare(title, word);
                            if (cats.Contains("subject")) status = status || Compare(subject, word);

                            if (cats.Contains("keyword")) status = status || Compare(keywords, words);
                            if (cats.Contains("keywords")) status = status || Compare(keywords, words);
                            if (cats.Contains("tag")) status = status || Compare(keywords, words);
                            if (cats.Contains("tags")) status = status || Compare(keywords, words);

                            if (cats.Contains("comments")) status = status || Compare(comments, word);

                            if (cats.Contains("artist")) status = status || Compare(artist, words);
                            if (cats.Contains("author")) status = status || Compare(artist, words);
                            if (cats.Contains("copyright")) status = status || Compare(copyright, words);

                            if (cats.Contains("software")) status = status || Compare(software, word);
                            if (cats.Contains("rate")) status = status || Compare(rate, word);
                            if (cats.Contains("rank")) status = status || Compare(rank, word);

                            int word_int;
                            var word_value = int.TryParse(word, out word_int) ? word.PadLeft(16, '0') : word;

                            var rate_value = string.IsNullOrEmpty(rate) ? rate : rate.PadLeft(16, '0');
                            var rank_value = string.IsNullOrEmpty(rank) ? rank : rank.PadLeft(16, '0');

                            if (cats.Contains("rate")) status = status || Compare(rate_value, word_value);
                            if (cats.Contains("rank")) status = status || Compare(rank_value, word_value);
                            if (cats.Contains("date") && date.HasValue) status = status || Compare(date.Value, _date_);
                        }
                    }
                    else
                    {
                        var padding = "".PadLeft(ValuePaddingLeft);
                        StringBuilder sb = new StringBuilder();
                        sb.Append("\u20D0");
                        if (cats.Contains("title") && !string.IsNullOrEmpty(title)) sb.AppendLine($"{padding}{title}");
                        if (cats.Contains("subject") && !string.IsNullOrEmpty(subject)) sb.AppendLine($"{padding}{subject}");
                        if (cats.Contains("keyword") && !string.IsNullOrEmpty(keywords)) sb.AppendLine($"{padding}{keywords}");
                        else if (cats.Contains("keywords") && !string.IsNullOrEmpty(keywords)) sb.AppendLine($"{padding}{keywords}");
                        else if (cats.Contains("tag") && !string.IsNullOrEmpty(keywords)) sb.AppendLine($"{padding}{keywords}");
                        else if (cats.Contains("tags") && !string.IsNullOrEmpty(keywords)) sb.AppendLine($"{padding}{keywords}");
                        if (cats.Contains("comments") && !string.IsNullOrEmpty(comments)) sb.AppendLine($"{padding}{comments}");
                        if (cats.Contains("artist") && !string.IsNullOrEmpty(artist)) sb.AppendLine($"{padding}{artist}");
                        else if (cats.Contains("author") && !string.IsNullOrEmpty(artist)) sb.AppendLine($"{padding}{artist}");
                        else if (cats.Contains("authors") && !string.IsNullOrEmpty(artist)) sb.AppendLine($"{padding}{artist}");
                        if (cats.Contains("copyright") && !string.IsNullOrEmpty(copyright)) sb.AppendLine($"{padding}{copyright}");
                        if (cats.Contains("copyrights") && !string.IsNullOrEmpty(copyright)) sb.AppendLine($"{padding}{copyright}");
                        if (cats.Contains("software") && !string.IsNullOrEmpty(software)) sb.AppendLine($"{padding}{software}");
                        if (cats.Contains("rate") && !string.IsNullOrEmpty(rate)) sb.AppendLine($"{padding}{rate}");
                        if (cats.Contains("rank") && !string.IsNullOrEmpty(rank)) sb.AppendLine($"{padding}{rank}");
                        if (cats.Contains("date") && !string.IsNullOrEmpty(date_string)) sb.AppendLine($"{padding}{date_string}");
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
