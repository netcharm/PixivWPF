using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Mono.Options;

namespace ImageApplets.Applets
{
    class OpenWith : Applet
    {
        public override Applet GetApplet()
        {
            return (new OpenWith());
        }

        private string _viewer_ = string.Empty;
        public string Viewer { get { return (_viewer_); } set { _viewer_ = value; } }

        public OpenWith()
        {
            Category = AppletCategory.FileOP;

            var opts = new OptionSet()
            {
                { "v|viewer=", "Custom Specicalfiles Viewer", v => { _viewer_ = v; } },
                { "" },
            };
            AppendOptions(opts);

            if (!string.IsNullOrEmpty(_viewer_) && !Path.IsPathRooted(_viewer_)) InSearchPath(_viewer_, out _viewer_);
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

                    status = (dynamic)true;
                    if (string.IsNullOrEmpty(_viewer_))
                    {
                        System.Diagnostics.Process.Start(InputFile);
                    }
                    else
                    {
                        System.Diagnostics.Process.Start(_viewer_, InputFile);
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
