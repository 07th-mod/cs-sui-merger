using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace SuiMerger
{
    class XMLMerger
    {
        static string xmlCountPattern = @".*?_(\d+?).xml";
        static Regex xmlCountRegex = new Regex(xmlCountPattern, RegexOptions.IgnoreCase);

        //merges files in alphabetical order
        public static void MergeFilesInFolder(string folder, string outputFilePath)
        {
            Dictionary<int, string> filePathDict = new Dictionary<int, string>();
            int numFiles = 0;

            string[] files = Directory.GetFiles(folder);
            foreach (string filename in files)
            {
                Match match = xmlCountRegex.Match(filename);
                if (!match.Success)
                {
                    throw new FormatException("XML filename does not adhere to regex pattern!");
                }

                int xmlCount = Convert.ToInt32(match.Groups[1].Value);
                Console.WriteLine($"{filename}: {xmlCount}");

                filePathDict.Add(xmlCount, Path.Combine(folder, filename));
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
    class PS3XMLReader
    {
        public static async Task TestReader(System.IO.Stream stream)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.Async = true;

            using (XmlReader reader = XmlReader.Create(stream, settings))
            {
                while (await reader.ReadAsync())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            Console.WriteLine("Attributes of <" + reader.Name + ">");
                            while (reader.MoveToNextAttribute())
                            {
                                Console.WriteLine(" {0}={1}", reader.Name, reader.Value);
                            }

                            break;
                        case XmlNodeType.Text:
                            Console.WriteLine("Text Node: {0}",
                                     await reader.GetValueAsync());
                            break;
                        case XmlNodeType.EndElement:
                            Console.WriteLine("End Element {0}", reader.Name);
                            break;
                        default:
                            Console.WriteLine("Other node {0} with value {1}",
                                            reader.NodeType, reader.Value);
                            break;
                    }
                }
            }
        }
    }


    class Program
    {
        static void Main(string[] args)
        {
            XMLMerger.MergeFilesInFolder(@"c:\temp\sui_try_merge", @"c:\temp\sui_xml_merged.xml");

            Console.ReadLine();

            return;
            string XMLFilePath = @"C:\temp\sui\sui_full.xml";

            FileStream fs = new FileStream(XMLFilePath, FileMode.Open);

            Task result = PS3XMLReader.TestReader(fs);

            Console.ReadLine();

            return;

            LineTrackerMG lt = new LineTrackerMG();
            string fileToParse = "manga_gamer_example.txt";
            
            using (StreamReader sr = new StreamReader(fileToParse))
            {
                String line;
                while ((line = sr.ReadLine()) != null)
                {
                    Console.WriteLine(line);
                    lt.AddLine(line);
                }
            }

        }
    }
}
