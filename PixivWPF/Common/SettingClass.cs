using MahApps.Metro.Controls;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PixivWPF.Common
{
    [JsonObject(MemberSerialization.OptOut)]
    class Setting
    {
        private static string AppPath = Path.GetDirectoryName(Application.ResourceAssembly.CodeBase.ToString()).Replace("file:\\", "");
        private static string config = Path.Combine(AppPath, "config.json");
        private static Setting Cache = null;// Load(config);

        [JsonIgnore]
        private string username = string.Empty;
        [JsonIgnore]
        public string User
        {
            get
            {
                return (username);
            }
            set
            {
                username = value;
            }
        }

        [JsonIgnore]
        private string password = string.Empty;
        [JsonIgnore]
        public string Pass
        {
            get
            {
                return (password);
            }
            set
            {
                password = value;
            }
        }

        [JsonIgnore]
        private Pixeez.Objects.User myinfo = null;
        [JsonIgnore]
        public Pixeez.Objects.User MyInfo
        {
            get { return myinfo; }
            set { myinfo = value; }
        }

        private long update = 0;
        public long Update
        {
            get { return update; }
            set { update = value; }
        }

        [JsonIgnore]
        private string lastfolder = string.Empty;
        [JsonIgnore]
        public string LastFolder
        {
            get { return lastfolder; }
            set { lastfolder = value; }
        }

        [JsonIgnore]
        public string APPPATH
        {
            get { return AppPath; }
        }

        private string accesstoken = string.Empty;
        public string AccessToken
        {
            get
            {
                return (accesstoken);
            }
            set
            {
                accesstoken = value;
            }
        }

        private string refreshtoken = string.Empty;
        public string RefreshToken
        {
            get
            {
                return (refreshtoken);
            }
            set
            {
                refreshtoken = value;
            }
        }


        private string proxy = string.Empty;
        public string Proxy
        {
            get
            {
                return (proxy);
            }
            set
            {
                proxy = value;
            }
        }

        private bool useproxy = false;
        public bool UsingProxy
        {
            get
            {
                return (useproxy);
            }
            set
            {
                useproxy = value;
            }
        }

        private string theme = string.Empty;
        public string Theme { get; set; }

        private string accent = string.Empty;
        public string Accent { get; set; }

        private string lastSaveFolder = string.Empty;
        [JsonIgnore]
        public string SaveFolder { get; set; }

        public void Save(string configfile = "")
        {
            try
            {
                if (string.IsNullOrEmpty(configfile)) configfile = config;
                var text = JsonConvert.SerializeObject(Cache, Formatting.Indented);
                File.WriteAllText(configfile, text, new UTF8Encoding(true));
            }
            catch (Exception ex)
            {
                CommonHelper.ShowMessageDialog("ERROR", ex.Message);
            }
        }

        public static Setting Load(string configfile = "")
        {
            Setting result = new Setting();
            try
            {
                if (Cache is Setting) result = Cache;
                else
                {
                    if (string.IsNullOrEmpty(configfile)) configfile = config;
                    if (File.Exists(config))
                    {
                        var text = File.ReadAllText(configfile);
                        if (text.Length < 20)
                            Cache = new Setting();
                        else
                            Cache = JsonConvert.DeserializeObject<Setting>(text);
                        result = Cache;
                    }
                }
            }
            catch (Exception) { }
            return (result);
        }

        public static string Token()
        {
            string result = null;
            if (Cache is Setting) result = Cache.AccessToken;
            return (result);
        }

        public static string ProxyServer()
        {
            string result = null;
            if (Cache is Setting) result = Cache.Proxy;
            return (result);
        }

        public static bool UseProxy()
        {
            bool result = false;
            if (Cache is Setting) result = Cache.UsingProxy;
            return (result);
        }

        public static bool Token(string token)
        {
            bool result = false;
            if (Cache is Setting)
            {
                Cache.AccessToken = token;
                result = true;
            }
            return (result);
        }

        public static string GetDeviceId()
        {
            string location = @"SOFTWARE\Microsoft\Cryptography";
            string name = "MachineGuid";

            using (RegistryKey localMachineX64View = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                using (RegistryKey rk = localMachineX64View.OpenSubKey(location))
                {
                    if (rk == null)
                        throw new KeyNotFoundException(string.Format("Key Not Found: {0}", location));

                    object machineGuid = rk.GetValue(name);
                    if (machineGuid == null)
                        throw new IndexOutOfRangeException(string.Format("Index Not Found: {0}", name));

                    return machineGuid.ToString().Replace("-", "");
                }
            }
        }
    }

}
