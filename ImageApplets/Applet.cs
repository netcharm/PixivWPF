using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using CompactExifLib;
using Mono.Options;

namespace ImageApplets
{
    public enum CompareMode { NONE, IS, EQ, NEQ, GT, LT, GE, LE, HAS, NO, IN, OUT, AND, OR, NOT, XOR, BEFORE, PREV, TODAY, CURRENT, NEXT, AFTER, VALUE };
    public enum DateCompareMode { IS, NOT, IN, OUT, BEFORE, PREV, TODAY, CURRENT, NEXT, AFTER };
    public enum DateUnit { DAY, WEEK, MONTH, SEASON, QUATER, YEAR };

    public enum AppletCategory { FileOP, ImageType, ImageContent, ImageGeneration, ImageAttribure, Other, Unknown, None }
    public enum STATUS { All, Yes, No, None };
    [Flags]
    public enum ClipboardMode { NONE, IN, OUT, RESULT, INOUT, INRESULT, OUTRESULT, ALL };
    public enum ReadMode { ALL, LINE };

    static public class AppletExtensions
    {
        #region String Padding with CJK lenhth calculating
        static private Encoding MBCS = Encoding.GetEncoding("GB18030");

        static private int LengthCJK(string s)
        {
            return (MBCS.GetByteCount(s));
        }

        static public string PadLefttCJK(this string s, int totalWidth)
        {
            return (s.PadLeft(totalWidth - (LengthCJK(s) - s.Length)));
        }

        static public string PadLefttCJK(this string s, int totalWidth, char paddingChar)
        {
            return (s.PadLeft(totalWidth - (LengthCJK(s) - s.Length), paddingChar));
        }

        static public string PadRightCJK(this string s, int totalWidth)
        {
            return (s.PadRight(totalWidth - (LengthCJK(s) - s.Length)));
        }

        static public string PadRightCJK(this string s, int totalWidth, char paddingChar)
        {
            return (s.PadRight(totalWidth - (LengthCJK(s) - s.Length), paddingChar));
        }
        #endregion

        #region Convert Chinese to Japanese Kanji
        static private Encoding GB2312 = Encoding.GetEncoding("GB2312");
        static private Encoding JIS = Encoding.GetEncoding("SHIFT_JIS");
        static private List<char> GB2312_List { get; set; } = new List<char>();
        static private List<char> JIS_List { get; set; } = new List<char>();
        static private void InitGBJISTable()
        {
            if (JIS_List.Count == 0 || GB2312_List.Count == 0)
            {
                JIS_List.Clear();
                GB2312_List.Clear();

                var jis_data =  Properties.Resources.GB2JIS;
                var jis_count = jis_data.Length / 2;
                JIS_List = JIS.GetString(jis_data).ToList();
                GB2312_List = GB2312.GetString(jis_data).ToList();

                var jis = new byte[2];
                var gb2312 = new byte[2];
                for (var i = 0; i < 94; i++)
                {
                    gb2312[0] = (byte)(i + 0xA1);
                    for (var j = 0; j < 94; j++)
                    {
                        gb2312[1] = (byte)(j + 0xA1);
                        var offset = i * 94 + j;
                        GB2312_List[offset] = GB2312.GetString(gb2312).First();

                        jis[0] = jis_data[2 * offset];
                        jis[1] = jis_data[2 * offset + 1];
                        JIS_List[i * 94 + j] = JIS.GetString(jis).First();
                    }
                }
            }
        }

        static public char ConvertChinese2Japanese(this char character)
        {
            var result = character;

            InitGBJISTable();
            var idx = GB2312_List.IndexOf(result);
            if (idx >= 0) result = JIS_List[idx];

            return (result);
        }

        static public string ConvertChinese2Japanese(this string line)
        {
            var result = line;

            result = string.Join("", line.ToCharArray().Select(c => ConvertChinese2Japanese(c)));
            //result = new string(line.ToCharArray().Select(c => ConvertChinese2Japanese(c)).ToArray());

            return (result);
        }

        static public IList<string> ConvertChinese2Japanese(this IEnumerable<string> lines)
        {
            var result = new List<string>();

            result.AddRange(lines.Select(l => ConvertChinese2Japanese(l)).ToList());

            return (result);
        }

        static public char ConvertJapanese2Chinese(this char character)
        {
            var result = character;

            InitGBJISTable();
            var idx = JIS_List.IndexOf(result);
            if (idx >= 0) result = GB2312_List[idx];

            return (result);
        }

        static public string ConvertJapanese2Chinese(this string line)
        {
            var result = line;

            result = string.Join("", line.ToCharArray().Select(c => ConvertJapanese2Chinese(c)));
            //result = new string(line.ToCharArray().Select(c => ConvertJapanese2Chinese(c)).ToArray());

            return (result);
        }

        static public IList<string> ConvertJapanese2Chinese(this IEnumerable<string> lines)
        {
            var result = new List<string>();

            result.AddRange(lines.Select(l => ConvertJapanese2Chinese(l)).ToList());

            return (result);
        }
        #endregion

        #region double inside/outside a range
        static public bool Inside(this double src, dynamic dst, double tolerance = 0.005, bool include = true)
        {
            if (dst is double) return (Inside(src, (double)dst, tolerance, include));
            else if (dst is int) return (Inside(src, (int)dst, tolerance, include));
            else if (dst is Tuple<double, double>) return (Inside(src, (Tuple<double, double>)dst, tolerance, include));
            else return (false);
        }

        static public bool Outside(this double src, dynamic dst, double tolerance = 0.005, bool include = false)
        {
            if (dst is double) return (Outside(src, (double)dst, tolerance, include));
            else if (dst is int) return (Outside(src, (int)dst, tolerance, include));
            else if (dst is Tuple<double, double>) return (Outside(src, (Tuple<double, double>)dst, tolerance, include));
            else return (false);
        }

        static public bool Inside(this double src, Tuple<double, double> dst, double tolerance = 0.005, bool include = true)
        {
            var r_l = dst.Item1;
            var r_r = dst.Item2;
            if (include)
                return (src >= r_l * (1 - tolerance) && src <= r_r * (1 + tolerance));
            else
                return (src > r_l * (1 - tolerance) && src < r_r * (1 + tolerance));
        }

        static public bool Outside(this double src, Tuple<double, double> dst, double tolerance = 0.005, bool include = false)
        {
            var r_l = dst.Item1;
            var r_r = dst.Item2;
            if (include)
                return (src <= r_l * (1 - tolerance) || src >= r_r * (1 + tolerance));
            else
                return (src < r_l * (1 - tolerance) || src > r_r * (1 + tolerance));
        }

        static public bool Inside(this double src, double dst, double tolrence = 0.005, bool include = true)
        {
            if (include)
                return (src >= dst * (1 - tolrence) && src <= dst * (1 + tolrence));
            else
                return (src > dst * (1 - tolrence) && src < dst * (1 + tolrence));
        }

        static public bool Outside(this double src, double dst, double tolrence = 0.005, bool include = false)
        {
            if (include)
                return (src <= dst * (1 - tolrence) || src >= dst * (1 + tolrence));
            else
                return (src < dst * (1 - tolrence) || src > dst * (1 + tolrence));
        }

        static public bool Inside(this int src, int dst, double tolrence = 1.0, bool include = true)
        {
            if (include)
                return (src >= dst * (1.0 - tolrence) && src <= dst * (1.0 + tolrence));
            else
                return (src > dst * (1.0 - tolrence) && src < dst * (1.0 + tolrence));
        }

        static public bool Outside(this int src, int dst, double tolrence = 1.0, bool include = false)
        {
            if (include)
                return (src <= dst * (1.0 - tolrence) || src >= dst * (1.0 + tolrence));
            else
                return (src < dst * (1.0 - tolrence) || src > dst * (1.0 + tolrence));
        }

