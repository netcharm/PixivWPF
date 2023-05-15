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
                { "w|targetfile", "Out Target File Name", v => { _TargetName_ = true; } },
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
                if (File.Exists(file) && !string.IsNullOrEmpty(_TargetFolder_))
                {
                    InputFile = file;

                    var folder = _TargetFolder_;

                    if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                        Directory.CreateDirectory(folder);

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
                                status = _TargetName_ ? (dynamic)OutputFile : (dynamic)true;
                            }
                            //var fo = fi.CopyTo(OutputFile, overwrite: OverWrite);
                            //fo.Refresh();
                            //if (fo.Exists)
                            //{
                            //    fo.CreationTime = fi.CreationTime;
                            //    fo.LastWriteTime = fi.LastWriteTime;
                            //    fo.LastAccessTime = fi.LastAccessTime;
                            //    fo.Refresh();
                            //}
                        }
                    }
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
