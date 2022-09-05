using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageApplets.Applets
{
    class IsJPG : Applet
    {
        public override Applet GetApplet()
        {
            return (new IsJPG());
        }

        public override bool Execute<T>(string file, out T result, params object[] args)
        {
            var ret = false;
            result = default(T);
            try
            {
                if (File.Exists(file))
                {
                    using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var status = false;
                        fs.Seek(0, SeekOrigin.Begin);
                        Image image = Image.FromStream(fs);
                        if (image is Image && image.RawFormat.Guid.Equals(ImageFormat.Jpeg.Guid))
                        {
                            status = true;
                        }
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
                        result = (T)(object)status;
                    }
                }
            }
            catch (Exception ex) { ShowMessage(ex, Name); }
            return (ret);
        }
    }
}
