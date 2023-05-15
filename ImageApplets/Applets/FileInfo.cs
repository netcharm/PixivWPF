using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Mono.Options;
using System.Text.RegularExpressions;
using System.Globalization;

namespace ImageApplets.Applets
{
    class FileInfo : Applet
    {
        private CompareMode _Mode_ = CompareMode.VALUE;
        private string _TargetValue_ = null;
        public string TargetValue { get { return (_TargetValue_); } set { _TargetValue_ = value; } }
        private string _SearchScope_ = "All";
        public string SearchScope { get { return (_SearchScope_); } set { _SearchScope_ = value; } }
        private string _SearchTerm_ = string.Empty;
        public string SearchTerm { get { return (_SearchTerm_); } set { _SearchTerm_ = value; } }
        private DateValue _date_ = null;
        public DateValue Date { get { return (_date_); } set { _date_ = value; } }
        private bool _raw_out_ = false;
        public bool RawOut { get { return (_raw_out_); } set { _raw_out_ = value; } }

        private List<string> CategoriesText  = new List<string>()
        {
            "FullName", "DirectoryName", "Folder", "Dir", "Name", "Ext",
            "Attribute", "Attr",
        };
        private List<string> CategoriesNumber  = new List<string>()
        {
            "Length", "Size",
        };
        private List<string> CategoriesDate  = new List<string>()
        {
            "Date", "DateUtc",
            "DateCreate", "DC", "DCL", "DateCreateUtc", "DCU",
            "DateModified", "DM", "DML", "DateModifiedUtc", "DMU",
            "DateAccess", "DA", "DAL", "DateAccessUtc", "DAU",
        };
        private List<string> CategoriesBool  = new List<string>()
        {
            "ReadOnly", "R", "Hidden", "H", "System", "S", "Directory", "D", "Archive", "A", "Compressed", "C", "Encrypted", "E",
            "Device", "DEV", "Normal", "N", "Temporary", "T", "SparseFile", "SF",
            "ReparsePoint", "RP", "Offline", "O", "NotContentIndexed", "NCI", "IntegrityStream", "IS", "NoScrubData", "NSD",
        };
        private List<string> Categories  = new List<string>()
        {
            "FullName", "DirectoryName", "Folder", "Dir", "Name", "Ext",
            "Length", "Size",

            "Date", "DateUtc",
            "DateCreate", "DC", "DCL", "DateCreateUtc", "DCU",
            "DateModified", "DM", "DML", "DateModifiedUtc", "DMU",
            "DateAccess", "DA", "DAL", "DateAccessUtc", "DAU",

            "Attribute", "Attr",
            "ReadOnly", "R", "Hidden", "H", "System", "S", "Directory", "D", "Archive", "A", "Compressed", "C", "Encrypted", "E",
            "Device", "DEV", "Normal", "N", "Temporary", "T", "SparseFile", "SF",
            "ReparsePoint", "RP", "Offline", "O", "NotContentIndexed", "NCI", "IntegrityStream", "IS", "NoScrubData", "NSD",
            "All"
        };

        private List<string> _CategoriesText_ = null;
        private List<string> _CategoriesNumber_ = null;
        private List<string> _CategoriesDate_ = null;
        private List<string> _CategoriesBool_ = null;
        private List<string> _Categories_ = null;

        private bool IsText(string key)
        {
            if (_CategoriesText_ == null) _CategoriesText_ = CategoriesText.Select(c => c.ToLower()).ToList();
            return (_CategoriesText_.Contains(key.ToLower()));
        }

        private bool IsNumber(string key)
        {
            if (_CategoriesNumber_ == null) _CategoriesNumber_ = CategoriesNumber.Select(c => c.ToLower()).ToList();
            return (_CategoriesNumber_.Contains(key.ToLower()));
        }

        private bool IsDate(string key)
        {
            if (_CategoriesDate_ == null) _CategoriesDate_ = CategoriesDate.Select(c => c.ToLower()).ToList();
            return (_CategoriesDate_.Contains(key.ToLower()));
        }

        private bool IsBool(string key)
        {
            if (_CategoriesBool_ == null) _CategoriesBool_ = CategoriesBool.Select(c => c.ToLower()).ToList();
            return (_CategoriesBool_.Contains(key.ToLower()));
        }

