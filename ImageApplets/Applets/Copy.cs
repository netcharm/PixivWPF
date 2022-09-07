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

        private string TargetFolder = string.Empty;
        private bool OverWrite = false;
        public Copy()
        {
            Category = AppletCategory.FileOP;

            var opts = new OptionSet()
            {
                { "d|folder=", "Target {Folder}", v => { TargetFolder = v != null ? v : "."; } },
                { "o|overwrite", "Overwrite Exists File", v => { OverWrite = v != null ? true : false; } },
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
                var status = false;
                if (File.Exists(file) && !string.IsNullOrEmpty(TargetFolder))
                {
                    var folder = TargetFolder;

                    if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                        Directory.CreateDirectory(folder);

                    if (Directory.Exists(folder))
                    {
                        var fi = new FileInfo(file);
                        var target = Path.Combine(folder, Path.GetFileName(file));
                        if (OverWrite || !File.Exists(target))
                        {
                            File.Copy(file, target, OverWrite);
                            File.SetCreationTime(target, fi.CreationTime);
                            File.SetLastWriteTime(target, fi.LastWriteTime);
                            File.SetLastAccessTime(target, fi.LastAccessTime);
                            status = true;
                        }
                    }
                }

                ret = GetReturnValueByStatus(status);
                result = (T)(object)status;
            }
            catch (Exception ex) { ShowMessage(ex, Name); }
            return (ret);
        }

    }
}
