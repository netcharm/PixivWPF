﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageApplets.Applets
{
    class IsBMP : Applet
    {
        public override Applet GetApplet()
        {
            return (new IsBMP());
        }

        public IsBMP()
        {
            Category = AppletCategory.ImageType;
        }

        public override bool Execute<T>(Stream source, out T result, params object[] args)
        {
            var ret = false;
            result = default(T);
            try
            {
                Result.Reset();
                if (source is Stream && source.CanRead)
                {
                    var status = false;
                    if (source.CanSeek) source.Seek(0, SeekOrigin.Begin);
                    using (Image image = Image.FromStream(source))
                    {
                        if (image is Image && image.RawFormat.Guid.Equals(ImageFormat.Bmp.Guid))
                        {
                            status = true;
                        }
                    }

                    ret = GetReturnValueByStatus(status);
                    result = (T)(object)status;
                }
                Result.Set(InputFile, OutputFile, ret, result);
            }
            catch (Exception ex) { ShowMessage(ex, Name); }
            return (ret);
        }
    }
}
