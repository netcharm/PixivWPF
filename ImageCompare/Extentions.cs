using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ImageMagick;

namespace ImageCompare
{
    public static class Extentions
    {
        public static bool Valid(this MagickImage image)
        {
            return (image is MagickImage && !image.IsDisposed);
        }

        public static bool Invalided(this MagickImage image)
        {
            return (image == null || image.IsDisposed);
        }
    }
}
