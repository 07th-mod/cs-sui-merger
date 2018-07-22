using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

//TODO: Consider stripping jpanaese punctuation (eg 「山盛り。」 -> 山盛り) | (this is done already): strip japanese names from ps3 script (where is this in they python code?)
//TODO: for a long sequence of +- which don't match in the diff, just associated them in order (eg line up the - and the + in order)
//TOOD: Within each block of non-matched lines, do a 'best fit' of all possible combinations (or some other way to match up the lines)
//      Need to take into account that more than one line may map to each line (usually more than one MangaGamer line maps to a PS3 line)...
//      Could split ps3 lines by voices/� marker, but a generic method could turn out better.

namespace SuiMerger
{
    class Chunker
    {
        string startOfChunkString = ">>>> DIFF START";
        IEnumerator<DialogueBase> dialogueIter;

        public Chunker(IEnumerator<DialogueBase> dialogueIter)
        {
            this.dialogueIter = dialogueIter;
            dialogueIter.MoveNext();
        }

        //TODO: verify this function actually outputs all the data, and doesn't skip any data...
        public List<string> GetCurrentChunk()
        {
            if (dialogueIter.Current == null)
                return new List<string>();

            List<string> mgCurrentChunk = new List<string>();
            
            while (dialogueIter.Current != null)
            {
                mgCurrentChunk.Add(">>>> " + startOfChunkString);
                DialogueBase currentDialogue = dialogueIter.Current;
                startOfChunkString = currentDialogue.data; //this sets the start of chunk line for the NEXT chunk
                mgCurrentChunk.AddRange(currentDialogue.previousLinesOrInstructions);
                dialogueIter.MoveNext();

                if (currentDialogue.otherDialogue != null)
                {
                    Console.WriteLine($"Associated ps3: {currentDialogue.otherDialogue.ID}");
                    break;
                }
            }

            return mgCurrentChunk;
        }
    }

    class Program
    {
        static void WriteSideBySide(FileStream fsA, FileStream fsB, List<string> listA, List<string> listB)
        {
            
            int maxLen = Math.Max(listA.Count, listB.Count);
            for(int i = 0; i < maxLen; i++)
            {
                string listAToWrite = "\n";
                string listBToWrite = "\n";

                if (i < listA.Count)
                {
                    listAToWrite = listA[i].TrimEnd() + "\n";
                }

                if (i < listB.Count)
                {
                    listBToWrite = listB[i].TrimEnd() + "\n";
                }
                //string left_string_padded = listAToWrite.Substring(0, Math.Min(80, listAToWrite.Length)).PadRight(80);

                //string right_string = listBToWrite;
                //string joined = left_string_padded + " | " + right_string + "\n";

                byte[] stringAsBytes = new UTF8Encoding(true).GetBytes(listAToWrite);
                fsA.Write(stringAsBytes, 0, stringAsBytes.Length);

                stringAsBytes = new UTF8Encoding(true).GetBytes(listBToWrite);
                fsB.Write(stringAsBytes, 0, stringAsBytes.Length);
            }

        }

        static void Main(string[] args)
        {
            //MUST set this so that diff tool can output proper unicode (otherwise output is scrambled)
            //and so can see japanese characters (you might need to change your console font too to MS Gothic or similar)
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            
            const string separate_xml_folder = @"c:\tempsui\sui_try_merge";
            const string untranslatedXMLFilePath = @"c:\tempsui\sui_xml_NOT_translated.xml";
            const string mangagamerScript = @"C:\tempsui\example_scripts\onik_001.txt";
            const string diff_temp_folder = @"C:\tempsui\temp_diff";
            const string debug_side_by_side_diff_A = @"c:\tempsui\debug_side_A.txt";
            const string debug_side_by_side_diff_B = @"c:\tempsui\debug_side_B.txt";

            //These booleans control how much data should be regenerated each iteration
            //Set all to false to regenerate the data
            //skip concatenating the separate xml files into one
            bool do_concat = true;

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

            //Now all the dialogue objects have associations, iterate through them again and builid a side by side diff for debugging

            IEnumerator<DialogueBase> mgIter = allMangaGamerDialogue.GetEnumerator();
            IEnumerator<DialogueBase> ps3Iter = PS3DialogueInstructions.GetEnumerator();

            //iterate through mangagamer dialogue until reaching a dialogue which has an association. 
            //Store it in an array
            using (FileStream fsA = File.Open(debug_side_by_side_diff_A, FileMode.Create))
            {
                using (FileStream fsB = File.Open(debug_side_by_side_diff_B, FileMode.Create))
                {
                    Chunker mgChunker = new Chunker(mgIter);
                    Chunker ps3Chunker = new Chunker(ps3Iter);

                    while(true)
                    {
                        List<string> mgChunk = mgChunker.GetCurrentChunk();
                        List<string> ps3Chunk = ps3Chunker.GetCurrentChunk();

                        if (mgChunk.Count == 0 && ps3Chunk.Count == 0)
                            break;

                        WriteSideBySide(fsA, fsB, mgChunk, ps3Chunk);
                    }
                }
            }

            //iterate through the ps3 dialogue until reaching a dialogue which has an association
            //Store it in an array

            //Pad the shorter array until both arrays are the same length
            //write the arrays to disk

            //if end of file reached, treat it the same as when an association is reached.



            Console.WriteLine("\n\nProgram Finished!");
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
