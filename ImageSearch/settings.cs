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
using Newtonsoft.Json.Converters;

namespace ImageSearch
{
    public class Settings
    {
        [Newtonsoft.Json.JsonIgnore]
        internal protected string SettingFile { get; set; } = string.Empty;

        public static Settings? Load(string setting_file)
        {
            Settings? result = new ();
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
                if (System.IO.File.Exists(setting_file))
                    System.IO.File.Move(setting_file, $"{setting_file}.lastgood", true);

                var text = JsonConvert.SerializeObject(this, Formatting.Indented);
                System.IO.File.WriteAllText(setting_file, text);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        public bool DarkBackground { get; set; } = false;

        public string ModelFile { get; set; } = @"models\resnet50-v2-7.onnx";
        public string ModelInput { get; set; } = @"data";
        public string ModelOutput { get; set; } = @"resnetv24_dense0_fwd";

        public string ImageViewerCmd { get; set; } = string.Empty;
        public string ImageViewerOpt { get; set; } = string.Empty;

        public string ImageInfoViewerCmd { get; set; } = string.Empty;
        public string ImageInfoViewerOpt { get; set; } = string.Empty;

        public string ImageCompareCmd { get; set; } = string.Empty;
        public string ImageCompareOpt { get; set; } = string.Empty;

        [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
        public BatchRunningMode ParallelMode { get; set; } = BatchRunningMode.ForLoop;
        public int ParallelLimit { get; set; } = 5;
        public int ParallelTimeOut { get; set; } = 5;
        public ulong ParallelCheckPoint { get; set; } = 1000;

        public int LogLines { get; set; } = 500;

        public bool AllFolder { get; set; } = true;

        public int ResultLimitMax { get; set; } = 1000;
        public double[] ResultLimitList = [5, 10, 12, 15, 18, 20, 24, 25, 30, 35, 40, 45, 50, 60, 80, 120, 0.95, 0.9, 0.85, 0.8, 0.75, 0.7, 0.65, 0.6, 0.55, 0.5, 0.45, 0.4];
        public double ResultLimit { get; set; } = 10;
        public double ResultConfidence { get; set; } = 0.25;

        public string LastImageFolder { get; set; } = string.Empty;
        public List<Storage> StorageList { get; set; } = [];
    }
}