        #endregion

        #region image bitmap helper
        static public Color[] GetMatrix(this Bitmap bmp, int x, int y, int w, int h)
        {
            var ret = new List<Color>();
            if (bmp is Bitmap)
            {
                //var data = bmp.LockBits(new Rectangle(x, y, w, h), ImageLockMode.ReadOnly, bmp.PixelFormat);
                for (var i = x; i < x + w; i++)
                {
                    for (var j = y; j < y + h; j++)
                    {
                        if (i < bmp.Width && j < bmp.Height)
                            ret.Add(bmp.GetPixel(i, j));
                    }
                }
                //bmp.UnlockBits(data);
            }
            return (ret.ToArray());
        }

        static public bool GuessAlpha(this Image image, int win_size, int threshold)
        {
            var status = false;
            try
            {
                if (image is Image && (image.RawFormat.Guid.Equals(ImageFormat.Png.Guid) || image.RawFormat.Guid.Equals(ImageFormat.Tiff.Guid)))
                {
                    if (image.PixelFormat == PixelFormat.Format32bppArgb) { status = true; }
                    else if (image.PixelFormat == PixelFormat.Format32bppPArgb) { status = true; }
                    else if (image.PixelFormat == PixelFormat.Format16bppArgb1555) { status = true; }
                    else if (image.PixelFormat == PixelFormat.Format64bppArgb) { status = true; }
                    else if (image.PixelFormat == PixelFormat.Format64bppPArgb) { status = true; }
                    else if (image.PixelFormat == PixelFormat.PAlpha) { status = true; }
                    else if (image.PixelFormat == PixelFormat.Alpha) { status = true; }
                    else if (Image.IsAlphaPixelFormat(image.PixelFormat)) { status = true; }

                    if (status)
                    {
                        var bmp = new Bitmap(image);
                        var w = bmp.Width;
                        var h = bmp.Height;
                        var m = win_size;
                        var mt = Math.Ceiling(m * m / 2.0);
                        var lt = bmp?.GetMatrix(0, 0, m, m).Count(c => c.A < threshold);
                        var rt = bmp?.GetMatrix(w - m, 0, m, m).Count(c => c.A < threshold);
                        var lb = bmp?.GetMatrix(0, h - m, m, m).Count(c => c.A < threshold);
                        var rb = bmp?.GetMatrix(w - m, h - m, m, m).Count(c => c.A < threshold);
                        var ct = bmp?.GetMatrix((int)(w / 2.0 - m / 2.0) , (int)(h / 2.0 - m / 2.0), m, m).Count(c => c.A < threshold);
                        status = (lt > mt || rt > mt || lb > mt || rb > mt || ct > mt);
                    }
                }
            }
            catch { }
            //catch (Exception ex) {}
            return (status);
        }
        #endregion
    }

    public class DateValue
    {
        static private Dictionary<string, string> num_chs = new Dictionary<string, string>()
        {
            { "一", "1" }, { "二", "2" }, { "三", "3" }, { "四", "4" }, { "五", "5" }, { "六", "6" }, { "七", "7" }, { "八", "8" }, { "九", "9" }, { "零", "0" },
            { "壹", "1" }, { "贰", "2" }, { "叁", "3" }, { "肆", "4" }, { "伍", "5" }, { "陆", "6" }, { "柒", "7" }, { "捌", "8" }, { "玖", "9" },
        };

        private string _text_ { get; set; } = string.Empty;

        internal protected double y { get; set; } = double.NaN;
        internal protected double m { get; set; } = double.NaN;
        internal protected double d { get; set; } = double.NaN;
        internal protected double w { get; set; } = double.NaN;
        internal protected double h { get; set; } = double.NaN;
        internal protected double n { get; set; } = double.NaN;
        internal protected double s { get; set; } = double.NaN;

        public int? Year
        {
            get { return ((double.IsNaN(y) ? null : (int?)Math.Max(01, y) % 9999)); }
        }
        public int? Month
        {
            get { return ((double.IsNaN(m) ? null : (int?)Math.Max(01, m) % 0012)); }
        }
        public int? Day
        {
            get { return ((double.IsNaN(d) ? null : (int?)Math.Max(01, d) % 0031)); }
        }
        public int? Hour
        {
            get { return ((double.IsNaN(h) ? null : (int?)Math.Max(00, h) % 0024)); }
        }
        public int? Minute
        {
            get { return ((double.IsNaN(n) ? null : (int?)Math.Max(00, n) % 0060)); }
        }
        public int? Second
        {
            get { return ((double.IsNaN(s) ? null : (int?)Math.Max(00, s) % 0060)); }
        }
        public DayOfWeek? WeekDay
        {
            get { return ((double.IsNaN(w) ? null : (DayOfWeek?)(Math.Max(1, w) % 7))); }
        }

        private DateTime? _date_ref_ = null;
        public DateTime Refrence
        {
            get { return (_date_ref_ ?? DateTime.Now); }
            set { _date_ref_ = value; }
        }
        public DateTime? Date
        {
            get { return (GetDateTime(Refrence)); }
        }
        public DayOfWeek? DayOfWeek
        {
            get { return (GetWeekDay(Refrence)); }
        }

        public DateValue(string text)
        {
            Parsing(text);
        }

        public DateValue(DateTime dt)
        {
            y = dt.Year;
            m = dt.Month;
            d = dt.Day;
            
            h = dt.Hour;
            n = dt.Minute;
            s = dt.Second;

            w = (double)dt.DayOfWeek;
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

        internal string ConvertChineseNumberString(string text)
        {
            var result = text;
            if (!string.IsNullOrEmpty(text))
            {
                foreach (var kv in num_chs)
                {
                    result = result.Replace(kv.Key, kv.Value); ;
                }
                if (result.StartsWith("十")) result = $"1{result}";
                var matches_w = Regex.Matches(result, @"((\d+)(万|千|百|十)?)+", RegexOptions.IgnoreCase);
                foreach (Match mw in matches_w)
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
                        result = result.Replace(mw.Value.TrimStart('0'), $"{value}");
                    }
                }
            }
            return (result);
        }

        internal DateTime? ConvertFromDateValue(DateTime src)
        {
            DateTime? result = null;
            try
            {
                var y = (int)(double.IsNaN(this.y) ? src.Year : this.y);
                var m = (int)(double.IsNaN(this.m) ? src.Month : this.m);
                var d = (int)(double.IsNaN(this.d) ? src.Day : this.d);
                var h = (int)(double.IsNaN(this.h) ? src.Hour : this.h);
                var n = (int)(double.IsNaN(this.n) ? src.Minute : this.n);
                var s = (int)(double.IsNaN(this.s) ? src.Second : this.s);
                var dt = new DateTime(y, m, d, h, n, s);
                result = dt;
            }
            catch { }
            return (result);
        }

