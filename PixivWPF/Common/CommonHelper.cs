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
    class CommonHelper
    {
    }

    [JsonObject(MemberSerialization.OptOut)]
    class Setting
    {
        private static string AppPath = Path.GetDirectoryName(Application.ResourceAssembly.CodeBase.ToString()).Replace("file:\\", "");
        private static string config = Path.Combine(AppPath, "config.json");
        private static Setting Cache = null;// Load(config);

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

        public bool Save(string configfile = "")
        {
            bool result = false;
            try
            {
                if (!string.IsNullOrEmpty(configfile)) config = configfile;
                var text = JsonConvert.SerializeObject(Cache, Formatting.Indented);
                File.WriteAllText(config, text, new UTF8Encoding(true));
                result = true;
            }
            catch (Exception) { }
            return (result);
        }

        public static Setting Load(string configfile="")
        {
            Setting result = new Setting();
            try
            {
                if (Cache is Setting) result = Cache;
                else
                {
                    if (!string.IsNullOrEmpty(configfile)) config = configfile;
                    if (File.Exists(config))
                    {
                        var text = File.ReadAllText(config);
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
    }
}
