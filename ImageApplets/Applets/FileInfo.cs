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
        private CompareMode Mode = CompareMode.VALUE;
        private string TargetValue = null;
        private string SearchScope = "All";
        private string SearchTerm = string.Empty;

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

        private DateValue _date_ = null;

        public override Applet GetApplet()
        {
            return (new FileInfo());
        }

        public FileInfo()
        {
            Category = AppletCategory.FileOP;

            var opts = new OptionSet()
            {
                { "m|mode=", "File Infos Mode {VALUE} : <IS|EQ|NEQ|LT|LE|GT|GE|IN|OUT|AND|OR|NOT|HAS|NONE>", v => { if (v != null) Enum.TryParse(v.ToUpper(), out Mode); } },
                { "c|category=", $"File Search From {{VALUE}} : <{string.Join("|", Categories)}> And more File Attributes. Multiple serach category seprated by ','", v => { if (v != null) SearchScope = v.Trim().Trim('"'); } },
                { "s|search=", "EXIF Search {Term}. Multiple serach keywords seprated by ';' or '#'", v => { if (v != null) SearchTerm = v.Trim().Trim('"'); } },
                { "set|change=", "Will Chanege To {VALUE}, not implemented now.", v => { if (v != null) TargetValue = v.Trim().Trim('"'); } },
                { "" },
            };
            AppendOptions(opts);
        }

        public override List<string> ParseOptions(IEnumerable<string> args)
        {
            var extras = base.ParseOptions(args);

            _date_ = new DateValue(SearchTerm.Split(SplitChar).First());

            return (extras);
        }

        public override bool Execute<T>(string file, out T result, params object[] args)
        {
            var ret = false;
            result = default(T);
            try
            {
                dynamic status = false;
                if (File.Exists(file))
                {
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
                    var cats = SearchScope.Split(SplitChar).Select(c => c.Trim().ToLower()).Distinct().ToList();
                    if (cats.Contains("all"))
                    {
                        cats.AddRange(Categories);
                        cats = cats.Select(c => c.Trim().ToLower()).Distinct().ToList();
                    }

                    status = new CompareMode[] { CompareMode.AND, CompareMode.NOT, CompareMode.NONE }.Contains(Mode) ? true : false;
                    var invert = new CompareMode[]{ CompareMode.NOT, CompareMode.NEQ, CompareMode.NONE }.Contains(Mode) ? false : true;
                    if (!string.IsNullOrEmpty(SearchTerm))
                    {
                        if (Mode == CompareMode.VALUE) Mode = CompareMode.HAS;

                        var word = SearchTerm;
                        var words = Regex.IsMatch(word, IsRegexPattern, RegexOptions.IgnoreCase) ?  word.Trim(RegexTrimChar).Split(SplitChar) : word.Split(SplitChar);

                        if (Mode == CompareMode.AND || Mode == CompareMode.NOT)
                        {
                            foreach (var c in CategoriesText.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                status &= Compare((string)fi_dict[c], words, Mode);
                            }

                            foreach (var c in CategoriesDate.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                status &= Compare(GetDateLong((DateTime)fi_dict[c]), words, Mode);
                            }

                            foreach (var c in CategoriesNumber.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                status &= Compare((double)fi_dict[c], words, Mode);
                            }

                            foreach (var c in CategoriesBool.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                status &= Compare((bool)fi_dict[c], words, Mode);
                            }
                        }
                        else if (Mode == CompareMode.OR)
                        {
                            foreach (var c in CategoriesText.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                status |= Compare((string)fi_dict[c], words, Mode);
                            }

                            foreach (var c in CategoriesDate.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                status |= Compare(GetDateLong((DateTime)fi_dict[c]), words, Mode);
                            }

                            foreach (var c in CategoriesNumber.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                status |= Compare((double)fi_dict[c], words, Mode);
                            }

                            foreach (var c in CategoriesBool.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                status |= Compare((bool)fi_dict[c], words, Mode);
                            }
                        }
                        else if (Mode == CompareMode.LT || Mode == CompareMode.LE ||
                                 Mode == CompareMode.GT || Mode == CompareMode.GE ||
                                 Mode == CompareMode.EQ || Mode == CompareMode.NEQ ||
                                 Mode == CompareMode.HAS || Mode == CompareMode.NONE)
                        {
                            foreach (var c in CategoriesText.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                status |= Compare((string)fi_dict[c], words, Mode);
                            }

                            foreach (var c in CategoriesDate.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                status |= Compare((DateTime)fi_dict[c], _date_, Mode);
                            }

                            foreach (var c in CategoriesNumber.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                status |= Compare((double)fi_dict[c], words, Mode);
                            }

                            foreach (var c in CategoriesBool.Where(cat => cats.Contains(cat.ToLower()) && fi_dict.ContainsKey(cat)))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                status &= Compare((bool)fi_dict[c], words, Mode);
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
                        else if (Mode == CompareMode.OUT && words.Length >= 2)
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
                            sb.AppendLine($"{padding}{key} = {fi_dict[c]}");
                        }
                        status = sb.ToString().Trim();
                    }
                    //status = true;
                }
                ret = GetReturnValueByStatus(status);
                result = (T)(object)status;
            }
            catch (Exception ex) { ShowMessage(ex, Name); }
            return (ret);
        }

    }
}