        internal protected void Parsing(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                //DateTime tdt;
                //if (DateTime.TryParse(text, out tdt))
                //{
                //    y = tdt.Year;
                //    m = tdt.Month;
                //    d = tdt.Day;

                //    h = tdt.Hour;
                //    n = tdt.Minute;
                //    s = tdt.Second;

                //    w = (double)tdt.DayOfWeek;
                //}
                //else
                {
                    var word = ConvertChineseNumberString(text.Trim());
                    if (Regex.IsMatch(word, @"^/(.+?)/i?$", RegexOptions.IgnoreCase))
                    {
                        word = word.Trim(new char[] { 'i', '/' });
                    }
                    else
                    {
                        word = word.Replace("-", "\\-").Replace("\\", "\\\\").Replace(".", "\\.").Replace("?", "\\?").Replace("*", "\\*");
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
                        var match = Regex.Match(_text_, @"^(\d{2,4})[/\-,\. 年](\d{1,2})[/\-,\. 月](\d{1,2})日?", RegexOptions.IgnoreCase);
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

        public dynamic Compare(DateTime src, CompareMode mode)
        {
            dynamic status = false;
            if (mode == CompareMode.VALUE)
                status = src.ToString(Applet.DateTimeFormat);
            else
            {
                var dt = this.GetDateTime(src);
                if (dt == null) dt = ConvertFromDateValue(src);
                var wd = this.WeekDay ?? (double.IsNaN(this.w) ? src.DayOfWeek : (DayOfWeek)Enum.Parse(typeof(DayOfWeek), $"{this.w - 1}"));

                status = false;
                switch (mode)
                {
                    case CompareMode.HAS:
                        status = true;
                        if (!double.IsNaN(this.y)) status = status && this.y == src.Year;
                        if (!double.IsNaN(this.m)) status = status && this.m == src.Month;
                        if (!double.IsNaN(this.d)) status = status && this.d == src.Day;
                        if (!double.IsNaN(this.h)) status = status && this.h == src.Hour;
                        if (!double.IsNaN(this.n)) status = status && this.n == src.Minute;
                        if (!double.IsNaN(this.s)) status = status && this.s == src.Second;
                        if (!double.IsNaN(this.w)) status = status && this.WeekDay == src.DayOfWeek;
                        break;
                    case CompareMode.NONE:
                        if (!double.IsNaN(this.y)) status = status || this.y == src.Year;
                        if (!double.IsNaN(this.m)) status = status || this.m == src.Month;
                        if (!double.IsNaN(this.d)) status = status || this.d == src.Day;
                        if (!double.IsNaN(this.h)) status = status || this.h == src.Hour;
                        if (!double.IsNaN(this.n)) status = status || this.n == src.Minute;
                        if (!double.IsNaN(this.s)) status = status || this.s == src.Second;
                        if (!double.IsNaN(this.w)) status = status || this.WeekDay == src.DayOfWeek;
                        status = !status;
                        break;
                    case CompareMode.NEQ:
                        if (!double.IsNaN(this.w)) status = src.DayOfWeek != wd;
                        else status = dt.HasValue && src != dt;
                        break;
                    case CompareMode.EQ:
                        if (!double.IsNaN(this.w)) status = src.DayOfWeek == wd;
                        else status = dt.HasValue && src == dt;
                        break;
                    case CompareMode.LT:
                        if (!double.IsNaN(this.w)) status = src.DayOfWeek < wd;
                        else status = dt.HasValue && src < dt;
                        break;
                    case CompareMode.LE:
                        if (!double.IsNaN(this.w)) status = src.DayOfWeek <= wd;
                        else status = dt.HasValue && src <= dt;
                        break;
                    case CompareMode.GT:
                        if (!double.IsNaN(this.w)) status = src.DayOfWeek > wd;
                        else status = dt.HasValue && src > dt;
                        break;
                    case CompareMode.GE:
                        if (!double.IsNaN(this.w)) status = src.DayOfWeek >= wd;
                        else status = dt.HasValue && src >= dt;
                        break;
                    default:
                        break;
                }
            }
            return (status);
        }

        public dynamic Compare(DateValue src, CompareMode mode)
        {
            dynamic status = false;
            if (src is DateValue && src.Date.HasValue)
            {
                var sdt = new DateTime(src.Date.Value.Year, src.Date.Value.Month, src.Date.Value.Day, src.Date.Value.Hour, src.Date.Value.Minute, src.Date.Value.Second);
                if (mode == CompareMode.VALUE)
                    status = sdt.ToString(Applet.DateTimeFormat);
                else
                {
                    var dt = this.GetDateTime(src.Date.Value);
                    if (dt == null) dt = ConvertFromDateValue(src.Date.Value);
                    var wd = this.WeekDay ?? (double.IsNaN(this.w) ? src.DayOfWeek : (DayOfWeek)Enum.Parse(typeof(DayOfWeek), $"{this.w - 1}"));

                    status = false;
                    switch (mode)
                    {
                        case CompareMode.HAS:
                            status = true;
                            if (!double.IsNaN(this.y)) status = status && this.y == src.Year;
                            if (!double.IsNaN(this.m)) status = status && this.m == src.Month;
                            if (!double.IsNaN(this.d)) status = status && this.d == src.Day;
                            if (!double.IsNaN(this.h)) status = status && this.h == src.Hour;
                            if (!double.IsNaN(this.n)) status = status && this.n == src.Minute;
                            if (!double.IsNaN(this.s)) status = status && this.s == src.Second;
                            if (!double.IsNaN(this.w)) status = status && this.WeekDay == src.DayOfWeek;
                            break;
                        case CompareMode.NONE:
                            if (!double.IsNaN(this.y)) status = status || this.y == src.Year;
                            if (!double.IsNaN(this.m)) status = status || this.m == src.Month;
                            if (!double.IsNaN(this.d)) status = status || this.d == src.Day;
                            if (!double.IsNaN(this.h)) status = status || this.h == src.Hour;
                            if (!double.IsNaN(this.n)) status = status || this.n == src.Minute;
                            if (!double.IsNaN(this.s)) status = status || this.s == src.Second;
                            if (!double.IsNaN(this.w)) status = status || this.WeekDay == src.DayOfWeek;
                            status = !status;
                            break;
                        case CompareMode.NEQ:
                            if (!double.IsNaN(this.w)) status = src.DayOfWeek != wd;
                            else status = dt.HasValue && sdt != dt;
                            break;
                        case CompareMode.EQ:
                            if (!double.IsNaN(this.w)) status = src.DayOfWeek == wd;
                            else status = dt.HasValue && sdt == dt;
                            break;
                        case CompareMode.LT:
                            if (!double.IsNaN(this.w)) status = src.DayOfWeek < wd;
                            else status = dt.HasValue && sdt < dt;
                            break;
                        case CompareMode.LE:
                            if (!double.IsNaN(this.w)) status = src.DayOfWeek <= wd;
                            else status = dt.HasValue && sdt <= dt;
                            break;
                        case CompareMode.GT:
                            if (!double.IsNaN(this.w)) status = src.DayOfWeek > wd;
                            else status = dt.HasValue && sdt > dt;
                            break;
                        case CompareMode.GE:
                            if (!double.IsNaN(this.w)) status = src.DayOfWeek >= wd;
                            else status = dt.HasValue && sdt >= dt;
                            break;
                        default:
                            break;
                    }
                }
            }
            return (status);
        }

        public override bool Equals(object obj)
        {
            if (obj is DateValue)
                return (this.Compare(obj as DateValue, CompareMode.EQ));
            else
                return (false);
        }

        public override int GetHashCode()
        {
            return (this.GetHashCode());
        }

        public static bool operator ==(DateValue left, DateValue right)
        {
            if (left is DateValue && right is DateValue)
                return (right.Compare(left, CompareMode.EQ));
            else return (false);
        }
        public static bool operator !=(DateValue left, DateValue right)
        {
            if (left is DateValue && right is DateValue)
                return (right.Compare(left, CompareMode.NEQ));
            else return (false);
        }
        public static bool operator <=(DateValue left, DateValue right)
        {
            if (left is DateValue && right is DateValue)
                return (right.Compare(left, CompareMode.LE));
            else return (false);
        }
        public static bool operator >=(DateValue left, DateValue right)
        {
            if (left is DateValue && right is DateValue)
                return (right.Compare(left, CompareMode.GE));
            else return (false);
        }
        public static bool operator <(DateValue left, DateValue right)
        {
            if (left is DateValue && right is DateValue)
                return (right.Compare(left, CompareMode.LT));
            else return (false);
        }
        public static bool operator >(DateValue left, DateValue right)
        {
            if (left is DateValue && right is DateValue)
                return (right.Compare(left, CompareMode.GT));
            else return (false);
        }

        public static bool operator ==(DateTime left, DateValue right)
        {
            if (right is DateValue)
                return (right.Compare(left, CompareMode.EQ));
            else return (false);
        }
        public static bool operator !=(DateTime left, DateValue right)
        {
            if (right is DateValue)
                return (right.Compare(left, CompareMode.NEQ));
            else return (false);
        }
        public static bool operator <=(DateTime left, DateValue right)
        {
            if (right is DateValue)
                return (right.Compare(left, CompareMode.LE));
            else return (false);
        }
        public static bool operator >=(DateTime left, DateValue right)
        {
            if (right is DateValue)
                return (right.Compare(left, CompareMode.GE));
            else return (false);
        }
        public static bool operator <(DateTime left, DateValue right)
        {
            if (right is DateValue)
                return (right.Compare(left, CompareMode.LT));
            else return (false);
        }
        public static bool operator >(DateTime left, DateValue right)
        {
            if (right is DateValue)
                return (right.Compare(left, CompareMode.GT));
            else return (false);
        }

        public static bool operator ==(DateValue left, DateTime right)
        {
            if (left is DateValue)
                return (left.Compare(right, CompareMode.EQ));
            else return (false);
        }
        public static bool operator !=(DateValue left, DateTime right)
        {
            if (left is DateValue)
                return (left.Compare(right, CompareMode.NEQ));
            else return (false);
        }
        public static bool operator <=(DateValue left, DateTime right)
        {
            if (left is DateValue)
                return (left.Compare(right, CompareMode.GE));
            else return (false);
        }
        public static bool operator >=(DateValue left, DateTime right)
        {
            if (left is DateValue)
                return (left.Compare(right, CompareMode.LE));
            else return (false);
        }
        public static bool operator <(DateValue left, DateTime right)
        {
            if (left is DateValue)
                return (left.Compare(right, CompareMode.GT));
            else return (false);
        }
        public static bool operator >(DateValue left, DateTime right)
        {
            if (left is DateValue)
                return (left.Compare(right, CompareMode.LT));
            else return (false);
        }
    }

    public class ExecuteResult
    {
        public FileInfo InputInfo { get; set; } = null;
        public string InputFile { get; set; } = string.Empty;
        public FileInfo OutputInfo { get; set; } = null;
        public string OutputFile { get; set; } = string.Empty;
        public bool State { get; set; } = false;
        public dynamic Message { get; set; } = null;

        public void Set(bool state, dynamic message = null, bool clean = false)
        {
            if (clean)
            {
                InputFile = string.Empty;
                InputInfo = null;
                OutputFile = string.Empty;
                OutputInfo = null;
            }
            State = state;
            Message = message;
        }

        public void Set(string src, string dst, bool state, dynamic message = null)
        {
            InputFile = src;
            InputInfo = string.IsNullOrEmpty(src) ? null : new FileInfo(src);
            OutputFile = dst;
            OutputInfo = string.IsNullOrEmpty(dst) ? null : new FileInfo(dst);
            State = state;
            Message = message;
        }

        public void Reset()
        {
            InputInfo = null;
            InputFile = string.Empty;
            OutputInfo = null;
            OutputFile = string.Empty;
            State = false;
            Message = null;
        }

        public void Clear()
        {
            Reset();
        }

        public ExecuteResult Clone()
        {
            return (new ExecuteResult()
            {
                InputInfo = InputInfo, 
                InputFile = InputFile,
                OutputInfo = OutputInfo,
                OutputFile = OutputFile,
                State = State,
                Message = Message
            });
        }
    }

    public interface IApplet
    {
        Applet GetApplet();
    }

    public abstract class Applet: IApplet
    {
        #region Background Execute Helper
        private SemaphoreSlim bgWorking = null;
        private class BackgroundExecuteParamter<T>
        {
            public IEnumerable<T> Files { get; set; } = new List<T>();
            public object[] Args { get; set; } = null;
            public IEnumerable<ExecuteResult> ResultList { get; set; } = new List<ExecuteResult>();
        }
        private BackgroundWorker bgExecuteTask = null;
        public Action<int, int, ExecuteResult, object, object> ReportProgress { get; set; } = null;
        #endregion

        public static string ClipboardName { get; } = "ClipBoard";

        public static string DateTimeFormat = $"yyyy-MM-dd HH:mm:ss.fffzzz";
        public static string DateTimeFormatLocal = $"{CultureInfo.CurrentCulture.DateTimeFormat.LongDatePattern}, ddd";
        public static string DateTimeFormatUtc = $"yyyy-MM-dd HH:mm:ss.fff+00:00";
        public static string DateTimeFormatLocalUtc = $"{CultureInfo.CurrentCulture.DateTimeFormat.LongDatePattern}, ddd";
        public static char[] SplitChar = new char[] { '#', ';', ',' };
        public static char[] RegexTrimChar = new char[] { 'i', '/' };
        public static string IsRegexPattern = @"^/(.+?)/i?$";

        //private static bool show_help = false;
        protected static STATUS Status { get; set; } = STATUS.All;

        private static List<dynamic> _range_value_ = new List<dynamic>();
        public static List<dynamic> RangeValue
        {
            get { return (_range_value_); }
            set { _range_value_ = value; }
        }

        public string Help { get { return (GetHelp()); } }

        public virtual AppletCategory Category { get; internal protected set; } = AppletCategory.Unknown;

        static private ClipboardMode _ClipboardMode_ = ClipboardMode.IN | ClipboardMode.RESULT;
        public ClipboardMode ClipboardMode
        {
            get { return (_ClipboardMode_); }
            set { _ClipboardMode_ = value; }
        }

        static private ReadMode _ReadInputMode_ = ReadMode.LINE;
        public ReadMode ReadInputMode { get { return (_ReadInputMode_); } }

        static public string[] LINE_BREAK { get; set; } = new string[] { Environment.NewLine, "\n\r", "\r\n", "\n", "\r" };
        static public char ContentMark { get; } = '\u20D0';

        public string Name { get { return (this.GetType().Name); } }

        public int ValuePaddingLeft { get; set; } = 0;
        public int ValuePaddingRight { get; set; } = 0;

        static private bool _verbose_ = false;
        public bool Verbose { get { return (_verbose_); } }

        static private bool _descending_ = false;
        public bool Descending { get { return (_descending_); } }

        static private bool _recursion_ = false;
        public bool Recursion { get { return (_recursion_); } }

        static private bool _sorting_ = true;
        public virtual bool Sorting { get { return (_sorting_); } }

        static private int _sortzero_ = 16;
        public int SortZero { get { return (_sortzero_); } }

        static private string _input_file_ = string.Empty;
        public string InputFile { get { return (_input_file_); } set { _input_file_ = value is string ? value : string.Empty; } }

        static private string _output_file_ = string.Empty;
        public string OutputFile { get { return (_output_file_); } protected internal set { _output_file_ = value is string ? value : string.Empty; } }

        static private string _result_file_ = string.Empty;
        public string ResultFile { get { return (_result_file_); } protected internal set { _result_file_ = value is string ? value : string.Empty; } }

        private ExecuteResult _result_ = new ExecuteResult();
        public ExecuteResult Result { get { return (_result_); } }

        static private IEnumerable<string> _input_files_ = new List<string>();
        public IEnumerable<string> InputFiles { get { return (_input_files_); } set { _input_files_ = value is IEnumerable<string> ? value : new List<string>(); } }

        static private IEnumerable<string> _output_files_ = new List<string>();
        public IEnumerable<string> OutputFiles { get { return (_output_files_); } }

        static private IEnumerable<ExecuteResult> _results_ = new List<ExecuteResult>();
        public IEnumerable<ExecuteResult> Results { get { return (_results_); } }

        static List<string> _log_ = new List<string>();
        public List<string> Log { get { return (_log_); } }

        public virtual OptionSet Options { get; set; } = new OptionSet()
        {
            { "t|y|true|yes", "Keep True Result", v => { Status = STATUS.Yes; } },
            { "f|n|false|no", "Keep False Result", v => { Status = STATUS.No; } },
            { "a|all", "Keep All", v => { Status = STATUS.All; } },
            { " " },
            { "verbose", "Output All When Redirected STDOUT", v => { _verbose_ = true; } },
            { "descending", "Process File List Order", v => { _descending_ = true; } },
            { "recursion|subfolders", "Recursion Sub Folders", v => { _recursion_ = true; } },
            { "nosort", "Not Sorting Input List", v => { _sorting_ = false; } },
            { "sortzero=", "Max Of NaturalSort Padding Zero", v => { if (!string.IsNullOrEmpty(v)) int.TryParse(v, out _sortzero_); } },
            { "result|log=", "Result To {FILE} or CLIPBOARD", v => { if (!string.IsNullOrEmpty(v)) _result_file_ = v; } },
            { "input|filelist=", "Get Files From {FILE} or CLIPBOARD", v => { if (!string.IsNullOrEmpty(v)) _input_file_ = v; } },
            { "output=", "Output To {FILE} or CLIPBOARD", v => { if (!string.IsNullOrEmpty(v)) _output_file_ = v; } },
            { "clipboard=", "using CLIPBOARD as <IN|OUT|RESULT|ALL>", v => 
                {
                    if (!string.IsNullOrEmpty(v) && Enum.TryParse(v, out _ClipboardMode_))
                    {
                        if (_ClipboardMode_.HasFlag(ClipboardMode.IN)) _input_file_ = ClipboardName;
                        if (_ClipboardMode_.HasFlag(ClipboardMode.OUT)) _output_file_ = ClipboardName;
                        if (_ClipboardMode_.HasFlag(ClipboardMode.RESULT)) _result_file_ = ClipboardName;
                        if (_ClipboardMode_.HasFlag(ClipboardMode.INOUT)) { _input_file_ = ClipboardName; _output_file_ = ClipboardName; }
                        if (_ClipboardMode_.HasFlag(ClipboardMode.INRESULT)) { _input_file_ = ClipboardName; _result_file_ = ClipboardName; }
                        if (_ClipboardMode_.HasFlag(ClipboardMode.OUTRESULT)) { _output_file_ = ClipboardName; _result_file_ = ClipboardName; }
                        if (_ClipboardMode_.HasFlag(ClipboardMode.ALL)) { _input_file_ = ClipboardName; _output_file_ = ClipboardName; _result_file_ = ClipboardName; }
                    }
                }
            },
            { "read=", "Read Mode {<All|Line>} When Input Redirected", v => { if (!string.IsNullOrEmpty(v)) Enum.TryParse(v.ToUpper(), out _ReadInputMode_); } },
        };

        public virtual void InitBackgroundExecute()
        {
            if (!(bgWorking is SemaphoreSlim)) bgWorking = new SemaphoreSlim(1, 1);
            if (!(bgExecuteTask is BackgroundWorker))
            {
                bgExecuteTask = new BackgroundWorker() { WorkerReportsProgress = true, WorkerSupportsCancellation = true };

                bgExecuteTask.DoWork -= bgExecute_DoWork;
                bgExecuteTask.ProgressChanged -= bgExecute_ProgressChanged;
                bgExecuteTask.RunWorkerCompleted -= bgExecute_RunWorkerCompleted;

                bgExecuteTask.DoWork += bgExecute_DoWork;
                bgExecuteTask.ProgressChanged += bgExecute_ProgressChanged;
                bgExecuteTask.RunWorkerCompleted += bgExecute_RunWorkerCompleted;
            }
        }

        internal protected virtual void AppendOptions(OptionSet opts, int index = 1)
        {
            foreach (var opt in opts.Reverse())
            {
                try
                {
                    Options.Insert(index, opt);
                }
                catch (Exception ex) { ShowMessage(ex); }
            }
        }

        public Applet()
        {
            var opts = new OptionSet()
            {
                { $"{Name}" },
            };

            AppendOptions(opts, 0);
        }
       
        static public IEnumerable<string> GetApplets()
        {
            var result = new List<string>();
            Assembly applets = typeof(Applet).Assembly;
            foreach (Type type in applets.GetTypes())
            {
                if (type.BaseType == typeof(Applet))
                    result.Add(type.Name);
            }
            return (result);
        }

        public abstract Applet GetApplet();

        static public Applet GetApplet(string applet = null)
        {
            Applet result = null;
            if (string.IsNullOrEmpty(applet))
            {
                //result = new Applet();
            }
            else
            {
                Assembly applets = typeof(Applet).Assembly;
                foreach (Type type in applets.GetTypes())
                {
                    if (type.BaseType == typeof(Applet) && type.Name.Equals(applet, StringComparison.CurrentCultureIgnoreCase))
                    {
                        result = Assembly.GetAssembly(type).CreateInstance(type.FullName) as Applet;
                    }
                }
            }
            return (result);
        }

        public virtual string GetHelp(string indent = null)
        {
            var result = string.Empty;
            if (Options is OptionSet)
            {
                using (var sw = new StringWriter())
                {
                    if (base.GetType().BaseType == typeof(Applet))
                        Options.WriteOptionDescriptions(sw);
                    result = string.Join(Environment.NewLine, sw.ToString().Trim().Split(LINE_BREAK, StringSplitOptions.None).Select(l => $"{indent}{l}"));
                }
            }
            return (result);
        }

        public virtual bool InSearchPath(string file, out string fullname)
        {
            var result = false;
            fullname = string.Empty;

            try
            {
                if (Path.IsPathRooted(file))
                {
                    result = File.Exists(file);
                    if (result) fullname = file;
                }
                else
                {
                    var plist = Environment.GetEnvironmentVariable("PATH").Split(Path.PathSeparator);
                    foreach (var p in plist)
                    {
                        var f = Path.GetFullPath(Path.Combine(p, file));
                        result = File.Exists(file);
                        if (result) { fullname = file; break; }
                    }
                }
            }
            catch (Exception ex) { ShowMessage(ex, Name); }

            return (result);
        }

        public virtual List<string> ParseOptions(IEnumerable<string> args)
        {
            return (Options.Parse(args));
        }

        public virtual void ShowMessage(string text, string title = null)
        {
            if (_verbose_)
            {
                if (string.IsNullOrEmpty(title))
                    MessageBox.Show(text, Name);
                else
                    MessageBox.Show(text, title);
            }
            _log_.Add(string.IsNullOrEmpty(title) ? text : $"[{title}]{text}");
        }

        public virtual void ShowMessage(Exception ex, string title = null)
        {
            if (ex is ExifException)
            {
                switch ((ex as ExifException).ErrorCode)
                {
                    case ExifErrCode.ImageTypeIsNotSupported: break;
                    default:
                        ShowMessage($"{ex.Message}{Environment.NewLine}{ex.StackTrace}", title);
                        break;
                }
            }
            else ShowMessage($"{ex.Message}{Environment.NewLine}{ex.StackTrace}", title);
        }

        public virtual bool GetReturnValueByStatus(dynamic status)
        {
            var ret = true;
            if (status is bool)
            {
                switch (Status)
                {
                    case STATUS.Yes:
                        ret = status;
                        break;
                    case STATUS.No:
                        ret = !status;
                        break;
                    default:
                        ret = true;
                        break;
                }
            }
            else if (status is string && !string.IsNullOrEmpty(status as string))
            {
                bool value = false;
                if (bool.TryParse((status as string).Trim().Trim(ContentMark).Trim(), out value)) ret = GetReturnValueByStatus(value);
            }
            else if (status is IEnumerable<bool>)
            {
                ret = ((IEnumerable<bool>)status).Count() > 0 ? true : false;
                foreach (var s in (IEnumerable<bool>)status) { ret |= s; };
            }
            else if(status is IEnumerable<string>)
            {
                bool value = false;
                if (bool.TryParse((string.Join(Environment.NewLine, (IEnumerable<string>)status)).Trim().Trim(ContentMark).Trim(), out value))
                    ret = GetReturnValueByStatus(value);
            }
            return (ret);
        }

        public virtual string GetDateLong(DateTime date)
        {
            return (date.ToString($"{DateTimeFormat}, {DateTimeFormatLocal}"));
        }

        public virtual string GetDateLong(DateTime? date)
        {
            return (date.HasValue ? date.Value.ToString($"{DateTimeFormat}, {DateTimeFormatLocal}") : string.Empty);
        }

        public virtual string GetDateLong(DateTime date, bool utc = false)
        {
            if (utc)
                return (date.ToString($"{DateTimeFormatUtc}, {DateTimeFormatLocal}"));
            else
                return (date.ToString($"{DateTimeFormat}, {DateTimeFormatLocal}"));
        }

        public virtual string GetDateLong(DateTime? date, bool utc = false)
        {
            if (utc)
                return (date.HasValue ? date.Value.ToString($"{DateTimeFormatUtc}, {DateTimeFormatLocal}") : string.Empty);
            else
                return (date.HasValue ? date.Value.ToString($"{DateTimeFormat}, {DateTimeFormatLocal}") : string.Empty);
        }

        public virtual dynamic Compare(DateTime src, DateTime dst, CompareMode Mode)
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

        public virtual dynamic Compare(DateTime src, string dst, CompareMode Mode)
        {
            return (Compare(src, new DateValue(dst), Mode));
        }

        public virtual dynamic Compare(DateTime src, DateValue dst, CompareMode Mode)
        {
            return (dst.Compare(src, Mode));
        }

        public virtual dynamic Compare(double src, double dst, CompareMode Mode)
        {
            if (Mode == CompareMode.VALUE)
                return (src);
            else
            {
                var status = false;

                switch (Mode)
                {
                    case CompareMode.EQ:
                        status = src == dst;
                        break;
                    case CompareMode.NEQ:
                        status = src != dst;
                        break;
                    case CompareMode.NOT:
                        status = src != dst;
                        break;
                    case CompareMode.LT:
                        status = src < dst;
                        break;
                    case CompareMode.LE:
                        status = src <= dst;
                        break;
                    case CompareMode.GT:
                        status = src > dst;
                        break;
                    case CompareMode.GE:
                        status = src >= dst;
                        break;
                    default:
                        break;
                }
                return (status);
            }
        }

        public virtual dynamic Compare(double src, IEnumerable<double> dsts, CompareMode Mode)
        {
            if (Mode == CompareMode.VALUE)
                return (src);
            else
            {
                var status = new CompareMode[]{ CompareMode.AND, CompareMode.NOT, CompareMode.NEQ, CompareMode.NONE }.Contains(Mode) ? true : false;
                foreach (var dst in dsts)
                {
                    switch (Mode)
                    {
                        case CompareMode.AND:
                            status &= Compare(src, dst, Mode);
                            break;
                        case CompareMode.OR:
                            status |= Compare(src, dst, Mode);
                            break;
                        case CompareMode.NOT:
                            status &= Compare(src, dst, Mode);
                            break;
                        case CompareMode.EQ:
                            status |= Compare(src, dst, Mode);
                            break;
                        case CompareMode.NEQ:
                            status &= Compare(src, dst, Mode);
                            break;
                        case CompareMode.HAS:
                            status |= Compare(src, dst, Mode);
                            break;
                        case CompareMode.NONE:
                            status &= Compare(src, dst, Mode);
                            break;
                        default:
                            status |= Compare(src, dst, Mode);
                            break;
                    }
                }
                return (status);
            }
        }

        public virtual dynamic Compare(double src, IEnumerable<string> dsts, CompareMode Mode)
        {
            var values = dsts.Select(d => { double v = double.NaN; return (double.TryParse(d, out v) ? v : double.NaN); }).Where(d => !double.IsNaN(d));
            return(Compare(src, values, Mode));
        }

        public virtual dynamic Compare(bool src, bool dst, CompareMode Mode)
        {
            if (Mode == CompareMode.VALUE)
                return (src);
            else
            {
                var status = false;

                switch (Mode)
                {
                    case CompareMode.EQ:
                        status = src == dst;
                        break;
                    case CompareMode.NEQ:
                        status = src != dst;
                        break;
                    case CompareMode.NOT:
                        status = src != dst;
                        break;
                    default:
                        break;
                }
                return (status);
            }
        }

        public virtual dynamic Compare(bool src, IEnumerable<bool> dsts, CompareMode Mode)
        {
            if (Mode == CompareMode.VALUE)
                return (src);
            else
            {
                var status = new CompareMode[]{ CompareMode.AND, CompareMode.NOT, CompareMode.NEQ, CompareMode.NONE }.Contains(Mode) ? true : false;
                foreach (var dst in dsts)
                {
                    switch (Mode)
                    {
                        case CompareMode.AND:
                            status &= Compare(src, dst, Mode);
                            break;
                        case CompareMode.OR:
                            status |= Compare(src, dst, Mode);
                            break;
                        case CompareMode.NOT:
                            status &= Compare(src, dst, Mode);
                            break;
                        case CompareMode.EQ:
                            status |= Compare(src, dst, Mode);
                            break;
                        case CompareMode.NEQ:
                            status &= Compare(src, dst, Mode);
                            break;
                        case CompareMode.HAS:
                            status |= Compare(src, dst, Mode);
                            break;
                        case CompareMode.NONE:
                            status &= Compare(src, dst, Mode);
                            break;
                        default:
                            break;
                    }
                }
                return (status);
            }
        }

        public virtual dynamic Compare(bool src, IEnumerable<string> dsts, CompareMode Mode)
        {
            var values = dsts.Select(d => { bool v = false; return (bool.TryParse(d, out v) ? v : false); });
            return (Compare(src, values, Mode));
        }

        public virtual dynamic Compare(string text, string word, CompareMode Mode, bool? ignorecase = null)
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
                    regex_ignore |= word.EndsWith("/i", StringComparison.CurrentCultureIgnoreCase) ? RegexOptions.IgnoreCase : RegexOptions.None;
                    word = word.Trim(RegexTrimChar);
                }

                switch (Mode)
                {
                    case CompareMode.OR:
                    case CompareMode.AND:
                    case CompareMode.HAS:
                        status = Regex.IsMatch(text, word, regex_ignore);
                        break;
                    case CompareMode.NO:
                    case CompareMode.NOT:
                    case CompareMode.NONE:
                        status = !Regex.IsMatch(text, word, regex_ignore);
                        break;
                    case CompareMode.EQ:
                        //status = regex_ignore == RegexOptions.IgnoreCase ? text.Equals(word, StringComparison.CurrentCultureIgnoreCase) : text.Equals(word);
                        status = string.Compare(text, word, ignorecase ?? false) == 0;
                        break;
                    case CompareMode.NEQ:
                        //status = regex_ignore == RegexOptions.IgnoreCase ? !text.Equals(word, StringComparison.CurrentCultureIgnoreCase) : !text.Equals(word);
                        status = string.Compare(text, word, ignorecase ?? false) != 0;
                        break;
                    case CompareMode.LT:
                        status = string.Compare(text, word, ignorecase ?? false) < 0;
                        break;
                    case CompareMode.LE:
                        status = string.Compare(text, word, ignorecase ?? false) <= 0;
                        break;
                    case CompareMode.GT:
                        status = string.Compare(text, word, ignorecase ?? false) > 0;
                        break;
                    case CompareMode.GE:
                        status = string.Compare(text, word, ignorecase ?? false) >= 0;
                        break;
                    default:
                        break;
                }
                return (status);
            }
        }