        private bool IsOther(string key)
        {
            if (_Categories_ == null) _Categories_ = Categories.Select(c => c.ToLower()).ToList();
            return (_Categories_.Contains(key.ToLower()));
        }

        public override Applet GetApplet()
        {
            return (new FileInfo());
        }

        public FileInfo()
        {
            Category = AppletCategory.FileOP;

            var opts = new OptionSet()
            {
                { "m|mode=", "File Infos Mode {VALUE} : <IS|EQ|NEQ|LT|LE|GT|GE|IN|OUT|AND|OR|NOT|HAS|NONE>", v => { if (!string.IsNullOrEmpty(v)) Enum.TryParse(v.ToUpper(), out _Mode_); } },
                { "c|category=", $"File Search From {{VALUE}} : <{string.Join("|", Categories)}> And more File Attributes. Multiple serach category seprated by ','", v => { if (!string.IsNullOrEmpty(v)) _SearchScope_ = v.Trim().Trim('"'); } },
                { "s|search=", "EXIF Search {Term}. Multiple serach keywords seprated by ';' or '#'", v => { if (!string.IsNullOrEmpty(v)) _SearchTerm_ = v.Trim().Trim('"'); } },
                { "set|change=", "Will Chanege To {VALUE}, not implemented now.", v => { if (!string.IsNullOrEmpty(v)) _TargetValue_ = v.Trim().Trim('"'); } },
                { "raw", "Will Output WIth RAW data. Otherwise will output with format, such as number with ',' for thousand", v => { _raw_out_ = true; } },
                { "" },
            };
            AppendOptions(opts);
        }

        public override List<string> ParseOptions(IEnumerable<string> args)
        {
            var extras = base.ParseOptions(args);

            _date_ = new DateValue(_SearchTerm_.Split(SplitChar).First());

            return (extras);
        }

