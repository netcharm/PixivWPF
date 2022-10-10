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
            "DateCreate", "DC", "DateCreateUtc", "DCU",
            "DateModified", "DM", "DateModifiedUtc", "DMU",
            "DateAccess", "DA", "DateAccessUtc", "DAU",
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
            "DateCreate", "DC", "DateCreateUtc", "DCU",
            "DateModified", "DM", "DateModifiedUtc", "DMU",
            "DateAccess", "DA", "DateAccessUtc", "DAU",
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

            _date_ = new DateValue(SearchTerm);

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
                    var fi = new System.IO.FileInfo(file);
                    var fi_dict = new Dictionary<string, dynamic>(StringComparer.CurrentCultureIgnoreCase)
                    {
                        { "FullName", fi.FullName }, { "Name", fi.Name }, { "Ext", fi.Extension },

                        { "DirectoryName", fi.DirectoryName }, { "Folder", fi.DirectoryName }, { "Dir", fi.DirectoryName },

                        { "Attributes", fi.Attributes }, { "Attr", fi.Attributes },

                        { "Size", fi.Length }, { "Length", fi.Length },

                        { "Date", fi.LastWriteTime }, { "DateUtc", fi.LastWriteTimeUtc },
                        { "DateCreate", fi.CreationTime }, { "DC", fi.CreationTime }, { "DateCreateUtc", fi.CreationTimeUtc }, { "DCU", fi.CreationTimeUtc },
                        { "DateModified", fi.LastWriteTime }, { "DM", fi.LastWriteTime }, { "DateModifiedUtc", fi.LastWriteTimeUtc }, { "DMU", fi.LastWriteTimeUtc },
                        { "DateAccedd", fi.LastAccessTime }, { "DA", fi.LastAccessTime }, { "DateAccessUtc", fi.LastAccessTimeUtc }, { "DAU", fi.LastAccessTimeUtc },

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

                    var cats = SearchScope.Split(',').Select(c => c.Trim().ToLower()).Distinct().ToList();
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
                            foreach (var c in CategoriesText)
                            {
                                if (cats.Contains(c.ToLower()) && fi_dict.ContainsKey(c))
                                {
                                    var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                    status &= Compare(fi_dict[c] as string, words, Mode);
                                }
                            }

                            foreach (var c in CategoriesDate)
                            {
                                if (cats.Contains(c.ToLower()) && fi_dict.ContainsKey(c))
                                {
                                    var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                    status &= Compare(GetDateLong(fi_dict[c]), words, Mode);
                                }
                            }

                            foreach (var c in CategoriesNumber)
                            {
                                if (cats.Contains(c.ToLower()) && fi_dict.ContainsKey(c))
                                {
                                    var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                    double value = double.NaN;
                                    if (double.TryParse(fi_dict[c], out value)) status &= Compare(value, words, Mode);
                                }
                            }

                            foreach (var c in CategoriesBool)
                            {
                                if (cats.Contains(c.ToLower()) && fi_dict.ContainsKey(c))
                                {
                                    var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                    bool value = false;
                                    if (bool.TryParse(fi_dict[c], out value)) status &= Compare(value, words, Mode);
                                }
                            }
                        }
                        else if (Mode == CompareMode.OR)
                        {
                            foreach (var c in CategoriesText)
                            {
                                if (cats.Contains(c.ToLower()) && fi_dict.ContainsKey(c))
                                {
                                    var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                    status |= Compare(fi_dict[c] as string, words, Mode);
                                }
                            }

                            foreach (var c in CategoriesDate)
                            {
                                if (cats.Contains(c.ToLower()) && fi_dict.ContainsKey(c))
                                {
                                    var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                    status |= Compare(GetDateLong(fi_dict[c]), words, Mode);
                                }
                            }

                            foreach (var c in CategoriesNumber)
                            {
                                if (cats.Contains(c.ToLower()) && fi_dict.ContainsKey(c))
                                {
                                    var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                    status |= Compare((double)fi_dict[c], words, Mode);
                                }
                            }

                            foreach (var c in CategoriesBool)
                            {
                                if (cats.Contains(c.ToLower()) && fi_dict.ContainsKey(c))
                                {
                                    var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                    bool value = false;
                                    if (bool.TryParse(fi_dict[c], out value)) status |= Compare(value, words, Mode);
                                }
                            }
                        }
                        else if (Mode == CompareMode.LT || Mode == CompareMode.LE ||
                                 Mode == CompareMode.GT || Mode == CompareMode.GE ||
                                 Mode == CompareMode.EQ || Mode == CompareMode.NEQ ||
                                 Mode == CompareMode.HAS || Mode == CompareMode.NONE)
                        {
                            foreach (var c in CategoriesNumber)
                            {
                                if (cats.Contains(c.ToLower()) && fi_dict.ContainsKey(c))
                                {
                                    var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                    status |= Compare((double)fi_dict[c], words, Mode);
                                }
                            }

                            foreach (var c in CategoriesDate)
                            {
                                if (cats.Contains(c.ToLower()) && fi_dict.ContainsKey(c))
                                {
                                    var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                    status |= _date_.Compare((DateTime)fi_dict[c], Mode);
                                }
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
                                foreach (var c in CategoriesNumber)
                                {
                                    if (cats.Contains(c.ToLower()) && fi_dict.ContainsKey(c))
                                    {
                                        var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                        value = (double)fi_dict[c];
                                        status |= value_low <= value && value <= value_high;
                                    }
                                }
                            }

                            var date_low = new DateValue(words.First());
                            var date_high = new DateValue(words.Last());
                            foreach (var c in CategoriesDate)
                            {
                                if (cats.Contains(c.ToLower()) && fi_dict.ContainsKey(c))
                                {
                                    var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                    status |= date_low.Compare(fi_dict[c], CompareMode.GE) && date_high.Compare(fi_dict[c], CompareMode.LE);
                                }
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
                                foreach (var c in CategoriesNumber)
                                {
                                    if (cats.Contains(c.ToLower()) && fi_dict.ContainsKey(c))
                                    {
                                        var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                        value = (double)fi_dict[c];
                                        status |= value_low > value || value > value_high;
                                    }
                                }
                            }

                            var date_low = new DateValue(words.First());
                            var date_high = new DateValue(words.Last());
                            foreach (var c in CategoriesDate)
                            {
                                if (cats.Contains(c.ToLower()) && fi_dict.ContainsKey(c))
                                {
                                    var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                    status |= date_low.Compare(fi_dict[c], CompareMode.LT) || date_high.Compare(fi_dict[c], CompareMode.GT);
                                }
                            }
                        }
                    }
                    else
                    {
                        var padding = "".PadLeft(ValuePaddingLeft);
                        StringBuilder sb = new StringBuilder();
                        sb.Append("\u20D0");
                        foreach(var c in cats)
                        {
                            if (fi_dict.ContainsKey(c))
                            {
                                var key = keys.Where(k => k.Equals(c, StringComparison.CurrentCultureIgnoreCase)).First();
                                sb.AppendLine($"{padding}{key} = {fi_dict[c]}");
                            }
                        }
                        //if (cats.Contains("fullname")) sb.AppendLine($"{padding}{fi.FullName}");

                        //if (cats.Contains("directory")) sb.AppendLine($"{padding}{fi.DirectoryName}");
                        //else if (cats.Contains("folder")) sb.AppendLine($"{padding}{fi.DirectoryName}");
                        //else if (cats.Contains("dir")) sb.AppendLine($"{padding}{fi.DirectoryName}");

                        //if (cats.Contains("name")) sb.AppendLine($"{padding}{fi.Name}");
                        //if (cats.Contains("ext")) sb.AppendLine($"{padding}{fi.Extension}");

                        //if (cats.Contains("size")) sb.AppendLine($"{padding}{fi.Length}");
                        //else if (cats.Contains("length")) sb.AppendLine($"{padding}{fi.Length}");

                        //if (cats.Contains("datecreate")) sb.AppendLine($"{padding}{GetDateLong(fi.CreationTime)}");
                        //else if (cats.Contains("dc")) sb.AppendLine($"{padding}{GetDateLong(fi.CreationTime)}");
                        //if (cats.Contains("datecreateutc")) sb.AppendLine($"{padding}{GetDateLong(fi.CreationTimeUtc, true)}");
                        //else if (cats.Contains("dcu")) sb.AppendLine($"{padding}{GetDateLong(fi.CreationTimeUtc, true)}");

                        //if (cats.Contains("date")) sb.AppendLine($"{padding}{GetDateLong(fi.LastWriteTime)}");
                        //else if (cats.Contains("datemodified")) sb.AppendLine($"{padding}{GetDateLong(fi.LastWriteTime)}");
                        //else if (cats.Contains("dm")) sb.AppendLine($"{padding}{GetDateLong(fi.LastWriteTime)}");
                        //if (cats.Contains("dateutc")) sb.AppendLine($"{padding}{GetDateLong(fi.LastWriteTimeUtc, true)}");
                        //else if (cats.Contains("datemodifiedutc")) sb.AppendLine($"{padding}{GetDateLong(fi.LastWriteTimeUtc, true)}");
                        //else if (cats.Contains("dmu")) sb.AppendLine($"{padding}{GetDateLong(fi.LastWriteTimeUtc, true)}");

                        //if (cats.Contains("dateaccess")) sb.AppendLine($"{padding}{GetDateLong(fi.LastAccessTime)}");
                        //else if (cats.Contains("da")) sb.AppendLine($"{padding}{GetDateLong(fi.LastAccessTime)}");
                        //if (cats.Contains("dateaccessutc")) sb.AppendLine($"{padding}{GetDateLong(fi.LastAccessTimeUtc, true)}");
                        //else if (cats.Contains("dau")) sb.AppendLine($"{padding}{GetDateLong(fi.LastAccessTimeUtc, true)}");

                        //if (cats.Contains("attributes")) sb.AppendLine($"{padding}{fi.Attributes.ToString()}");
                        //else if (cats.Contains("attribute")) sb.AppendLine($"{padding}{fi.Attributes.ToString()}");
                        //else if (cats.Contains("attr")) sb.AppendLine($"{padding}{fi.Attributes.ToString()}");

                        //if (cats.Contains("attributes")) sb.AppendLine($"{padding}{fi.Attributes.ToString()}");
                        //else if (cats.Contains("attribute")) sb.AppendLine($"{padding}{fi.Attributes.ToString()}");
                        //else if (cats.Contains("attr")) sb.AppendLine($"{padding}{fi.Attributes.ToString()}");

                        //if (cats.Contains("readonly")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.ReadOnly)}");
                        //else if (cats.Contains("r")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.ReadOnly)}");

                        //if (cats.Contains("hidden")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.Hidden)}");
                        //else if (cats.Contains("h")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.Hidden)}");

                        //if (cats.Contains("system")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.System)}");
                        //else if (cats.Contains("s")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.System)}");

                        //if (cats.Contains("directory")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.Directory)}");
                        //else if (cats.Contains("d")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.Directory)}");

                        //if (cats.Contains("archive")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.Archive)}");
                        //else if (cats.Contains("a")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.Archive)}");

                        //if (cats.Contains("device")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.Device)}");
                        //else if (cats.Contains("dev")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.Device)}");

                        //if (cats.Contains("normal")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.Normal)}");
                        //else if (cats.Contains("n")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.Normal)}");

                        //if (cats.Contains("temporary")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.Temporary)}");
                        //else if (cats.Contains("t")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.Temporary)}");

                        //if (cats.Contains("sparsefile")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.SparseFile)}");
                        //else if (cats.Contains("sf")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.SparseFile)}");

                        //if (cats.Contains("reparsepoint")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.ReparsePoint)}");
                        //else if (cats.Contains("rp")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.ReparsePoint)}");

                        //if (cats.Contains("compressed")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.Compressed)}");
                        //else if (cats.Contains("c")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.Compressed)}");

                        //if (cats.Contains("offline")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.Offline)}");
                        //else if (cats.Contains("o")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.Offline)}");

                        //if (cats.Contains("notcontentindexed")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.NotContentIndexed)}");
                        //else if (cats.Contains("nci")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.NotContentIndexed)}");

                        //if (cats.Contains("encrypted")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.Encrypted)}");
                        //else if (cats.Contains("e")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.Encrypted)}");

                        //if (cats.Contains("integritystream")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.IntegrityStream)}");
                        //else if (cats.Contains("is")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.IntegrityStream)}");

                        //if (cats.Contains("noscrubdata")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.NoScrubData)}");
                        //else if (cats.Contains("ncd")) sb.AppendLine($"{padding}{fi.Attributes.HasFlag(FileAttributes.NoScrubData)}");


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
