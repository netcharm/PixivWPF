using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Mono.Options;
using System.IO;

namespace ImageApplets.Applets
{
    class Move : Applet
    {
        public override Applet GetApplet()
        {
            return (new Move());
        }

        private string TargetFolder = string.Empty;
        private bool OverWrite = false;
        public Move()
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
                Result.Reset();

                var status = false;
                if (File.Exists(file) && !string.IsNullOrEmpty(TargetFolder))
                {
                    InputFile = file;

                    var folder = TargetFolder;

                    if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                        Directory.CreateDirectory(folder);

                    if (Directory.Exists(folder))
                    {
                        var fi = new System.IO.FileInfo(file);
                        OutputFile = Path.Combine(folder, Path.GetFileName(file));
                        if (File.Exists(OutputFile)) File.Delete(OutputFile);
                        File.Move(file, OutputFile);
                        File.SetCreationTime(OutputFile, fi.CreationTime);
                        File.SetLastWriteTime(OutputFile, fi.LastWriteTime);
                        File.SetLastAccessTime(OutputFile, fi.LastAccessTime);
                        status = true;
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
