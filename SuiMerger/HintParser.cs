using Nett;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiMerger
{
    class MergerConfiguration
    {
        public bool debug { get; set; }
        public string working_directory { get; set; }

        public string temp_folder { get; set; }
        public string output_folder { get; set; }
        public string input_folder { get; set; }

        //ps3 config
        public string ps3_xml_path { get; set; }
        public string ps3_merged_output_path { get; set; }

        //per each mg input config
        public List<InputInfo> input { get; set; }
    }

    class InputInfo
    {
        public string filename { get; set; }
        public List<List<int>> ps3_regions { get; set; } //array of 2 element regions defining sections of the ps3 to match against, IN ORDER 
    }

    class HintParser
    {
        public static MergerConfiguration ParseTOML(string tomlFilePath)
        {
            MergerConfiguration config = Toml.ReadFile<MergerConfiguration>(tomlFilePath);
            bool configOK = true;
            
            //validate the configuration
            foreach(InputInfo inputInfo in config.input)
            {
                foreach(List<int> region in inputInfo.ps3_regions)
                {
                    //check for a region which is not length 2
                    if(region.Count != 2)
                    {
                        Console.WriteLine($"Error parsing info for {inputInfo.filename}: Region should be length 2 but is length {region.Count}");
                        configOK = false;
                        continue;
                    }
                    
                    //check for a region where the start value is greater than the end value
                    if(region[0] > region[1])
                    {
                        Console.WriteLine($"Error parsing info for {inputInfo.filename}: Start is greater than end [{region[0]},{region[1]}]");
                        configOK = false;
                        continue;
                    }
                }
            }

            return configOK ? config : null;
        }
    }
}