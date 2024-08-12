using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
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

        public string Model { get; set; } = @"models\resnet50-v2-7.onnx";
        public string ModelInput { get; set; } = @"data";
        public string ModelOutput { get; set; } = @"resnetv24_dense0_fwd";

        public bool AllFolder { get; set; } = true;

        public int ResultLimit { get; set; } = 10;

        public string LastImageFolder { get; set; } = string.Empty;
        public List<Storage> StorageList { get; set; } = [];
    }
}
