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
        public override Applet GetApplet()
        {
            return (new HasExif());
        }

        private List<string> Categories  = new List<string>() { "Artist", "Author", "Title", "Suject", "Comment", "Comments", "Keyword", "Keywords", "Tag", "Tags", "Copyright", "Software", "Rate", "Rating", "Rank", "Ranking", "Date", "Width", "Height", "Aspect", "Landscape", "Portrait", "Square", "Bits", "Endian", "LittleEndian", "LSB", "BigEndian", "MSB", "All" };
        private string[] ExifAttrs = new string[] { };

        private DateValue _date_ = null;

        private int MaxLength = 64;
        private string SearchScope = "All";
        private string SearchTerm = string.Empty;
        private CompareMode Mode = CompareMode.VALUE;

        public EXIF()
        {
            Category = AppletCategory.ImageAttribure;

            var opts = new OptionSet()
            {
                { "m|mode=", "EXIF Search Mode {VALUE} : <EQ|NEQ|LT|LE|GT|GE|IN|OUT|AND|OR|NOT>", v => { if (v != null) Enum.TryParse(v.ToUpper(), out Mode); } },
                { "c|category=", $"EXIF Search From {{VALUE}} : <{string.Join("|", Categories)}> And more EXIF Tag. Note: Support '*'.", v => { if (v != null) SearchScope = v.Trim().Trim('"'); } },
                { "l|limit|length=", $"EXIF Value Max Length Limit {{VALUE}}", v => { if (v != null) int.TryParse(v, out MaxLength); } },
                { "s|search=", "EXIF Search {Term}, multiple serach keywords seprated by ';' or '#'.", v => { if (v != null) SearchTerm = v.Trim().Trim('"'); } },
                { "" },
            };
            AppendOptions(opts);

            ExifAttrs = Enum.GetNames(typeof(ExifTag));
        }

        public override List<string> ParseOptions(IEnumerable<string> args)
        {
            var extras = base.ParseOptions(args);

            _date_ = new DateValue(SearchTerm);

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

        private string GetIntString(ExifData exif, ExifTag tag, bool raw = false)
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

        private string GetTagValue(ExifData exif, string attr)
        {
            var result = string.Empty;
            try
            {
                attr = ExifAttrs.Where(a => a.Equals(attr, StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();
                if (!string.IsNullOrEmpty(attr))
                {
                    var TagSpec = (ExifTag)Enum.Parse(typeof(ExifTag), attr);
                    if (exif is ExifData && exif.TagExists(TagSpec))
                    {
                        result = GetTagValue(exif, TagSpec);
                    }
                }
            }
            catch { }
            return (result);
        }

        private string GetTagValue(ExifData exif, ExifTag TagSpec)
        {
            var result = string.Empty;
            try
            {
                if (exif is ExifData && exif.TagExists(TagSpec))
                {
                    ExifTagType TagType;
                    //ExifTagId TagId;
                    int ValueCount;//, TagDataByteCount;
                    byte[] TagData;
                    string s = string.Empty;

                    if (exif.GetTagRawData(TagSpec, out TagType, out ValueCount, out TagData))
                    {
                        int TagValueCount;
                        int TagIntValue;
                        uint TagUIntValue;
                        ExifRational TagRationalValue;

                        exif.GetTagValueCount(TagSpec, out TagValueCount);

                        if (TagSpec == ExifTag.DateTime || TagSpec == ExifTag.DateTimeDigitized || TagSpec == ExifTag.DateTimeOriginal)
                        {
                            DateTime dt;
                            exif.GetTagValue(TagSpec, out dt);
                            s = GetDateLong(dt);
                        }
                        else if(TagSpec == ExifTag.GpsDateStamp || TagSpec == ExifTag.GpsTimeStamp)
                        {
                            DateTime dt;
                            exif.GetGpsDateTimeStamp(out dt);
                            dt = dt.ToLocalTime();
                            s = GetDateLong(dt);
                        }
                        else if (TagType == ExifTagType.Ascii)
                        {
                            exif.GetTagValue(TagSpec, out s, StrCoding.Utf8);
                        }
                        else if (TagType == ExifTagType.Byte && TagSpec == ExifTag.XmpMetadata)
                        {
                            s = Encoding.UTF8.GetString(TagData);
                        }
                        else if ((TagType == ExifTagType.Byte) && ((TagSpec == ExifTag.XpTitle) || (TagSpec == ExifTag.XpComment) || (TagSpec == ExifTag.XpAuthor) ||
                                 (TagSpec == ExifTag.XpKeywords) || (TagSpec == ExifTag.XpSubject)))
                        {
                            exif.GetTagValue(TagSpec, out s, StrCoding.Utf16Le_Byte);
                        }
                        else if ((TagType == ExifTagType.Undefined) && (TagSpec == ExifTag.UserComment))
                        {
                            exif.GetTagValue(TagSpec, out s, StrCoding.IdCode_Utf16);
                            s.Trim('\0').TrimEnd();
                        }
                        else if ((TagType == ExifTagType.Undefined) && ((TagSpec == ExifTag.ExifVersion) || (TagSpec == ExifTag.FlashPixVersion) ||
                                 (TagSpec == ExifTag.InteroperabilityVersion)))
                        {
                            exif.GetTagValue(TagSpec, out s, StrCoding.UsAscii_Undef);
                        }
                        else if ((TagType == ExifTagType.Undefined) && ((TagSpec == ExifTag.SceneType) || (TagSpec == ExifTag.FileSource)))
                        {
                            if (TagData.Length > 0) s += TagData[0].ToString();
                        }
                        else if ((TagType == ExifTagType.Undefined) && ((TagSpec == ExifTag.MakerNote) || (TagSpec == ExifTag.CameraOwnerName) || (TagSpec == ExifTag.GpsProcessingMethod)))
                        {
                            if (TagData.Length > 0) s = Encoding.UTF8.GetString(TagData);
                            var r = s.Split(new string[]{ "\0" }, StringSplitOptions.RemoveEmptyEntries).Distinct();
                            s = string.Join(Environment.NewLine, r);
                        }
                        else if ((TagType == ExifTagType.Undefined) && ((TagSpec == ExifTag.ComponentsConfiguration)))
                        {
                            if (TagData.Length > 0) s = string.Join(", ", TagData.Select(b => $"{b.ToString()}"));
                            var r = s.Split(new string[]{ "\0" }, StringSplitOptions.RemoveEmptyEntries).Distinct();
                            s = string.Join(Environment.NewLine, r);
                        }

                        else if ((TagType == ExifTagType.Undefined) && ((TagSpec == ExifTag.LensMake) || (TagSpec == ExifTag.LensModel)))
                        {
                            if (TagData.Length > 0) s = Encoding.UTF8.GetString(TagData);
                        }
                        else if ((TagType == ExifTagType.Byte) || (TagType == ExifTagType.UShort) || (TagType == ExifTagType.ULong))
                        {
                            for (int i = 0; i < TagValueCount; i++)
                            {
                                exif.GetTagValue(TagSpec, out TagIntValue, i);
                                if (i > 0) s += $", {TagIntValue.ToString()}";
                                else s = TagIntValue.ToString();
                            }
                        }
                        else if (TagType == ExifTagType.SLong)
                        {
                            for (int i = 0; i < TagValueCount; i++)
                            {
                                exif.GetTagValue(TagSpec, out TagUIntValue, i);
                                if (i > 0) s += $", {TagUIntValue.ToString()}";
                                else s = TagUIntValue.ToString();
                            }
                        }
                        else if ((TagType == ExifTagType.SRational) || (TagType == ExifTagType.URational))
                        {
                            for (int i = 0; i < TagValueCount; i++)
                            {
                                exif.GetTagValue(TagSpec, out TagRationalValue, i);
                                if (i > 0) s += $", {TagRationalValue.Numer / TagRationalValue.Denom:F2} [{TagRationalValue.ToString()}]";
                            }
                        }
                        result = s;
                    }
                }
            }
            catch { }
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

        private dynamic Compare(string text, string word, bool? ignorecase = null)
        {
            return (base.Compare(text, word, Mode, ignorecase));
        }

        private dynamic Compare(string text, string[] words, bool? ignorecase = null)
        {
            return (base.Compare(text, words, Mode, ignorecase));
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

                    var rate = GetIntString(exif, ExifTag.RatingPercent);
                    var rank = GetIntString(exif, ExifTag.Rating);

                    DateTime? date = GetDateTime(exif, ExifTag.GpsDateStamp);
                    if (date == null) date = GetDateTime(exif, ExifTag.DateTimeOriginal);
                    if (date == null) date = GetDateTime(exif, ExifTag.DateTimeDigitized);
                    if (date == null) date = GetDateTime(exif, ExifTag.DateTime);
                    if (date == null) date = exif.LastWriteTime;
                    if (date == null) date = exif.CreateTime;
                    if (date == null) date = exif.LastAccessTime;
                    var date_string = GetDateLong(date);
                    #endregion

                    var cats = SearchScope.Split(',').Select(c => c.Trim().ToLower()).Distinct().ToList();
                    IEnumerable<string> cats_exif = new List<string>();
                    foreach (var attr in cats.Except(Categories.Select(c => c.ToLower())))
                    {
                        var attr_n = $"^{attr.Replace("*", ".*")}$";
                        cats_exif = cats_exif.Concat(ExifAttrs.Where(a => Regex.IsMatch(a, attr_n, RegexOptions.IgnoreCase)));
                    }
                    cats_exif = cats_exif.Distinct();

                    if (!string.IsNullOrEmpty(SearchTerm))
                    {
                        #region Config comparing 
                        var word = SearchTerm;
                        var words_o = Regex.IsMatch(word, IsRegexPattern, RegexOptions.IgnoreCase) ?  word.Trim(RegexTrimChar).Split(SplitChar) : word.Split(SplitChar);
                        // words = words.Concat(words.ConvertChinese2Japanese()).Concat(words.ConvertJapanese2Chinese()).Distinct().ToArray();
                        var words_j = words_o.ConvertChinese2Japanese().ToArray();
                        var words_c = words_o.ConvertJapanese2Chinese().ToArray();

                        if (_date_ == null) _date_ = new DateValue(word);

                        if (Mode == CompareMode.VALUE) Mode = CompareMode.HAS;

                        if (cats.Contains("all"))
                        {
                            cats.AddRange(Categories);
                            cats = cats.Select(c => c.Trim().ToLower()).Distinct().ToList();
                        }
                        #endregion

                        foreach (var words in new List<string[]>() { words_o, words_j, words_c })
                        {
                            #region Comparing attribute
                            status = new CompareMode[] { CompareMode.AND, CompareMode.NOT, CompareMode.NONE }.Contains(Mode) ? true : false;
                            var invert = new CompareMode[]{ CompareMode.NOT, CompareMode.NEQ, CompareMode.NONE }.Contains(Mode) ? false : true;
                            if (Mode == CompareMode.AND || Mode == CompareMode.NOT)
                            {
                                if (cats.Contains("title")) status &= Compare(title, words);
                                if (cats.Contains("subject")) status &= Compare(subject, words);

                                if (cats.Contains("keyword")) status &= Compare(keywords, words);
                                if (cats.Contains("keywords")) status &= Compare(keywords, words);
                                if (cats.Contains("tag")) status &= Compare(keywords, words);
                                if (cats.Contains("tags")) status &= Compare(keywords, words);

                                if (cats.Contains("comment")) status &= Compare(comments, words);
                                if (cats.Contains("comments")) status &= Compare(comments, words);

                                if (cats.Contains("artist")) status &= Compare(artist, words);
                                else if (cats.Contains("author")) status &= Compare(artist, words);

                                if (cats.Contains("copyright")) status &= Compare(copyright, words);

                                if (cats.Contains("software")) status &= Compare(software, words);
                                if (cats.Contains("rate") || cats.Contains("rating")) status &= Compare(rate, words);
                                if (cats.Contains("rank") || cats.Contains("ranking")) status &= Compare(rank, words);
                                if (cats.Contains("date")) status &= Compare(date_string, words);

                                if (cats.Contains("width")) status &= Compare($"{exif.Width}", words);
                                if (cats.Contains("height")) status &= Compare($"{exif.Height}", words);
                                if (cats.Contains("aspect")) status &= Compare($"{exif.Width / exif.Height}", words);

                                if (cats.Contains("landscape")) status &= exif.Height / exif.Height > 1;
                                if (cats.Contains("portrait")) status &= exif.Height / exif.Height < 1;
                                if (cats.Contains("square")) status &= exif.Height / exif.Height == 1;

                                if (cats.Contains("bits")) status &= Compare($"{exif.ColorDepth}", words);
                                if (cats.Contains("endian")) status &= Compare($"{exif.ByteOrder}", words);

                                if (cats.Contains("littleendian")) status &= invert && exif.ByteOrder == ExifByteOrder.LittleEndian;
                                else if (cats.Contains("lsb")) status &= invert && exif.ByteOrder == ExifByteOrder.LittleEndian;

                                if (cats.Contains("bigendian")) status &= invert && exif.ByteOrder == ExifByteOrder.BigEndian;
                                else if (cats.Contains("msb")) status &= invert && exif.ByteOrder == ExifByteOrder.BigEndian;

                                foreach (var attr in cats_exif)
                                {
                                    try
                                    {
                                        var value = GetTagValue(exif, attr);
                                        if (cats.Count(c => c.Equals(attr, StringComparison.CurrentCultureIgnoreCase)) > 0 && !string.IsNullOrEmpty(value))
                                            status = (status || cats.Contains(attr)) && Compare(value, words);
                                    }
                                    catch { }
                                }
                            }
                            else if (Mode == CompareMode.OR)
                            {
                                if (cats.Contains("title")) status |= Compare(title, words);
                                if (cats.Contains("subject")) status |= Compare(subject, words);

                                if (cats.Contains("keyword")) status |= Compare(keywords, words);
                                if (cats.Contains("keywords")) status |= Compare(keywords, words);
                                if (cats.Contains("tag")) status |= Compare(keywords, words);
                                if (cats.Contains("tags")) status |= Compare(keywords, words);

                                if (cats.Contains("comment")) status |= Compare(comments, words);
                                if (cats.Contains("comments")) status |= Compare(comments, words);

                                if (cats.Contains("artist")) status |= Compare(artist, words);
                                else if (cats.Contains("author")) status |= Compare(artist, words);

                                if (cats.Contains("copyright")) status |= Compare(copyright, words);

                                if (cats.Contains("software")) status |= Compare(software, words);
                                if (cats.Contains("rate") || cats.Contains("rating")) status |= Compare(rate, words);
                                if (cats.Contains("rank") || cats.Contains("ranking")) status |= Compare(rank, words);
                                if (cats.Contains("date")) status |= Compare(date_string, words);

                                if (cats.Contains("width")) status |= Compare($"{exif.Width}", words);
                                if (cats.Contains("height")) status |= Compare($"{exif.Height}", words);
                                if (cats.Contains("aspect")) status |= Compare($"{(double)exif.Width / exif.Height}", words);

                                if (cats.Contains("landscape")) status |= (double)exif.Width / exif.Height > 1;
                                if (cats.Contains("portrait")) status |= (double)exif.Width / exif.Height < 1;
                                if (cats.Contains("square")) status |= (double)exif.Width / exif.Height == 1;

                                if (cats.Contains("bits")) status |= Compare($"{exif.ColorDepth}", words);
                                if (cats.Contains("endian")) status |= Compare($"{exif.ByteOrder}", words);

                                if (cats.Contains("littleendian")) status |= exif.ByteOrder == ExifByteOrder.LittleEndian;
                                else if (cats.Contains("lsb")) status |= exif.ByteOrder == ExifByteOrder.LittleEndian;

                                if (cats.Contains("bigendian")) status |= exif.ByteOrder == ExifByteOrder.BigEndian;
                                else if (cats.Contains("msb")) status |= exif.ByteOrder == ExifByteOrder.BigEndian;

                                foreach (var attr in cats_exif)
                                {
                                    try
                                    {
                                        var value = GetTagValue(exif, attr);
                                        if (cats.Count(c => c.Equals(attr, StringComparison.CurrentCultureIgnoreCase)) > 0 && !string.IsNullOrEmpty(value))
                                            status |= Compare(value, words);
                                    }
                                    catch { }
                                }
                            }
                            else if (Mode == CompareMode.LT || Mode == CompareMode.LE ||
                                     Mode == CompareMode.GT || Mode == CompareMode.GE ||
                                     Mode == CompareMode.EQ || Mode == CompareMode.NEQ ||
                                     Mode == CompareMode.HAS || Mode == CompareMode.NONE)
                            {
                                if (cats.Contains("title")) status |= Compare(title, words);
                                if (cats.Contains("subject")) status |= Compare(subject, words);

                                if (cats.Contains("keyword")) status |= Compare(keywords, words);
                                if (cats.Contains("keywords")) status |= Compare(keywords, words);
                                if (cats.Contains("tag")) status |= Compare(keywords, words);
                                if (cats.Contains("tags")) status |= Compare(keywords, words);

                                if (cats.Contains("comment")) status |= Compare(comments, words);
                                if (cats.Contains("comments")) status |= Compare(comments, words);

                                if (cats.Contains("artist")) status |= Compare(artist, words);
                                if (cats.Contains("author")) status |= Compare(artist, words);
                                if (cats.Contains("copyright")) status |= Compare(copyright, words);

                                if (cats.Contains("software")) status |= Compare(software, words);

                                int word_int;
                                var word_value = int.TryParse(word, out word_int) ? word.PadLeft(16, '0') : word;

                                var rate_value = string.IsNullOrEmpty(rate) ? rate : rate.PadLeft(16, '0');
                                var rank_value = string.IsNullOrEmpty(rank) ? rank : rank.PadLeft(16, '0');

                                if (cats.Contains("rate") || cats.Contains("rating")) status |= Compare(rate_value, word_value);
                                if (cats.Contains("rank") || cats.Contains("ranking")) status |= Compare(rank_value, word_value);
                                if (cats.Contains("date") && date.HasValue) status |= _date_.Compare(date.Value, Mode);

                                if (cats.Contains("width")) status |= Compare($"{exif.Width}", words);
                                if (cats.Contains("height")) status |= Compare($"{exif.Height}", words);
                                if (cats.Contains("aspect")) status |= Compare($"{(double)exif.Width / exif.Height}", words);

                                if (cats.Contains("landscape")) status |= (double)exif.Width / exif.Height > 1;
                                if (cats.Contains("portrait")) status |= (double)exif.Width / exif.Height < 1;
                                if (cats.Contains("square")) status |= (double)exif.Width / exif.Height == 1;

                                if (cats.Contains("bits")) status |= Compare($"{exif.ColorDepth}", words);
                                if (cats.Contains("endian")) status |= Compare($"{exif.ByteOrder}", words);

                                if (cats.Contains("littleendian")) status |= invert && exif.ByteOrder == ExifByteOrder.LittleEndian;
                                else if (cats.Contains("lsb")) status |= invert && exif.ByteOrder == ExifByteOrder.LittleEndian;

                                if (cats.Contains("bigendian")) status |= invert && exif.ByteOrder == ExifByteOrder.BigEndian;
                                else if (cats.Contains("msb")) status |= invert && exif.ByteOrder == ExifByteOrder.BigEndian;

                                foreach (var attr in cats_exif)
                                {
                                    try
                                    {
                                        var value = GetTagValue(exif, attr);
                                        if (cats.Count(c => c.Equals(attr, StringComparison.CurrentCultureIgnoreCase)) > 0 && !string.IsNullOrEmpty(value))
                                            status |= Compare(value, words);
                                    }
                                    catch { }
                                }
                            }
                            else if (Mode == CompareMode.IN && words.Length >= 2)
                            {
                                var values = words.Select(w => { double v = double.NaN; return(double.TryParse(w, out v) ? v : double.NaN); }).Where(d => !double.IsNaN(d)).OrderBy(d => d);
                                if (values.Count() >= 2)
                                {
                                    double value = double.NaN;
                                    double value_low = values.First();
                                    double value_high = values.Last();

                                    if ((cats.Contains("rate") || cats.Contains("rating")) && double.TryParse(rate, out value)) status |= value_low <= value && value <= value_high;
                                    if ((cats.Contains("rank") || cats.Contains("ranking")) && double.TryParse(rank, out value)) status |= value_low <= value && value <= value_high;

                                    if (cats.Contains("width")) status |= value_low <= exif.Width && exif.Width <= value_high;
                                    if (cats.Contains("height")) status |= value_low <= exif.Height && exif.Height <= value_high;

                                    value = (double)exif.Width / exif.Height;
                                    if (cats.Contains("aspect")) status |= value_low <= value && value <= value_high;

                                    if (cats.Contains("bits")) status |= value_low <= exif.ColorDepth && exif.ColorDepth <= value_high;
                                }

                                if (cats.Contains("date") && date.HasValue)
                                {
                                    var date_low = new DateValue(words.First());
                                    var date_high = new DateValue(words.Last());
                                    status |= date_low.Date <= date && date <= date_high.Date;
                                }
                            }
                            else if (Mode == CompareMode.OUT && words.Length >= 2)
                            {
                                var values = words.Select(w => { double v = double.NaN; return(double.TryParse(w, out v) ? v : double.NaN); }).Where(d => !double.IsNaN(d)).OrderBy(d => d);
                                if (values.Count() >= 2)
                                {
                                    double value = double.NaN;
                                    double value_low = values.First();
                                    double value_high = values.Last();

                                    if ((cats.Contains("rate") || cats.Contains("rating")) && double.TryParse(rate, out value)) status |= value < value_low || value_high < value;
                                    if ((cats.Contains("rank") || cats.Contains("ranking")) && double.TryParse(rank, out value)) status |= value < value_low || value_high < value;

                                    if (cats.Contains("width")) status |= exif.Width < value_low || value_high < exif.Width;
                                    if (cats.Contains("height")) status |= exif.Height < value_low || value_high < exif.Height;

                                    value = (double)exif.Width / exif.Height;
                                    if (cats.Contains("aspect")) status |= value < value_low || value_high < value;

                                    if (cats.Contains("bits")) status |= exif.ColorDepth < value_low || value_high < exif.ColorDepth;

                                }

                                if (cats.Contains("date") && date.HasValue)
                                {
                                    var date_low = new DateValue(words.First());
                                    var date_high = new DateValue(words.Last());
                                    status |= date < date_low.Date || date_high.Date < date;
                                }
                            }
                            #endregion
                            status |= status;
                            if (status) break;
                        }
                    }
                    else
                    {
                        #region Output attributes
                        var padding = "".PadLeft(ValuePaddingLeft);
                        StringBuilder sb = new StringBuilder();
                        sb.Append(ValueHeader);
                        if (cats.Contains("title") && !string.IsNullOrEmpty(title)) sb.AppendLine($"{padding}{title}");
                        if (cats.Contains("subject") && !string.IsNullOrEmpty(subject)) sb.AppendLine($"{padding}{subject}");
                        if (cats.Contains("keyword") && !string.IsNullOrEmpty(keywords)) sb.AppendLine($"{padding}{keywords}");
                        else if (cats.Contains("keywords") && !string.IsNullOrEmpty(keywords)) sb.AppendLine($"{padding}{keywords}");
                        else if (cats.Contains("tag") && !string.IsNullOrEmpty(keywords)) sb.AppendLine($"{padding}{keywords}");
                        else if (cats.Contains("tags") && !string.IsNullOrEmpty(keywords)) sb.AppendLine($"{padding}{keywords}");
                        if (cats.Contains("comment") && !string.IsNullOrEmpty(comments)) sb.AppendLine($"{padding}{comments}");
                        else if (cats.Contains("comments") && !string.IsNullOrEmpty(comments)) sb.AppendLine($"{padding}{comments}");
                        if (cats.Contains("artist") && !string.IsNullOrEmpty(artist)) sb.AppendLine($"{padding}{artist}");
                        else if (cats.Contains("author") && !string.IsNullOrEmpty(artist)) sb.AppendLine($"{padding}{artist}");
                        else if (cats.Contains("authors") && !string.IsNullOrEmpty(artist)) sb.AppendLine($"{padding}{artist}");
                        if (cats.Contains("copyright") && !string.IsNullOrEmpty(copyright)) sb.AppendLine($"{padding}{copyright}");
                        if (cats.Contains("copyrights") && !string.IsNullOrEmpty(copyright)) sb.AppendLine($"{padding}{copyright}");
                        if (cats.Contains("software") && !string.IsNullOrEmpty(software)) sb.AppendLine($"{padding}{software}");
                        if ((cats.Contains("rate") || cats.Contains("rating")) && !string.IsNullOrEmpty(rate)) sb.AppendLine($"{padding}{rate}");
                        if ((cats.Contains("rank") || cats.Contains("ranking")) && !string.IsNullOrEmpty(rank)) sb.AppendLine($"{padding}{rank}");
                        if (cats.Contains("date") && !string.IsNullOrEmpty(date_string)) sb.AppendLine($"{padding}{date_string}");

                        if (cats.Contains("width")) sb.AppendLine($"{padding}{exif.Width}");
                        if (cats.Contains("height")) sb.AppendLine($"{padding}{exif.Height}");
                        if (cats.Contains("aspect")) sb.AppendLine($"{padding}{((double)exif.Width / exif.Height):F4}");

                        if (cats.Contains("landscape")) sb.AppendLine($"{(double)exif.Width / exif.Height > 1}");
                        if (cats.Contains("portrait")) sb.AppendLine($"{(double)exif.Width / exif.Height < 1}");
                        if (cats.Contains("square")) sb.AppendLine($"{(double)exif.Width / exif.Height == 1}");

                        if (cats.Contains("bits")) sb.AppendLine($"{exif.ColorDepth}");
                        if (cats.Contains("endian")) sb.AppendLine($"{exif.ByteOrder}");

                        if (cats.Contains("littleendian")) sb.AppendLine($"{exif.ByteOrder == ExifByteOrder.LittleEndian}");
                        else if (cats.Contains("lsb")) sb.AppendLine($"{exif.ByteOrder == ExifByteOrder.LittleEndian}");

                        if (cats.Contains("bigendian")) sb.AppendLine($"{exif.ByteOrder == ExifByteOrder.BigEndian}");
                        else if (cats.Contains("msb")) sb.AppendLine($"{exif.ByteOrder == ExifByteOrder.BigEndian}");

                        foreach (var attr in cats_exif)
                        {
                            var value = GetTagValue(exif, attr);
                            if (!string.IsNullOrEmpty(value)) sb.AppendLine($"{padding}{attr} = {(MaxLength > 0 ? value.Substring(0, MaxLength) : value)}");
                        }
                        status = sb.ToString().Trim();
                        #endregion
                    }

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