        public override bool Execute<T>(string file, out T result, params object[] args)
        {
            var ret = false;
            result = default(T);
            try
            {
                Result.Reset();

                dynamic status = false;
                if (File.Exists(file))
                {
                    InputFile = file;
                    OutputFile = file;

                    #region init file informations dictionary
                    var fi = new System.IO.FileInfo(file);
                    var fi_dict = new Dictionary<string, dynamic>(StringComparer.CurrentCultureIgnoreCase)
                    {
                        { "FullName", fi.FullName }, { "Name", fi.Name }, { "Ext", fi.Extension },

                        { "DirectoryName", fi.DirectoryName }, { "Folder", fi.DirectoryName }, { "Dir", fi.DirectoryName },

                        { "Attributes", fi.Attributes.ToString() }, { "Attr", fi.Attributes.ToString() },

                        { "Size", fi.Length }, { "Length", fi.Length },

                        { "Date", fi.LastWriteTime }, { "DateUtc", fi.LastWriteTimeUtc },
                        { "DateCreate", fi.CreationTime }, { "DC", fi.CreationTime }, { "DCL", fi.CreationTime },
                        { "DateCreateUtc", fi.CreationTimeUtc }, { "DCU", fi.CreationTimeUtc },
                        { "DateModified", fi.LastWriteTime }, { "DM", fi.LastWriteTime }, { "DML", fi.LastWriteTime },
                        { "DateModifiedUtc", fi.LastWriteTimeUtc }, { "DMU", fi.LastWriteTimeUtc },
                        { "DateAccedd", fi.LastAccessTime }, { "DA", fi.LastAccessTime }, { "DAL", fi.LastAccessTime },
                        { "DateAccessUtc", fi.LastAccessTimeUtc }, { "DAU", fi.LastAccessTimeUtc },

                        { "ReadOnly", fi.Attributes.HasFlag(FileAttributes.ReadOnly) }, { "R", fi.Attributes.HasFlag(FileAttributes.ReadOnly) },
                        { "Hidden", fi.Attributes.HasFlag(FileAttributes.Hidden) }, { "H", fi.Attributes.HasFlag(FileAttributes.Hidden) },
                        { "System", fi.Attributes.HasFlag(FileAttributes.System) }, { "S", fi.Attributes.HasFlag(FileAttributes.System) },
                        { "Directory", fi.Attributes.HasFlag(FileAttributes.Directory) }, { "D", fi.Attributes.HasFlag(FileAttributes.Directory) },
                        { "Archive", fi.Attributes.HasFlag(FileAttributes.Archive) }, { "A", fi.Attributes.HasFlag(FileAttributes.Archive) },
                        { "Compressed", fi.Attributes.HasFlag(FileAttributes.Compressed) }, { "C", fi.Attributes.HasFlag(FileAttributes.Compressed) },
                        { "Encrypted", fi.Attributes.HasFlag(FileAttributes.Encrypted) }, { "E", fi.Attributes.HasFlag(FileAttributes.Encrypted) },
                        { "Device", fi.Attributes.HasFlag(FileAttributes.Device) }, { "DEV", fi.Attributes.HasFlag(FileAttributes.Device) },
                        { "Normal", fi.Attributes.HasFlag(FileAttributes.Normal) }, { "N", fi.Attributes.HasFlag(FileAttributes.Normal) },
                        { "Temporary", fi.Attributes.HasFlag(FileAttributes.Temporary) }, { "T", fi.Attributes.HasFlag(FileAttributes.Temporary) },
                        { "SparseFile", fi.Attributes.HasFlag(FileAttributes.SparseFile) }, { "SF", fi.Attributes.HasFlag(FileAttributes.SparseFile) },
                        { "ReparsePoint", fi.Attributes.HasFlag(FileAttributes.ReparsePoint) }, { "RP", fi.Attributes.HasFlag(FileAttributes.ReparsePoint) },
                        { "Offline", fi.Attributes.HasFlag(FileAttributes.Offline) }, { "O", fi.Attributes.HasFlag(FileAttributes.Offline) },
                        { "NotContentIndexed", fi.Attributes.HasFlag(FileAttributes.NotContentIndexed) }, { "NCI", fi.Attributes.HasFlag(FileAttributes.NotContentIndexed) },
                        { "IntegrityStream", fi.Attributes.HasFlag(FileAttributes.IntegrityStream) }, { "IS", fi.Attributes.HasFlag(FileAttributes.IntegrityStream) },
                        { "NoScrubData", fi.Attributes.HasFlag(FileAttributes.NoScrubData) }, { "NSD", fi.Attributes.HasFlag(FileAttributes.NoScrubData) },
                    };
                    var keys = fi_dict.Keys.ToList();
                    #endregion

                    //var cats = SearchScope.Split(SplitChar.Concat(new char[] { ',' }).ToArray()).Select(c => c.Trim().ToLower()).Distinct().ToList();
                    var cats = _SearchScope_.Split(SplitChar).Select(c => c.Trim().ToLower()).Distinct().ToList();
                    if (cats.Contains("all"))
                    {
                        cats.AddRange(Categories);
                        cats = cats.Select(c => c.Trim().ToLower()).Distinct().ToList();
                    }

                    status = new CompareMode[] { CompareMode.AND, CompareMode.NOT, CompareMode.NONE }.Contains(_Mode_) ? true : false;
                    var invert = new CompareMode[]{ CompareMode.NOT, CompareMode.NEQ, CompareMode.NONE }.Contains(_Mode_) ? false : true;
                    if (!string.IsNullOrEmpty(_SearchTerm_))
                    {
                        if (_Mode_ == CompareMode.VALUE) _Mode_ = CompareMode.HAS;

                        var word = _SearchTerm_;
                        var words = Regex.IsMatch(word, IsRegexPattern, RegexOptions.IgnoreCase) ?  word.Trim(RegexTrimChar).Split(SplitChar) : word.Split(SplitChar);

                        if (_Mode_ == CompareMode.AND || _Mode_ == CompareMode.NOT)
                        {
                            foreach (var c in CategoriesText.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                status &= Compare((string)fi_dict[c], words, _Mode_);
                            }

                            foreach (var c in CategoriesDate.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                status &= Compare(GetDateLong((DateTime)fi_dict[c]), words, _Mode_);
                            }

                            foreach (var c in CategoriesNumber.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                status &= Compare((double)fi_dict[c], words, _Mode_);
                            }

                            foreach (var c in CategoriesBool.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                status &= Compare((bool)fi_dict[c], words, _Mode_);
                            }
                        }
                        else if (_Mode_ == CompareMode.OR)
                        {
                            foreach (var c in CategoriesText.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                status |= Compare((string)fi_dict[c], words, _Mode_);
                            }

                            foreach (var c in CategoriesDate.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                status |= Compare(GetDateLong((DateTime)fi_dict[c]), words, _Mode_);
                            }

                            foreach (var c in CategoriesNumber.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                status |= Compare((double)fi_dict[c], words, _Mode_);
                            }

                            foreach (var c in CategoriesBool.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                status |= Compare((bool)fi_dict[c], words, _Mode_);
                            }
                        }
                        else if (_Mode_ == CompareMode.LT || _Mode_ == CompareMode.LE ||
                                 _Mode_ == CompareMode.GT || _Mode_ == CompareMode.GE ||
                                 _Mode_ == CompareMode.EQ || _Mode_ == CompareMode.NEQ ||
                                 _Mode_ == CompareMode.HAS || _Mode_ == CompareMode.NONE)
                        {
                            foreach (var c in CategoriesText.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                status |= Compare((string)fi_dict[c], words, _Mode_);
                            }

                            foreach (var c in CategoriesDate.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                status |= Compare((DateTime)fi_dict[c], _date_, _Mode_);
                            }

                            foreach (var c in CategoriesNumber.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                status |= Compare((double)fi_dict[c], words, _Mode_);
                            }

                            foreach (var c in CategoriesBool.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                status &= Compare((bool)fi_dict[c], words, _Mode_);
                            }
                        }
                        else if (_Mode_ == CompareMode.IN && words.Length >= 2)
                        {
                            var values = words.Select(w => { double v = double.NaN; return(double.TryParse(w, out v) ? v : double.NaN); }).Where(d => !double.IsNaN(d)).OrderBy(d => d);
                            if (values.Count() >= 2)
                            {
                                double value = double.NaN;
                                double value_low = values.First();
                                double value_high = values.Last();
                                foreach (var c in CategoriesNumber.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                                {
                                    var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                    value = (double)fi_dict[c];
                                    status |= value_low <= value && value <= value_high;
                                }
                            }

                            var date_low = new DateValue(words.First());
                            var date_high = new DateValue(words.Last());
                            foreach (var c in CategoriesDate.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                status |= date_low <= fi_dict[c] && fi_dict[c] <= date_high;
                            }

                        }
                        else if (_Mode_ == CompareMode.OUT && words.Length >= 2)
                        {
                            var values = words.Select(w => { double v = double.NaN; return(double.TryParse(w, out v) ? v : double.NaN); }).Where(d => !double.IsNaN(d)).OrderBy(d => d);
                            if (values.Count() >= 2)
                            {
                                double value = double.NaN;
                                double value_low = values.First();
                                double value_high = values.Last();
                                foreach (var c in CategoriesNumber.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                                {
                                    var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                    value = (double)fi_dict[c];
                                    status |= value_low > value || value > value_high;
                                }
                            }

                            var date_low = new DateValue(words.First());
                            var date_high = new DateValue(words.Last());
                            foreach (var c in CategoriesDate.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                status |= date_low > fi_dict[c] || fi_dict[c] > date_high;
                            }
                        }
                    }
                    else
                    {
                        var padding = "".PadLeft(ValuePaddingLeft);
                        StringBuilder sb = new StringBuilder();
                        sb.Append("\u20D0");
                        foreach (var c in cats.Where(cat => fi_dict.ContainsKey(cat)))
                        {
                            var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                            if (!_raw_out_ && IsNumber(c))
                                sb.AppendLine($"{padding}{key} = {string.Format("{0:N0}", fi_dict[c])}");
                            else if (!_raw_out_ && IsBool(c))
                                sb.AppendLine($"{padding}{key} = {fi_dict[c]}");
                            else
                                sb.AppendLine($"{padding}{key} = {fi_dict[c]}");
                        }
                        status = sb.ToString().Trim();
                    }
                    //status = true;
                }
                ret = GetReturnValueByStatus(status);
                result = (T)(object)status;

                Result.Set(InputFile, OutputFile, ret, result);
            }
            catch (Exception ex) { ShowMessage(ex, Name); }
            return (ret);
        }

    }
}