        public virtual dynamic Compare(string text, string[] words, CompareMode Mode, bool? ignorecase = null)
        {
            if (Mode == CompareMode.VALUE)
                return (text);
            else
            {
                var status = new CompareMode[]{ CompareMode.AND, CompareMode.NOT, CompareMode.NEQ, CompareMode.NO, CompareMode.NONE }.Contains(Mode) ? true : false;
                foreach (var word in words.Select(w => w.Trim()))
                {
                    switch (Mode)
                    {
                        case CompareMode.AND:
                            status &= Compare(text, word, Mode, ignorecase);
                            break;
                        case CompareMode.OR:
                            status |= Compare(text, word, Mode, ignorecase);
                            break;
                        case CompareMode.NOT:
                            status &= Compare(text, word, Mode, ignorecase);
                            break;
                        case CompareMode.EQ:
                            status |= Compare(text, word, Mode, ignorecase);
                            break;
                        case CompareMode.NEQ:
                            status &= Compare(text, word, Mode, ignorecase);
                            break;
                        case CompareMode.HAS:
                            status |= Compare(text, word, Mode, ignorecase);
                            break;
                        case CompareMode.NO:
                            status &= Compare(text, word, Mode, ignorecase);
                            break;
                        case CompareMode.NONE:
                            status &= Compare(text, word, Mode, ignorecase);
                            break;
                        case CompareMode.LT:
                            status |= Compare(text, word, Mode, ignorecase);
                            break;
                        case CompareMode.LE:
                            status |= Compare(text, word, Mode, ignorecase);
                            break;
                        case CompareMode.GE:
                            status |= Compare(text, word, Mode, ignorecase);
                            break;
                        case CompareMode.GT:
                            status |= Compare(text, word, Mode, ignorecase);
                            break;
                        default:
                            break;
                    }
                }
                return (status);
            }
        }

