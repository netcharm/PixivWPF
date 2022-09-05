using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using Mono.Options;
using CompactExifLib;

namespace ImageApplets.Applets
{
    class HasExif : Applet
    {
        public override Applet GetApplet()
        {
            return (new HasExif());
        }

        public override bool Execute<T>(ExifData exif, out T result, params object[] args)
        {
            var ret = false;
            result = default(T);
            try
            {
                if (exif != null)
                {
                    var status = false;
                    if (exif.ImageFileBlockExists(ImageFileBlock.Exif)) status = true;
                    else if (exif.ImageFileBlockExists(ImageFileBlock.Xmp)) status = true;
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
