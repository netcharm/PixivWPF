using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Mono.Options;

namespace ImageApplets.Applets
{
    class Copy : Applet
    {
        public override Applet GetApplet()
        {
            return (new Copy());
        }
        
        private string _TargetFolder_ = string.Empty;
        public string TargetFolder { get { return (_TargetFolder_); }  set { _TargetFolder_ = value; } }
        private List<string> _TargetFolders_ = new List<string>();
        public IList<string> TargetFolders { get { return (_TargetFolders_); } set { _TargetFolders_ = (List<string>)value; } }
        private bool _OverWrite_ = false;
        public bool OverWrite { get { return (_OverWrite_); } set { _OverWrite_ = value; } }
        private bool _TargetName_ = false;
        public bool TargetName { get { return (_TargetName_); } set { _TargetName_ = value; } }

        public Copy()
        {
            Category = AppletCategory.FileOP;

            var opts = new OptionSet()
            {
                { "d|folder=", "Target {Folder}", v => { _TargetFolder_ = !string.IsNullOrEmpty(v) ? v : "."; } },
                { "o|overwrite", "Overwrite Exists File", v => { _OverWrite_ = true; } },
                { "s|showtarget", "Out Target File Name", v => { _TargetName_ = true; } },
                { "" },
            };
            AppendOptions(opts);
        }

        public override bool Execute<T>(string file, out T result, params object[] args)
        {
            var ret = false;
            result = default(T);
            try
            {
                Result.Reset();
                dynamic status = false;
                var OutputFiles = new Dictionary<string, dynamic>();
                if (File.Exists(file) && !string.IsNullOrEmpty(_TargetFolder_))
                {
                    InputFile = file;

                    _TargetFolders_.AddRange(_TargetFolder_.Split(new char[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries).Select(f => f.Trim('"')));
                    foreach (var folder in NaturalSort(_TargetFolders_.Distinct(), padding: SortZero, descending: Descending))
                    {
                        if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                        {
                            if (Directory.GetLogicalDrives().Contains(Path.GetPathRoot(Path.GetFullPath(folder))))
                                Directory.CreateDirectory(folder);
                        }

                        if (Directory.Exists(folder))
                        {
                            var fi = new System.IO.FileInfo(file);
                            OutputFile = Path.Combine(folder, Path.GetFileName(file));
                            if (_OverWrite_ || !File.Exists(OutputFile))
                            {
                                File.Copy(file, OutputFile, _OverWrite_);
                                if (File.Exists(OutputFile))
                                {
                                    File.SetCreationTime(OutputFile, fi.CreationTime);
                                    File.SetLastWriteTime(OutputFile, fi.LastWriteTime);
                                    File.SetLastAccessTime(OutputFile, fi.LastWriteTime);
                                    //status = _TargetName_ ? (dynamic)OutputFile : (dynamic)true;
                                    OutputFiles.Add(OutputFile, true);
                                }
                            }
                        }
                    }
                }

                if (OutputFiles.Count > 0)
                {
                    var padding = "".PadLeft(ValuePaddingLeft);
                    status = _TargetName_ ? OutputFiles.Select(o => $"{padding}{o.Key}") : OutputFiles.Select(o => $"{padding}{o.Value}");
                    ret = GetReturnValueByStatus(status);
                    //result = (T)(object)(string.Join($"{Path.PathSeparator}", ((IEnumerable<dynamic>)status).ToList()) + Path.PathSeparator);
                    result = (T)(object)(string.Join(Environment.NewLine, ((IEnumerable<dynamic>)status).ToList()).Trim());

                    //Result.Set(InputFile, string.Join($"{Path.PathSeparator}", OutputFiles.Select(o => o.Key)) + Path.PathSeparator, ret, result);
                    Result.Set(InputFile, string.Join(Environment.NewLine, OutputFiles.Select(o => $"{padding}{o.Key}")).Trim(), ret, result);
                }
                else
                {
                    status = false;
                    ret = GetReturnValueByStatus(status);
                    result = (T)(object)(status);

                    Result.Set(InputFile, string.Empty, ret, result);
                }
            }
            catch (Exception ex) { ShowMessage(ex, Name); }
            return (ret);
        }

    }
}
