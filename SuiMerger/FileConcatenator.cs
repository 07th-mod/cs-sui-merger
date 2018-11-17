using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SuiMerger
{
    class FileConcatenator
    {
        static Regex fileNumberingRegex = new Regex(@".*?_(\d+?).xml", RegexOptions.IgnoreCase);

        /// <summary>
        /// This function merges all files in a folder `in order` (like using cat)
        /// It assumes the files are of the form file1,file2,file3,...file9, file10, file11 etc.
        /// It is OK if there is no `0` padding (will work even if files are not numbered as file0001 etc.)
        /// </summary>
        /// <param name="folder"></param>
        /// <param name="outputFilePath"></param>
        public static void MergeFilesInFolder(string folder, string outputFilePath)
        {
            Dictionary<int, string> filePathDict = new Dictionary<int, string>();
            int numFiles = 0;

            string[] files = Directory.GetFiles(folder);
            foreach (string filename in files)
            {
                Match match = fileNumberingRegex.Match(filename);
                if (!match.Success)
                {
                    throw new FormatException("XML filename does not adhere to regex pattern!");
                }

                int fileNumber = Convert.ToInt32(match.Groups[1].Value);
                Console.WriteLine($"{filename}: {fileNumber}");

                filePathDict.Add(fileNumber, Path.Combine(folder, filename));
                numFiles += 1;
            }

            using (FileStream outputFile = File.Open(outputFilePath, FileMode.Create))
            {
                long bytesRead = 0;
                for (int i = 0; i < numFiles; i++)
                {
                    string filePath = filePathDict[i];
                    Console.WriteLine($"Writing {filePath}");
                    byte[] fileAsBytes = File.ReadAllBytes(filePath);
                    outputFile.Write(fileAsBytes, 0, fileAsBytes.Length);

                    //save total bytes read for debugging
                    bytesRead += fileAsBytes.Length;
                }

                Console.WriteLine($"Read {bytesRead} bytes, Wrote {outputFile.Position} bytes");
            }
        }
    }
}
