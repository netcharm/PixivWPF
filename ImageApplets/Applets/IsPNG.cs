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
    class IsPNG : Applet
    {
        public override Applet GetApplet()
        {
            return (new IsPNG());
        }

        public override bool Execute<T>(Stream source, out T result, params object[] args)
        {
            var ret = false;
            result = default(T);
            try
            {
                if (source is Stream && source.CanRead)
                {
                    var status = false;
                    if (source.CanSeek) source.Seek(0, SeekOrigin.Begin);
                    using (Image image = Image.FromStream(source))
                    {
                        if (image is Image && image.RawFormat.Guid.Equals(ImageFormat.Png.Guid))
                        {
                            status = true;
                        }
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
            catch (Exception ex) { ShowMessage(ex, Name); }
            return (ret);
        }
    }
}
