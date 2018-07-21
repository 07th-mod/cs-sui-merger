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
    class Program
    {
        static void Main(string[] args)
        {
            //MUST set this so that diff tool can output proper unicode (otherwise output is scrambled)
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            //set console output to UTF-8 so can see japanese characters
            /*
            Console.WriteLine("沙都子は、本当はまだ怖いくせにニヤ");
            Console.ReadLine();*/

            const string separate_xml_folder = @"c:\tempsui\sui_try_merge";
            const string untranslatedXMLFilePath = @"c:\tempsui\sui_xml_NOT_translated.xml";
            const string mangagamerScript = @"C:\tempsui\example_scripts\onik_001.txt";
            const string diff_temp_folder = @"C:\tempsui\temp_diff";

            //These booleans control how much data should be regenerated each iteration
            //Set all to false to regenerate the data
            //skip concatenating the separate xml files into one
            bool do_concat = false;


            if (do_concat)
            { 
                FileConcatenator.MergeFilesInFolder(separate_xml_folder, untranslatedXMLFilePath);
            }
            
            //load all ps3 dialogue instructions from the XML file
            List<PS3DialogueInstruction> PS3DialogueInstructions = PS3XMLReader.GetPS3DialoguesFromXML(untranslatedXMLFilePath);

            //load all the mangagamer lines form the mangagamer file
            List<MangaGamerDialogue> allMangaGamerDialogue = MangaGamerScriptReader.GetDialogueLinesFromMangaGamerScript(mangagamerScript);

            //Diff the dialogue
            Differ.DoDiff(diff_temp_folder, allMangaGamerDialogue, PS3DialogueInstructions);

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
