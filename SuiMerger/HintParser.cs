using Nett;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiMerger
{
    /// <summary>
    /// This class is used to deserialize .toml file using the 'Nett' library.
    /// See the 'conf.toml' example file for more documentation on each variable.
    /// </summary>
    class MergerConfiguration
    {
        public bool debug { get; set; }
        public string working_directory { get; set; }

        public string temp_folder { get; set; }
        public string output_folder { get; set; }

        public string pre_input_folder { get; set; }
        public string input_folder { get; set; }

        //ps3 config
        public string ps3_xml_path { get; set; }
        public string ps3_merged_output_path { get; set; }

        public string guessed_matches { get; set; }

        public bool trim_after_diff { get; set; }

        /// <summary>
        /// per each mg input config
        /// </summary>
        public List<InputInfo> input { get; set; }

        /// <summary>
        /// list of folders to search for BGM (used for inserting PS3 BGM)
        /// </summary>
        public List<String> bgm_folders { get; set; }
        public double music_threshold_seconds { get; set; }
    }

    class InputInfo
    {
        public string path { get; set; }
        /// <summary>
        /// //array of 2 element regions defining sections of the ps3 to match against, IN ORDER
        /// </summary>
        public List<List<int>> ps3_regions { get; set; }
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
                        Console.WriteLine($"Error parsing info for {inputInfo.path}: Region should be length 2 but is length {region.Count}");
                        configOK = false;
                        continue;
                    }
                    
                    //check for a region where the start value is greater than the end value
                    if(region[0] > region[1])
                    {
                        Console.WriteLine($"Error parsing info for {inputInfo.path}: Start is greater than end [{region[0]},{region[1]}]");
                        configOK = false;
                        continue;
                    }
                }
            }

            return configOK ? config : null;
        }
    }
}