        private static string NormalizationFileName(string file, int? padding = null)
        {
            var f = Path.GetFileName(file);
            return (Regex.Replace(f, @"\d+", m => m.Value.PadLeft(padding ?? _sortzero_, '0')));
        }

        public static IList<string> NaturalSort(IList<string> list, int? padding = null, bool? descending = null)
        {
            try
            {
                if (descending ?? _descending_)
                    return (list is IList<string> ? list.OrderByDescending(x => Regex.Replace(x, @"\d+", m => m.Value.PadLeft(padding ?? _sortzero_, '0'))).ToList() : list);
                else
                    return (list is IList<string> ? list.OrderBy(x => Regex.Replace(x, @"\d+", m => m.Value.PadLeft(padding ?? _sortzero_, '0'))).ToList() : list);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return (list); }
        }

        public static IList<FileInfo> NaturalSort(IList<FileInfo> list, int? padding = null, bool? descending = null)
        {
            try
            {
                if (descending ?? _descending_)
                    return (list is IList<FileInfo> ? list.OrderByDescending(x => NormalizationFileName(x.FullName, padding)).ToList() : list);
                else
                    return (list is IList<FileInfo> ? list.OrderBy(x => NormalizationFileName(x.FullName, padding)).ToList() : list);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return (list); }
        }

