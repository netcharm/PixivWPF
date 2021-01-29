//css_args /co:/win32icon:./aes.ico
//css_co /win32icon:./aes.ico

////css_reference PresentationFramework.dll

using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Collections.ObjectModel;
//using System.ComponentModel;
//using System.Globalization;
using System.IO;
//using System.IO.Pipes;
//using System.Linq;
using System.Management;
//using System.Media;
//using System.Net;
//using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
//using System.Security.Permissions;
using System.Text;
//using System.Text.RegularExpressions;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Windows;

[assembly: AssemblyTitle("AES Encrypt/Decrypt Test Tool")]
//[assembly: AssemblyDescription("AES Encrypt/Decrypt Test Tool")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("NetCharm")]
[assembly: AssemblyProduct("AES Encrypt/Decrypt Test Tool")]
[assembly: AssemblyCopyright("Copyright NetCharm © 2020")]
[assembly: AssemblyTrademark("NetCharm")]
[assembly: AssemblyCulture("")]

[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]

namespace netcharm
{
    static class MyScript
    {
        //public static void ShowMessageBox(this string content, string title, MessageBoxImage image = MessageBoxImage.Information)
        //{
        //    ShowMessageDialog(content, title, image);
        //}

        //public static async void ShowMessageDialog(this string content, string title, MessageBoxImage image= MessageBoxImage.Information)
        //{
        //    await Task.Delay(1);
        //    MessageBox.Show(content, title, MessageBoxButton.OK, image);
        //}	

        public static void LOG(this string content)
        {
            try
            {
                Console.WriteLine(content);
            }
            catch(Exception) {}            
        }
                
        public static void ERR(this string content, string title="")
        {
            try
            {
                if(string.IsNullOrEmpty(title)) title = "ERROR";
                Console.Error.WriteLine($"{title}:{content}");
            }
            catch(Exception) {}            
        }
        
        public static string ProcessorID { get; set; } = string.Empty;
        public static string GetProcessorID()
        {
            string result = string.Empty;

            ManagementObjectSearcher mos = new ManagementObjectSearcher("select * from Win32_Processor");
            foreach (ManagementObject mo in mos.Get())
            {
                try
                {
                    result = mo["ProcessorId"].ToString();
                    break;
                }
                catch (Exception ex) { ex.ERROR("PROCESSOR"); continue; }

                //foreach (PropertyData p in mo.Properties)
                //{
                //    if(p.Name.Equals("ProcessorId", StringComparison.CurrentCultureIgnoreCase))
                //    {
                //        result = p.Value.ToString();
                //        break;
                //    }
                //}
                //if (string.IsNullOrEmpty(result)) break;
            }

            return (result);
        }

        #region AES Encrypt/Decrypt helper
        public static string AesEncrypt(this string text, string skey, bool auto = true)
        {
            string encrypt = string.Empty;
            try
            {
                if (!string.IsNullOrEmpty(skey) && !string.IsNullOrEmpty(text))
                {
                    var uni_skey = $"{ProcessorID}{skey}";
                    var uni_text = $"{ProcessorID}{text}";

                    AesCryptoServiceProvider aes = new AesCryptoServiceProvider();
                    MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
                    SHA256CryptoServiceProvider sha256 = new SHA256CryptoServiceProvider();
                    aes.Key = sha256.ComputeHash(Encoding.UTF8.GetBytes(uni_skey));
                    aes.IV = md5.ComputeHash(Encoding.UTF8.GetBytes(uni_skey));

                    byte[] dataByteArray = Encoding.UTF8.GetBytes(uni_text);
                    if (auto)
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(aes.Key, aes.IV), CryptoStreamMode.Write))
                            {
                                using (StreamWriter sw = new StreamWriter(cs))
                                {
                                    sw.Write(uni_text);
                                }
                                encrypt = Convert.ToBase64String(ms.ToArray());
                            }
                        }
                    }
                    else
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(aes.Key, aes.IV), CryptoStreamMode.Write))
                            {
                                cs.Write(dataByteArray, 0, dataByteArray.Length);
                                cs.FlushFinalBlock();
                            }
                            encrypt = Convert.ToBase64String(ms.ToArray());
                        }
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("AES"); }
            return encrypt;
        }

        public static string AesDecrypt(this string text, string skey, bool auto = true)
        {
            string decrypt = string.Empty;
            try
            {
                if (!string.IsNullOrEmpty(skey) && !string.IsNullOrEmpty(text))
                {
                    var uni_skey = $"{ProcessorID}{skey}";
                    var uni_text = string.Empty;

                    AesCryptoServiceProvider aes = new AesCryptoServiceProvider();
                    MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
                    SHA256CryptoServiceProvider sha256 = new SHA256CryptoServiceProvider();
                    aes.Key = sha256.ComputeHash(Encoding.UTF8.GetBytes(uni_skey));
                    aes.IV = md5.ComputeHash(Encoding.UTF8.GetBytes(uni_skey));

                    byte[] dataByteArray = Convert.FromBase64String(text);
                    if (auto)
                    {
                        using (MemoryStream ms = new MemoryStream(dataByteArray))
                        {
                            using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(aes.Key, aes.IV), CryptoStreamMode.Read))
                            {
                                using (StreamReader sr = new StreamReader(cs))
                                {
                                    uni_text = sr.ReadToEnd();
                                }
                            }
                        }
                    }
                    else
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(aes.Key, aes.IV), CryptoStreamMode.Write))
                            {
                                cs.Write(dataByteArray, 0, dataByteArray.Length);
                                cs.FlushFinalBlock();
                            }
                            uni_text = Encoding.UTF8.GetString(ms.ToArray());
                        }
                    }
                    if (uni_text.StartsWith(ProcessorID)) decrypt = uni_text.Replace($"{ProcessorID}", "");
                }
            }
            catch (Exception ex) { ex.ERROR("AES"); }
            return decrypt;
        }
        #endregion
          
        public static void Main(string[] args)
        {
            var title = Console.Title;
            //LOG(args.Length);
            if (args.Length < 3) return;
            
            ProcessorID = GetProcessorID();
            
            var cmd = args[0].ToLower();
            if(cmd.Equals("-e")) 
            { 
                var u = args[2].AesEncrypt(args[1], false);
                LOG($"PID: {ProcessorID}, KEY: {args[1]}, TEXT: {args[2]}");
                LOG($"AES: {u}");
            }
            else if(cmd.Equals("-ea")) 
            { 
                var u = args[2].AesEncrypt(args[1], true);
                LOG($"PID: {ProcessorID}, KEY: {args[1]}, TEXT: {args[2]}");
                LOG($"AES: {u}");
            }
            else if(cmd.Equals("-d"))
            {
                var u = args[2].AesDecrypt(args[1], false);
                LOG($"PID: {ProcessorID}, KEY: {args[1]}, AES: {args[2]}");
                LOG($"Text: {u}");			
            }
            else if(cmd.Equals("-da"))
            {
                var u = args[2].AesDecrypt(args[1], true);
                LOG($"PID: {ProcessorID}, KEY: {args[1]}, AES: {args[2]}");
                LOG($"Text: {u}");			
            }
            
            Console.Title = title;
        }
    }
}  