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
    class PS3DialogueInstruction
    {
        int num;
        int dlgtype;
        string data; //raw string data from the translated XML file

        public PS3DialogueInstruction(int num, int dlgtype, string data, bool autoTranslate=true)
        {
            this.num = num;
            this.dlgtype = dlgtype;
            this.data = autoTranslate ? FileTranslator.TranslateString(data) : data;
        }
    }    

    class Program
    {
        static void Main(string[] args)
        {
            const string separate_xml_folder = @"c:\tempsui\sui_try_merge";
            const string untranslatedXMLFilePath = @"c:\tempsui\sui_xml_NOT_translated.xml";

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
