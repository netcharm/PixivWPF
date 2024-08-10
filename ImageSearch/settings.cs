using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ImageSearch.Search;
using Newtonsoft.Json;

namespace ImageSearch
{
    public class Settings
    {
        public static Settings? Load(string setting_file)
        {
            Settings? result = new Settings();
            if (System.IO.File.Exists(setting_file))
            {
                try
                {
                    var text = System.IO.File.ReadAllText(setting_file);

                    result = JsonConvert.DeserializeObject<Settings>(text);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
            return (result);
        }

        public void Save(string setting_file)
        {
            try
            {
                var text = JsonConvert.SerializeObject(this, Formatting.Indented);
                System.IO.File.WriteAllText(setting_file, text);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        public bool AllFolder { get; set; } = true;
        
        public int ResultLimit { get; set; } = 10;

        public List<Storage> StorageList { get; set; } = [];
    }
}
