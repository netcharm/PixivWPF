﻿using System;
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

        public bool AllFolder { get; set; } = true;

        public int ResultLimit { get; set; } = 10;

        public string LastImageFolder { get; set; } = string.Empty;
        public List<Storage> StorageList { get; set; } = [];
    }
}