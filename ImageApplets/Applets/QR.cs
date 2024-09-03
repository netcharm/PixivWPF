using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Mono.Options;
using QRCoder;

namespace ImageApplets.Applets
{
    class QR : Applet
    {
        public override Applet GetApplet()
        {
            return (new QR());
        }

        public override bool Sorting { get { return (false); } }
        public bool OverWrite { get; set; } = false;
        public string Content { get; set; } = null;
        public string TargetName { get; set; } = null;
        public QRCodeGenerator.ECCLevel Quality { get; set; } = QRCodeGenerator.ECCLevel.Q;

        public QR()
        {
            Category = AppletCategory.ImageGeneration;

            var opts = new OptionSet()
            {
                { "c|text=", "Content Text", v => { Content = !string.IsNullOrEmpty(v) ? v : null; } },
                { "o|overwrite", "Overwrite Exists File", v => { OverWrite = true; } },
                { "w|out=", "Output File Name", v => { TargetName = !string.IsNullOrEmpty(v) ? v : null; } },
                { "e|ecclevel=", "QRCode ECC Level (H, Q, M, L)", v => { Quality = (QRCodeGenerator.ECCLevel)(!string.IsNullOrEmpty(v) ? Enum.Parse(typeof(QRCodeGenerator.ECCLevel), v.ToUpper()) : QRCodeGenerator.ECCLevel.Q); } },
                { "" },
            };
            AppendOptions(opts);
        }

        public override bool Execute<T>(out T result, params object[] args)
        {
            var ret = false;
            result = default(T);
            try
            {
                Result.Reset();
                dynamic status = false;
                if (!string.IsNullOrEmpty(Content))
                {
                    using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
                    using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(Content, Quality))
                    using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))
                    {
                        byte[] qrCodeImage = qrCode.GetGraphic(20);
                        if (string.IsNullOrEmpty(TargetName))
                        {
                            BitmapImage bmp;
                            using (var ms = new MemoryStream(qrCodeImage))
                            {
                                ms.Seek(0, SeekOrigin.Begin);
                                bmp = new BitmapImage();
                                bmp.BeginInit();
                                bmp.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                                bmp.CacheOption = BitmapCacheOption.OnLoad;
                                bmp.StreamSource = ms;
                                bmp.EndInit();
                                bmp.Freeze();
                            }

                            Thread thread = new Thread(() =>
                            {
                                var win = new Window
                                {
                                    Title = Content,
                                    Icon = new BitmapImage(new Uri("pack://application:,,,/ImageApplets;component/Resources/qrcode.png", UriKind.RelativeOrAbsolute)),
                                    Background = new ImageBrush(bmp),
                                    Width = bmp.Width,
                                    Height = bmp.Height,
                                    MaxWidth = 1280,
                                    MaxHeight = 1280,
                                    ResizeMode = ResizeMode.CanMinimize,
                                    ShowActivated = true,
                                    ShowInTaskbar = true,
                                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                                };
                                win.PreviewKeyDown += (s,e) => { if (e.Key == Key.Escape) win.Close() ;};
                                win.ShowDialog();
                            });
                            thread.SetApartmentState(ApartmentState.STA);
                            thread.Start();
                        }
                        else
                        {
                            var invalid_path_chars = Path.GetInvalidPathChars();
                            var invalid_file_chars = Path.GetInvalidFileNameChars();
                            var target_path = Regex.Replace(Path.GetDirectoryName(TargetName), $@"[{string.Join("", invalid_path_chars)}]", "_", RegexOptions.IgnoreCase);
                            var target_file = Regex.Replace(Path.GetFileName(TargetName), $@"[{string.Join("", invalid_file_chars)}]", "_", RegexOptions.IgnoreCase);
                            target_file = Path.Combine(target_path, target_file);

                            if (!string.IsNullOrEmpty(target_path) && !Directory.Exists(target_path)) Directory.CreateDirectory(target_path);
                            if (!string.IsNullOrEmpty(target_file) && (OverWrite || !File.Exists(target_file))) File.WriteAllBytes(target_file, qrCodeImage);
                        }
                        ret = true;
                    }
                }
            }
            catch (Exception ex) { ShowMessage(ex, Name); }
            return (ret);
        }

    }
}
