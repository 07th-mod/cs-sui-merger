using Nett;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Example TOML file format
// ----------------------------------------------------------------------
//  # enable debug output
//  debug = true
//  
//  # temporary folder to put diffs of each file
//  temp_folder = "diff_temp"
//  
//  # if xml_path is a folder, will merge all the xml files in the folder
//  # into one file (will use the last part of filename to merge)
//  # eg *_0.xml, *_1.xml ... *_42.xml will be merged in that order
//  # if xml_path is a file, will directly use the specified xml file
//  xml_path = "test.xml"
//  
//  [[input]]
//  path = "script1.txt"
//  ps3_regions = [[1,2],[4,5]]
//  
//  
//  [[input]]
//  path = "script2.txt"
//  ps3_regions = [[10, 100],[30,40]]


namespace SuiMerger
{
    class MergerConfiguration
    {
        public string temp_folder { get; set; }
        public bool debug { get; set; }
        public List<InputInfo> input { get; set; }
        public string xml_path { get; set; }
    }

    class InputInfo
    {
        public string path { get; set; }
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