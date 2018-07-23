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
        DialogueBase startOfChunkString = new DialogueBase() { ID = -1, otherDialogue = null, data = ">>>> DIFF START" } ;
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
                string headerMarker = "NULL";
                if (startOfChunkString.otherDialogue != null)
                    headerMarker = startOfChunkString.otherDialogue.ID.ToString();

                mgCurrentChunk.Add($">>>> [{startOfChunkString.ID} -> {headerMarker}]: {startOfChunkString.data}");

                DialogueBase currentDialogue = dialogueIter.Current;
                startOfChunkString = currentDialogue; //this sets the start of chunk line for the NEXT chunk
                mgCurrentChunk.AddRange(currentDialogue.previousLinesOrInstructions);
                dialogueIter.MoveNext();

                if (currentDialogue.otherDialogue != null)
                {
                    Console.WriteLine($"{currentDialogue.ID} -> {currentDialogue.otherDialogue.ID}");
                    break;
                }
            }

            return mgCurrentChunk;
        }
    }

    class Program
    {
        static void WriteSideBySideTopPad(FileStream fsA, FileStream fsB, List<string> listA, List<string> listB)
        {
            int maxLen = Math.Max(listA.Count, listB.Count);
            int minLen = Math.Min(listA.Count, listB.Count);
            string padString = new String('\n', maxLen - minLen);

            if (listA.Count < maxLen) //list A is smaller
            {
                StringUtils.WriteString(fsA, padString);
            }

            if (listB.Count < maxLen)
            {
                StringUtils.WriteString(fsB, padString);
            }

            StringUtils.WriteStringList(fsA, listA, forceNewline:true);
            StringUtils.WriteStringList(fsB, listB, forceNewline:true);
        }

        //Returns a new list, filtered by the specified ranges
        static List<PS3DialogueInstruction> GetFilteredPS3Instructions(List<PS3DialogueInstruction> inputList, int regionStart, int regionEnd)
        {
            List<PS3DialogueInstruction> filteredList = new List<PS3DialogueInstruction>();
            if (regionStart == -1)
            {
                filteredList = inputList;
            }
            else
            {
                foreach (PS3DialogueInstruction ps3Dialogue in inputList)
                {
                    if (ps3Dialogue.ID >= regionStart && ps3Dialogue.ID <= regionEnd)
                    {
                        filteredList.Add(ps3Dialogue);
                    }
                }
            }
            return filteredList;
        }

        static void PrintSideBySideDiff(List<MangaGamerDialogue> allMangaGamerDialogue, List<PS3DialogueInstruction> PS3DialogueInstructions, string debug_path_MG, string debug_path_PS3)
        {
            //Now all the dialogue objects have associations, iterate through them again and builid a side by side diff for debugging

            IEnumerator<DialogueBase> mgIter = allMangaGamerDialogue.GetEnumerator();
            mgIter.MoveNext();
            //IEnumerator<DialogueBase> ps3Iter = PS3DialogueInstructions.GetEnumerator();

            //iterate through mangagamer dialogue until reaching a dialogue which has an association. 
            //Store it in an array
            using (FileStream fsMG = File.Open(debug_path_MG, FileMode.Create))
            {
                using (FileStream fsPS3 = File.Open(debug_path_PS3, FileMode.Create))
                {
                    //iterate through each dialogue in PS3 list
                    List<string> currentPS3ToSave = new List<string>();
                    List<string> currentMangaGamerToSave = new List<string>();
                    foreach (PS3DialogueInstruction ps3Instruction in PS3DialogueInstructions)
                    {
                        //add the previous lines (instructions)
                        currentPS3ToSave.AddRange(ps3Instruction.previousLinesOrInstructions);

                        //add the current PS3 line
                        StringBuilder associationsSB = new StringBuilder();
                        foreach (MangaGamerDialogue otherdialogue in ps3Instruction.GetOtherMangaGamerDialogues())
                        {
                            associationsSB.Append($"{otherdialogue.ID}, ");
                        }
                        currentPS3ToSave.Add($">>>> [{ps3Instruction.ID} -> {associationsSB.ToString()}]: {ps3Instruction.data}");

                        //if the dialogue association is empty, continue
                        List<MangaGamerDialogue> associatedMangaGamerDialogues = ps3Instruction.GetOtherMangaGamerDialogues();
                        if (associatedMangaGamerDialogues.Count == 0)
                        {
                            continue;
                        }

                        //Prepare a list of associated manga gamer line numbers
                        HashSet<int> mangaGamerLinesToGet = new HashSet<int>();
                        foreach (MangaGamerDialogue mgDialogue in associatedMangaGamerDialogues)
                        {
                            mangaGamerLinesToGet.Add(mgDialogue.ID);
                        }

                        //iterate through the list of mgDialogues until all IDs in the above list have been seen.
                        while (mgIter.Current != null)
                        {
                            //process the current object
                            if (mangaGamerLinesToGet.Contains(mgIter.Current.ID))
                            {
                                mangaGamerLinesToGet.Remove(mgIter.Current.ID);
                            }

                            currentMangaGamerToSave.AddRange(mgIter.Current.previousLinesOrInstructions);
                            currentMangaGamerToSave.Add($">>>> [{mgIter.Current.ID} -> {(mgIter.Current.otherDialogue == null ? "NULL" : mgIter.Current.otherDialogue.ID.ToString())}]: {mgIter.Current.data}");

                            mgIter.MoveNext();

                            //if the entire hashset is empty (that is, all the associated MG lines
                            //of the PS3 line have already been dumped, proceed to dump out lines
                            if (mangaGamerLinesToGet.Count == 0)
                            {
                                break;
                            }
                        }

                        //Finally, top-pad the file with enough spaces so they line up (printing could be its own function)
                        WriteSideBySideTopPad(fsMG, fsPS3, currentMangaGamerToSave, currentPS3ToSave);
                        currentPS3ToSave.Clear();
                        currentMangaGamerToSave.Clear();
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            //Tsumi 26 Start- 92565  End - 93391 / Tsumi 25 -Start -  91816  End - 92563
            //THESE RANGES ARE INCLUSIVE!
            int regionStart = 91816;
            int regionEnd = 92563;

            //MUST set this so that diff tool can output proper unicode (otherwise output is scrambled)
            //and so can see japanese characters (you might need to change your console font too to MS Gothic or similar)
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            
            const string separate_xml_folder = @"c:\tempsui\sui_try_merge";                     //input (if merging enabled)
            const string untranslatedXMLFilePath = @"c:\tempsui\sui_xml_NOT_translated.xml";    //output/input - ps3 xml as a single fiile
            //const string mangagamerScript = @"C:\tempsui\example_scripts\onik_001.txt";
            const string mangagamerScript = @"C:\tempsui\example_scripts\tsumi_025_3.txt";      //Input mangagamer script

            const string diff_temp_folder = @"C:\tempsui\temp_diff";                            //OUTPUT folder - must already exist (fix this later)

            const string debug_side_by_side_diff_path_MG = @"c:\tempsui\debug_side_MG.txt";          //Debug OUTPUT for side-by-side diff
            const string debug_side_by_side_diff_path_PS3 = @"c:\tempsui\debug_side_PS3.txt";        //Debug OUTPUT for side-by-side diff

            //These booleans control how much data should be regenerated each iteration
            //Set all to false to regenerate the data
            //skip concatenating the separate xml files into one
            bool do_concat = false;

            if (do_concat)
            { 
                FileConcatenator.MergeFilesInFolder(separate_xml_folder, untranslatedXMLFilePath);
            }
            
            //load all ps3 dialogue instructions from the XML file, then take only the user specified region
            List<PS3DialogueInstruction> pS3DialogueInstructionsPreFilter = PS3XMLReader.GetPS3DialoguesFromXML(untranslatedXMLFilePath);
            List<PS3DialogueInstruction> pS3DialogueInstructions = GetFilteredPS3Instructions(pS3DialogueInstructionsPreFilter, regionStart, regionEnd);

            //load all the mangagamer lines form the mangagamer file
            List<MangaGamerDialogue> allMangaGamerDialogue = MangaGamerScriptReader.GetDialogueLinesFromMangaGamerScript(mangagamerScript);

            //Diff the dialogue
            Differ.DoDiff(diff_temp_folder, allMangaGamerDialogue, pS3DialogueInstructions);

            //DEBUG: generate the side-by-side diff
            PrintSideBySideDiff(allMangaGamerDialogue, pS3DialogueInstructions, debug_side_by_side_diff_path_MG, debug_side_by_side_diff_path_PS3);

            Console.WriteLine("\n\nProgram Finished!");
            Console.ReadLine();

            return;
        }
    }
}