        public static IEnumerable<string> NaturalSort(IEnumerable<string> list, int? padding = null, bool? descending = null)
        {
            try
            {
                if (descending ?? _descending_)
                    return (list is IEnumerable<string> ? list.OrderByDescending(x => Regex.Replace(x, @"\d+", m => m.Value.PadLeft(padding ?? _sortzero_, '0'))) : list);
                else
                    return (list is IEnumerable<string> ? list.OrderBy(x => Regex.Replace(x, @"\d+", m => m.Value.PadLeft(padding ?? _sortzero_, '0'))) : list);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return (list); }
        }

        public static IEnumerable<FileInfo> NaturalSort(IEnumerable<FileInfo> list, int? padding = null, bool? descending = null)
        {
            try
            {
                if (descending ?? _descending_)
                    return (list is IEnumerable<FileInfo> ? list.OrderByDescending(x => NormalizationFileName(x.FullName, padding)) : list);
                else
                    return (list is IEnumerable<FileInfo> ? list.OrderBy(x => NormalizationFileName(x.FullName, padding)) : list);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); return (list); }
        }

        public virtual bool Execute<T>(out T result, params object[] args)
        {
            bool ret = false;
            result = default(T);
            Result.Reset();
            try
            {
                ret = true;
            }
            catch (ExifException ex)
            {
                if (ex is ExifException)
                {
                    ret = false;
                    switch (ex.ErrorCode)
                    {
                        //case ExifErrCode.ExifBlockHasIllegalContent: break;
                        //case ExifErrCode.ExifDataAreTooLarge: break;
                        //case ExifErrCode.ImageHasUnsupportedFeatures: break;
                        //case ExifErrCode.ImageTypesDoNotMatch: break;
                        case ExifErrCode.ImageTypeIsNotSupported: break;
                        case ExifErrCode.InternalError: break;
                        //case ExifErrCode.InternalImageStructureIsWrong: break;
                        default: MessageBox.Show(ex.Message); break;
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            
            Result.Set(string.Empty, OutputFile, ret, result);
            return (ret);
        }

        public virtual bool Execute<T>(string file, out T result, params object[] args)
        {
            bool ret = false;
            result = default(T);
            Result.Reset();
            if (File.Exists(file))
            {
                InputFile = file;
                try
                {
                    using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        //OutputFile = file;
                        ret = Execute<T>(fs, out result);
                    }
                }
                catch (ExifException ex)
                {
                    if (ex is ExifException)
                    {
                        ret = false;
                        switch (ex.ErrorCode)
                        {
                            //case ExifErrCode.ExifBlockHasIllegalContent: break;
                            //case ExifErrCode.ExifDataAreTooLarge: break;
                            //case ExifErrCode.ImageHasUnsupportedFeatures: break;
                            //case ExifErrCode.ImageTypesDoNotMatch: break;
                            case ExifErrCode.ImageTypeIsNotSupported: break;
                            case ExifErrCode.InternalError: break;
                            //case ExifErrCode.InternalImageStructureIsWrong: break;
                            default: MessageBox.Show(ex.Message); break;
                        }
                    }
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
            Result.Set(InputFile, OutputFile, ret, result);
            return (ret);
        }

        public virtual bool Execute<T>(Stream source, out T result, params object[] args)
        {
            bool ret = false;
            result = default(T);
            Result.Reset();
            if (source is Stream && source.CanRead)
            {
                try
                {
                    if (source.CanSeek) source.Seek(0, SeekOrigin.Begin);
                    var exif = new ExifData(source);
                    ret = Execute<T>(exif, out result);
                }
                catch (ExifException ex)
                {
                    if (ex is ExifException)
                    {
                        ret = false;
                        switch (ex.ErrorCode)
                        {
                            //case ExifErrCode.ExifBlockHasIllegalContent: break;
                            //case ExifErrCode.ExifDataAreTooLarge: break;
                            //case ExifErrCode.ImageHasUnsupportedFeatures: break;
                            //case ExifErrCode.ImageTypesDoNotMatch: break;
                            case ExifErrCode.ImageTypeIsNotSupported: break;
                            case ExifErrCode.InternalError: break;
                            //case ExifErrCode.InternalImageStructureIsWrong: break;
                            default: MessageBox.Show(ex.Message); break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
            Result.Set(InputFile, OutputFile, ret, result);
            return (ret);
        }

        public virtual bool Execute<T>(ExifData exif, out T result, params object[] args)
        {
            bool ret = false;
            result = default(T);
            Result.Reset();
            try
            {
                ret = true;
            }
            catch (ExifException ex)
            {
                if (ex is ExifException)
                {
                    ret = false;
                    switch (ex.ErrorCode)
                    {
                        //case ExifErrCode.ExifBlockHasIllegalContent: break;
                        //case ExifErrCode.ExifDataAreTooLarge: break;
                        //case ExifErrCode.ImageHasUnsupportedFeatures: break;
                        //case ExifErrCode.ImageTypesDoNotMatch: break;
                        case ExifErrCode.ImageTypeIsNotSupported: break;
                        case ExifErrCode.InternalError: break;
                        //case ExifErrCode.InternalImageStructureIsWrong: break;
                        default: MessageBox.Show(ex.Message); break;
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            Result.Set(InputFile, OutputFile, ret, result);
            return (ret);
        }

        public virtual IEnumerable<ExecuteResult> Execute(IEnumerable<FileInfo> files, STATUS status = STATUS.Yes, params object[] args)
        {
            Status = status;
            var infolist = new List<ExecuteResult>();
            if (files is IEnumerable<FileInfo>)
            {
                var index = 0;
                var total = files.Count();
                foreach (var file in files)
                {
                    bool result = false;
                    Result.Reset();
                    var ret = Execute(file.FullName, out result, args);
                    //if (ret) infolist.Add(new ExecuteResult() { InputFile = file.FullName, InputInfo = file, State = ret, Message = result });
                    if (ret) infolist.Add(Result.Clone());
                    if (ReportProgress is Action<int, int, ExecuteResult, object, object>) ReportProgress.Invoke(index, total, infolist.Last(), null, null);
                }
                _input_files_ = infolist.Select(i => i.InputFile).ToList();
                _output_files_ = infolist.Select(i => i.OutputFile).ToList();
                _results_ = infolist;
            }
            return (infolist);
        }

        public virtual IEnumerable<ExecuteResult> Execute(IEnumerable<string> files, STATUS status = STATUS.Yes, params object[] args)
        {
            Status = status;
            var infolist = new List<ExecuteResult>();
            if (files is IEnumerable<string>)
            {
                var index = 0;
                var total = files.Count();
                foreach (var file in files)
                {
                    bool result = false;
                    Result.Reset();
                    var ret = Execute(file, out result, args);
                    //if (ret) infolist.Add(new ExecuteResult() { InputFile = file, InputInfo = new FileInfo(file), State = ret, Message = result });
                    if (ret) infolist.Add(Result.Clone());
                    if (ReportProgress is Action<int, int, ExecuteResult, object, object>) ReportProgress.Invoke(index, total, infolist.Last(), null, null);
                }
                _input_files_ = infolist.Select(i => i.InputFile).ToList();
                _output_files_ = infolist.Select(i => i.OutputFile).ToList();
                _results_ = infolist;
            }
            return (infolist);
        }

        #region Background Execute Helper
        public virtual void BackgroundExecuteRun(IEnumerable<FileInfo> files, STATUS status = STATUS.Yes, params object[] args)
        {
            InitBackgroundExecute();
            bgExecuteTask.RunWorkerAsync(new BackgroundExecuteParamter<FileInfo>() { Files = files, ResultList = new List<ExecuteResult>(), Args = args });
        }

        public virtual void BackgroundExecuteRun(IEnumerable<string> files, STATUS status = STATUS.Yes, params object[] args)
        {
            InitBackgroundExecute();
            bgExecuteTask.RunWorkerAsync(new BackgroundExecuteParamter<string>() { Files = files, ResultList = new List<ExecuteResult>(), Args = args });
        }

        public bool BackgroundExecuteCancel()
        {
            var result = false;
            if (bgExecuteTask is BackgroundWorker && bgExecuteTask.IsBusy && !bgExecuteTask.CancellationPending)
            {
                bgExecuteTask.CancelAsync();
                if (bgWorking.Wait(TimeSpan.FromSeconds(5)))
                {
                    bgWorking.Release();
                    result = true;
                }
            }
            return (result);
        }

        public async Task<bool> BackgroundExecuteCancelAsync()
        {
            var result = false;
            if (bgExecuteTask is BackgroundWorker && bgExecuteTask.IsBusy && !bgExecuteTask.CancellationPending)
            {
                bgExecuteTask.CancelAsync();
                if (await bgWorking.WaitAsync(TimeSpan.FromSeconds(5)))
                {
                    bgWorking.Release();
                    result = true;
                }
            }
            return (result);
        }

        private void bgExecute_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (bgExecuteTask is BackgroundWorker)
            {
                bgExecuteTask.ReportProgress(100);

                if (ReportProgress is Action<int, int, ExecuteResult, object, object>) ReportProgress.Invoke(100, 100, null, e.Result, e.Error);

                if (bgWorking is SemaphoreSlim && bgWorking.CurrentCount <= 1) bgWorking.Release();
            }
        }

        private void bgExecute_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (bgExecuteTask is BackgroundWorker)
            {

            }
        }

        private void bgExecute_DoWork(object sender, DoWorkEventArgs e)
        {
            if (bgExecuteTask is BackgroundWorker && bgWorking is SemaphoreSlim && bgWorking.Wait(TimeSpan.FromSeconds(5)))
            {
                try
                {
                    if (e.Argument is BackgroundExecuteParamter<FileInfo>)
                    {
                        var param = e.Argument as BackgroundExecuteParamter<FileInfo>;
                        var args = param.Args;
                        var files = param.Files is IEnumerable<FileInfo> ? param.Files : new List<FileInfo>();
                        var infolist = param.ResultList is IList<ExecuteResult> ? param.ResultList as IList<ExecuteResult> : new List<ExecuteResult>();
                        var index = 0;
                        var total = files.Count();
                        foreach (var file in files)
                        {
                            bool result = false;
                            var ret = Execute(file.FullName, out result, args);
                            if (ret) infolist.Add(new ExecuteResult() { InputFile = file.FullName, InputInfo = file, State = ret, Message = result });
                            index++;
                            bgExecuteTask.ReportProgress((int)Math.Floor(total <= 0 ? 1.0 : index / total));
                            if (ReportProgress is Action<int, int, ExecuteResult, object, object>) ReportProgress.Invoke(index, total, infolist.Last(), e.Result, "");
                        }
                    }
                    else if (e.Argument is BackgroundExecuteParamter<string>)
                    {
                        var param = e.Argument as BackgroundExecuteParamter<string>;
                        var args = param.Args;
                        var files = param.Files is IEnumerable<string> ? param.Files : new List<string>();
                        var infolist = param.ResultList is IList<ExecuteResult> ? param.ResultList as IList<ExecuteResult> : new List<ExecuteResult>();
                        var index = 0;
                        var total = files.Count();
                        foreach (var file in files)
                        {
                            bool result = false;
                            var ret = Execute(file, out result, args);
                            if (ret) infolist.Add(new ExecuteResult() { InputFile = file, InputInfo = new FileInfo(file), State = ret, Message = result });
                            index++;
                            bgExecuteTask.ReportProgress((int)Math.Floor(total <= 0 ? 1.0 : index / total));
                            if (ReportProgress is Action<int, int, ExecuteResult, object, object>) ReportProgress.Invoke(index, total, infolist.Last(), e.Result, "");
                        }
                    }
                }
                catch { }
                finally { if (bgWorking is SemaphoreSlim && bgWorking.CurrentCount <= 1) bgWorking.Release(); }
            }
        }
        #endregion
    }
}
