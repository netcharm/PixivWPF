﻿using System;
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

        public enum QRTypes { Art, Bmp, Pdf, Png, Ascii, Base64, PostScript, Svg }

        public override bool Sorting { get { return (false); } }
        public bool OverWrite { get; set; } = false;
        public string Content { get; set; } = null;
        public string TargetName { get; set; } = null;
        private QRCodeGenerator.ECCLevel _quality_ = QRCodeGenerator.ECCLevel.Q;
        public QRCodeGenerator.ECCLevel Quality { get { return (_quality_); } set { _quality_ = value; } }
        private QRTypes _qrtype_ = QRTypes.Png;
        public QRTypes QRType { get { return (_qrtype_); } set { _qrtype_ = value; } }
        private int _pixels_ = 20;
        public int Pixels { get { return (_pixels_); } set { _pixels_ = value; } }


        public QR()
        {
            Category = AppletCategory.ImageGeneration;

            var opts = new OptionSet()
            {
                { "c|text=", "Content Text", v => { Content = !string.IsNullOrEmpty(v) ? v : null; } },
                { "o|overwrite", "Overwrite Exists File", v => { OverWrite = true; } },
                { "w|out=", "Output File Name", v => { TargetName = !string.IsNullOrEmpty(v) ? v : null; } },
                { "p|pixels=", "Pixels Per=Module", v => { if (string.IsNullOrEmpty(v) || !int.TryParse(v, out _pixels_)) _pixels_ = 20; } },
                { "e|ecclevel=", "QRCode ECC Level (H, Q, M, L)", v => { if (string.IsNullOrEmpty(v) || !Enum.TryParse(v.ToUpper(), true, out _quality_)) _quality_ = QRCodeGenerator.ECCLevel.Q; } },
                { "type=", "Output File Type(art, bmp, pdf, png, ascii, base64, postscript, svg)", v => { if (string.IsNullOrEmpty(v) || !Enum.TryParse(v.ToUpper(), true, out _qrtype_)) _qrtype_ = QRTypes.Png; } },
                { "" },
            };
            AppendOptions(opts);
        }

        private byte[] GetBitmapBytes(System.Drawing.Bitmap bmp)
        {
            byte[] result = null;
            if (bmp is System.Drawing.Bitmap)
            {
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    ms.Seek(0, SeekOrigin.Begin);
                    result = ms.ToArray();
                }
            }
            return (result);
        }

        private void ShowQR(byte[] qr)
        {
            BitmapImage bmp;
            using (var ms = new MemoryStream(qr))
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
                    MaxWidth = 1080,
                    MaxHeight = 1080,
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
                    {
                        using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(Content, Quality))
                        {
                            dynamic qr = null;
                            if (QRType == QRTypes.Art)
                            {
                                using (var qrCode = new ArtQRCode(qrCodeData)) { qr = GetBitmapBytes(qrCode.GetGraphic(_pixels_)); }
                            }
                            else if (QRType == QRTypes.Bmp)
                            {
                                using (var qrCode = new BitmapByteQRCode(qrCodeData)) { qr = qrCode.GetGraphic(_pixels_); }
                            }
                            else if (QRType == QRTypes.Png)
                            {
                                using (var qrCode = new PngByteQRCode(qrCodeData)) { qr = qrCode.GetGraphic(_pixels_); }
                            }
                            else if (QRType == QRTypes.Pdf)
                            {
                                using (var qrCode = new PdfByteQRCode(qrCodeData)) { qr = qrCode.GetGraphic(_pixels_); }
                            }
                            else if (QRType == QRTypes.Ascii)
                            {
                                //using (var qrCode = new AsciiQRCode(qrCodeData)) { qr = qrCode.GetGraphic(Math.Max(1, _pixels_), darkColorString: "⬛", whiteSpaceString: "  "); }
                                using (var qrCode = new AsciiQRCode(qrCodeData)) { qr = qrCode.GetGraphic(Math.Max(1, _pixels_)); }
                            }
                            else if (QRType == QRTypes.Base64)
                            {
                                using (var qrCode = new Base64QRCode(qrCodeData)) { qr = qrCode.GetGraphic(_pixels_); }
                            }
                            else if (QRType == QRTypes.PostScript)
                            {
                                using (var qrCode = new PostscriptQRCode(qrCodeData)) { qr = qrCode.GetGraphic(_pixels_); }
                            }
                            else if (QRType == QRTypes.Svg)
                            {
                                using (var qrCode = new SvgQRCode(qrCodeData)) { qr = qrCode.GetGraphic(_pixels_); }
                            }
                            else
                            {
                                using (var qrCode = new QRCode(qrCodeData)) { qr = GetBitmapBytes(qrCode.GetGraphic(_pixels_)); }
                            }

                            if (string.IsNullOrEmpty(TargetName) && qr is byte[])
                            {
                                ShowQR(qr as byte[]);
                                status = true;
                            }
                            else
                            {
                                var invalid_path_chars = Path.GetInvalidPathChars();
                                var invalid_file_chars = Path.GetInvalidFileNameChars();
                                var target_path = Regex.Replace(Path.GetDirectoryName(TargetName), $@"[{string.Join("", invalid_path_chars)}]", "_", RegexOptions.IgnoreCase);
                                var target_file = Regex.Replace(Path.GetFileName(TargetName), $@"[{string.Join("", invalid_file_chars)}]", "_", RegexOptions.IgnoreCase);
                                target_file = Path.Combine(target_path, target_file);

                                if (qr is byte[] && (qr as byte[]).Length > 0)
                                {
                                    if (!string.IsNullOrEmpty(target_path) && !Directory.Exists(target_path)) Directory.CreateDirectory(target_path);
                                    if (!string.IsNullOrEmpty(target_file) && (OverWrite || !File.Exists(target_file))) File.WriteAllBytes(target_file, qr as byte[]);
                                    status = true;
                                }
                                else if(qr is string && !string.IsNullOrEmpty(qr))
                                {
                                    if (!string.IsNullOrEmpty(target_path) && !Directory.Exists(target_path)) Directory.CreateDirectory(target_path);
                                    if (!string.IsNullOrEmpty(target_file) && (OverWrite || !File.Exists(target_file))) File.WriteAllText(target_file, qr as string);
                                    status = true;
                                }
                            }
                            ret = true;
                            result = (T)(object)status;
                        }
                    }
                }
            }
            catch (Exception ex) { ShowMessage(ex, Name); }
            return (ret);
        }

    }
}
