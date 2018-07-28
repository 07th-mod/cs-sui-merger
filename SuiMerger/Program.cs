﻿using System;
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
//TODO: The last chunk of ps3 instructions after the last dialogue in the ps3 file is never parsed

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
        static void WriteSideBySideTopPad(StreamWriter swA, StreamWriter swB, List<string> listA, List<string> listB)
        {
            int maxLen = Math.Max(listA.Count, listB.Count);
            int minLen = Math.Min(listA.Count, listB.Count);
            string padString = new String(Config.newline, maxLen - minLen);

            if (listA.Count < maxLen) //list A is smaller
            {
                swA.Write(padString);
            }

            if (listB.Count < maxLen)
            {
                swB.Write(padString);
            }

            StringUtils.WriteStringList(swA, listA);
            StringUtils.WriteStringList(swB, listB);
        }

        //Returns a new list, filtered by the specified ranges
        static List<PS3DialogueInstruction> GetFilteredPS3Instructions(List<PS3DialogueInstruction> inputList, List<List<int>> regions)
        {
            //assume that the whole list is to be scanned, if not specified.
            if(regions.Count == 0)
            {
                return new List<PS3DialogueInstruction>(inputList);
            }

            List<PS3DialogueInstruction> filteredList = new List<PS3DialogueInstruction>();

            //add regions in the order they appear passed in by the user
            foreach (List<int> region in regions)
            {
                foreach (PS3DialogueInstruction ps3Dialogue in inputList)
                {
                    if (ps3Dialogue.ID >= region[0] && ps3Dialogue.ID <= region[1])
                    {
                        filteredList.Add(ps3Dialogue);
                    }
                }
            }

            return filteredList;
        }

        static void PrintSideBySideDiff(List<AlignmentPoint> alignmentPoints, string debug_path_MG, string debug_path_PS3)
        {
            using (StreamWriter swMG = FileUtils.CreateDirectoriesAndOpen(debug_path_MG, FileMode.Create))
            {
                using (StreamWriter swPS3 = FileUtils.CreateDirectoriesAndOpen(debug_path_PS3, FileMode.Create))
                {
                    //iterate through each dialogue in PS3 list
                    List<string> currentPS3ToSave = new List<string>();
                    List<string> currentMangaGamerToSave = new List<string>();
                    foreach (AlignmentPoint alignmentPoint in alignmentPoints)
                    {
                        //add the previous lines (instructions

                        if(alignmentPoint.ps3DialogFragment != null)
                        {
                            PS3DialogueFragment ps3 = alignmentPoint.ps3DialogFragment;
                            currentPS3ToSave.AddRange(ps3.previousLinesOrInstructions);
                            currentPS3ToSave.Add($">>>> [{ps3.ID}.{ps3.fragmentID} -> {(ps3.otherDialogue == null ? "NULL" : ps3.otherDialogue.ID.ToString())}]: {ps3.data}");
                        }

                        if(alignmentPoint.mangaGamerDialogue != null)
                        {
                            MangaGamerDialogue mg = alignmentPoint.mangaGamerDialogue;
                            currentMangaGamerToSave.AddRange(mg.previousLinesOrInstructions);
                            currentMangaGamerToSave.Add($">>>> [{mg.ID} -> {(mg.otherDialogue == null ? "NULL" : mg.otherDialogue.ID.ToString())}]: {mg.data}");
                        }

                        if (alignmentPoint.ps3DialogFragment != null && alignmentPoint.mangaGamerDialogue != null)
                        {
                            //Finally, top-pad the file with enough spaces so they line up (printing could be its own function)
                            WriteSideBySideTopPad(swMG, swPS3, currentMangaGamerToSave, currentPS3ToSave);
                            currentPS3ToSave.Clear();
                            currentMangaGamerToSave.Clear();
                        }                    
                    }
                    
                    WriteSideBySideTopPad(swMG, swPS3, currentMangaGamerToSave, currentPS3ToSave);
                    currentPS3ToSave.Clear();
                    currentMangaGamerToSave.Clear();
                }
            }
        }

        static void SaveMergedMGScript(List<AlignmentPoint> alignmentPoints, string outputPath, List<string> mgLeftovers)
        {
            using (StreamWriter swOut = FileUtils.CreateDirectoriesAndOpen(outputPath, FileMode.Create))
            {
                //iterate through each dialogue in PS3 list
                List<string> currentPS3ToSave = new List<string>();
                List<string> currentMangaGamerToSave = new List<string>();

                foreach (AlignmentPoint alignmentPoint in alignmentPoints)
                {
                    if (alignmentPoint.ps3DialogFragment != null)
                    {
                        PS3DialogueFragment ps3 = alignmentPoint.ps3DialogFragment;
                        currentPS3ToSave.AddRange(ps3.previousLinesOrInstructions);
                        if(ps3.fragmentID == 0)
                        {
                            currentPS3ToSave.Add(ps3.parent.translatedRawXML);
                        }
                        //add a comment detailing the fragment information. NOTE: double hypen (--) is not allowed in XML comments
                        currentPS3ToSave.Add($"<!-- [{ps3.ID}.{ps3.fragmentID} > {(ps3.otherDialogue == null ? "NULL" : ps3.otherDialogue.ID.ToString())}]: {ps3.data} -->");
                    }

                    if (alignmentPoint.mangaGamerDialogue != null)
                    {
                        MangaGamerDialogue mg = alignmentPoint.mangaGamerDialogue;
                        currentMangaGamerToSave.AddRange(mg.previousLinesOrInstructions);
                        currentMangaGamerToSave.Add(mg.data);
                    }

                    if (alignmentPoint.ps3DialogFragment != null && alignmentPoint.mangaGamerDialogue != null)
                    {
                        WriteAssociatedPS3StringChunksFormatted(swOut, currentMangaGamerToSave, currentPS3ToSave);

                        currentPS3ToSave.Clear();
                        currentMangaGamerToSave.Clear();
                    }
                }

                //write out any leftover ps3 lines
                WriteAssociatedPS3StringChunksFormatted(swOut, currentMangaGamerToSave, currentPS3ToSave);

                //write out any leftover manga gamer lines
                StringUtils.WriteStringList(swOut, mgLeftovers);
            }
        }

        static void WriteAssociatedPS3StringChunksFormatted(StreamWriter swOut, List<string> currentMangaGamerToSave, List<string> currentPS3ToSave)
        {
            StringUtils.WriteStringListRegion(swOut, currentMangaGamerToSave, 0, currentMangaGamerToSave.Count - 1);

            swOut.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            swOut.WriteLine("<PS3_SECTION>  <!-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~START~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ -->");
            StringUtils.WriteStringList(swOut, currentPS3ToSave);
            swOut.WriteLine("</PS3_SECTION> <!-- ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~END~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ -->");

            StringUtils.WriteStringListRegion(swOut, currentMangaGamerToSave, currentMangaGamerToSave.Count - 1, currentMangaGamerToSave.Count);
        }

        static void SanityCheckAlignmentPoints(List<AlignmentPoint> alignmentPoints, List<MangaGamerDialogue> allMangaGamerDialogue, List<PS3DialogueFragment> fragments)
        {
            //check all manga gamer ids present
            int mgIndex = 0;
            int ps3Index = 0;
            foreach (AlignmentPoint ap in alignmentPoints)
            {
                if (ap.mangaGamerDialogue != null)
                {
                    if (ap.mangaGamerDialogue.ID != allMangaGamerDialogue[mgIndex].ID)
                    {
                        throw new Exception("wrong manga gamer dialogue found!");
                    }
                    mgIndex += 1;
                }

                if (ap.ps3DialogFragment != null)
                {
                    if (ap.ps3DialogFragment.ID != fragments[ps3Index].ID)
                    {
                        throw new Exception("wrong ps3 dialogue found!");
                    }
                    ps3Index += 1;
                }
            }

            if (mgIndex != allMangaGamerDialogue.Count)
            {
                throw new Exception("Some manga gamer dialogue were not present in the alignment points!");
            }

            if (ps3Index != fragments.Count)
            {
                throw new Exception("Some PS3 dialogue were not present in the alignment points!");
            }
        }

        static void PauseThenErrorExit()
        {
            Console.ReadLine();
            Environment.Exit(-1);
        }

        static List<AlignmentPoint> TrimAlignmentPoints(List<AlignmentPoint> alignmentPoints)
        {
            //assume entire match region (incase triming can't be done found)
            int firstMatch = 0;
            int lastMatch = alignmentPoints.Count-1;
            
            for(int i = 0; i < alignmentPoints.Count; i++)
            {
                AlignmentPoint ap = alignmentPoints[i];
                if (ap.mangaGamerDialogue != null)
                {
                    firstMatch = i;
                    break;
                }
            }

            for(int i = alignmentPoints.Count - 1; i > 0; i--)
            {
                AlignmentPoint ap = alignmentPoints[i];
                if (ap.mangaGamerDialogue != null)
                {
                    lastMatch = i;
                    break;
                }
            }

            List<AlignmentPoint> trimmedAlignmentPoints = new List<AlignmentPoint>();
            for(int i = firstMatch; i <= lastMatch; i++)
            {
                trimmedAlignmentPoints.Add(alignmentPoints[i]);
            }

            return trimmedAlignmentPoints;
        }

        static void ProcessSingleFile(List<PS3DialogueInstruction> pS3DialogueInstructionsPreFilter, MergerConfiguration config, InputInfo mgInfo, List<InputInfo> guessedInputInfos)
        {
            string fullPath = Path.Combine(config.input_folder, mgInfo.path);
            string pathNoExt = Path.GetFileNameWithoutExtension(fullPath);

            string debug_side_by_side_diff_path_MG  = Path.Combine(config.temp_folder, pathNoExt + "_debug_side_MG.txt");
            string debug_side_by_side_diff_path_PS3 = Path.Combine(config.temp_folder, pathNoExt + "_debug_side_PS3.txt");

            List<PS3DialogueInstruction> pS3DialogueInstructions = GetFilteredPS3Instructions(pS3DialogueInstructionsPreFilter, mgInfo.ps3_regions);           

            //load all the mangagamer lines form the mangagamer file
            List<MangaGamerDialogue> allMangaGamerDialogue = MangaGamerScriptReader.GetDialogueLinesFromMangaGamerScript(fullPath, out List<string> mg_leftovers);

            //Diff the dialogue
            List<AlignmentPoint> allAlignmentPoints = Differ.DoDiff(config.temp_folder, allMangaGamerDialogue, pS3DialogueInstructions, out List<PS3DialogueFragment> fragments, debugFilenamePrefix: pathNoExt);

            //Sanity check the alignment points by making sure there aren't missing any values
            SanityCheckAlignmentPoints(allAlignmentPoints, allMangaGamerDialogue, fragments);

            //trim alignment points to reduce output
            List<AlignmentPoint> alignmentPoints = config.trim_after_diff ? TrimAlignmentPoints(allAlignmentPoints) : allAlignmentPoints;

            //DEBUG: generate the side-by-side diff
            PrintSideBySideDiff(alignmentPoints, debug_side_by_side_diff_path_MG, debug_side_by_side_diff_path_PS3);

            //Insert PS3 instructions
            string mergedOutputPath = Path.Combine(config.output_folder, pathNoExt + "_merged.txt");
            SaveMergedMGScript(alignmentPoints, mergedOutputPath, mg_leftovers);

            //Use the inserted instructions
            UseInformation.InsertMGLinesUsingPS3XML(mergedOutputPath, Path.Combine(config.output_folder, pathNoExt + "_bgm.txt"));

            //Printout guessed ps3 region if region not specified in config file
            if(mgInfo.ps3_regions.Count == 0)
            {
                int firstMatchID = -1;
                int lastMatchID = -1;

                PS3DialogueInstruction ps3Parent = null;

                Console.WriteLine("\n[  HINT  ]: Printing first 5 matching PS3 lines");
                int numFound = 0;
                foreach (AlignmentPoint ap in alignmentPoints)
                {
                    if(ap.IsMatch())
                    {
                        if(ap.ps3DialogFragment.parent != ps3Parent)
                        {
                            ps3Parent = ap.ps3DialogFragment.parent;
                            //record the match so it can be saved to an output file
                            if (firstMatchID == -1)
                            {
                                firstMatchID = ps3Parent.ID;
                            }

                            Console.WriteLine($"\tStart {numFound}: {ps3Parent.ID} - {ps3Parent.translatedRawXML}");
                            numFound += 1;

                            if (numFound > 5)
                                break;
                        }
                    }
                }

                Console.WriteLine("\n[  HINT  ]: Printing last 5 matching PS3 lines");
                numFound = 0;
                for (int i = alignmentPoints.Count-1; i > 0; i--)
                {
                    AlignmentPoint ap = alignmentPoints[i];
                    if (ap.IsMatch())
                    {
                        if (ap.ps3DialogFragment.parent != ps3Parent)
                        {
                            ps3Parent = ap.ps3DialogFragment.parent;
                            //record the match so it can be saved to an output file
                            if (lastMatchID == -1)
                            {
                                lastMatchID = ps3Parent.ID;
                            }

                            Console.WriteLine($"\tEnd {numFound}: {ps3Parent.ID} - {ps3Parent.translatedRawXML}");
                            numFound += 1;

                            if (numFound > 5)
                                break;
                        }
                    }
                }

                
                List<List<int>> regions;
                //check for invalid match region
                if (firstMatchID == -1 || lastMatchID == -1)
                {
                    Console.WriteLine($"[  Warn  ]: Can't find match region for {mgInfo.path}");
                    regions = new List<List<int>>();
                }
                else
                {
                    regions = new List<List<int>>
                    {
                        new List<int> {firstMatchID, lastMatchID}
                    };
                }

                guessedInputInfos.Add(new InputInfo
                {
                    path = mgInfo.path,
                    ps3_regions = regions,
                });
            }
        }

        static int Main(string[] args)
        {
            Console.WriteLine("If japanese characters show as ???? please change your console font to MS Gothic or similar.");

            //MUST set this so that diff tool can output proper unicode (otherwise output is scrambled)
            //and so can see japanese characters (you might need to change your console font too to MS Gothic or similar)
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            MergerConfiguration config = HintParser.ParseTOML("conf.toml");
            if (config == null)
            {
                Console.WriteLine("Can't continue - config file is not valid!");
                PauseThenErrorExit();
            }
            else
            {
                Console.WriteLine("Config file is valid.");
            }
            Directory.SetCurrentDirectory(config.working_directory);

            //Read in/merge the xml file according to the config
            string untranslatedXMLFilePath = null;
            if (File.Exists(config.ps3_xml_path))
            {
                untranslatedXMLFilePath = config.ps3_xml_path;
            }
            else if(Directory.Exists(config.ps3_xml_path))
            {
                FileConcatenator.MergeFilesInFolder(config.ps3_xml_path, config.ps3_merged_output_path);
                untranslatedXMLFilePath = config.ps3_merged_output_path;
            }
            else
            {
                Console.WriteLine($"Can't find ps3 input file/folder {Path.GetFullPath(config.ps3_xml_path)}");
                PauseThenErrorExit();
            }

            //begin processing
            List<PS3DialogueInstruction> pS3DialogueInstructionsPreFilter = PS3XMLReader.GetPS3DialoguesFromXML(untranslatedXMLFilePath);

            //TODO: scan for files, generate dummy input infos for files which haven't got specified regions.
            //ProcessSingleFile should then attempt to find the correct regions for those files and dump to toml file
            //TODO: clean up console output
            HashSet<string> filePathsToGetStartEnd = new HashSet<string>(); //note: this path includes the input folder name eg "input/test.txt"
            foreach(string fileInInputFolder in Directory.EnumerateFiles(config.input_folder, "*.*", SearchOption.AllDirectories))
            {
                filePathsToGetStartEnd.Add(Path.GetFullPath(fileInInputFolder));
            }

            foreach (InputInfo inputInfo in config.input)
            {
                string tomlInputFilePathNormalized = Path.GetFullPath(Path.Combine(config.input_folder, inputInfo.path));
                
                if (filePathsToGetStartEnd.Contains(tomlInputFilePathNormalized))
                {
                    Console.WriteLine($"\n[  TOML OK   ]: {tomlInputFilePathNormalized} found in config file with region {StringUtils.PrettyPrintListOfListToString(inputInfo.ps3_regions)}");
                    filePathsToGetStartEnd.Remove(tomlInputFilePathNormalized);
                    ProcessSingleFile(pS3DialogueInstructionsPreFilter, config, inputInfo, new List<InputInfo>());
                }       
            }

            List<InputInfo> guessedInputInfos = new List<InputInfo>();
            foreach(string filePathToGetStartEnd in filePathsToGetStartEnd)
            {
                Console.WriteLine($"\n[TOML MISSING]: Start/End of [{filePathToGetStartEnd}] not specified.");
                Console.WriteLine($"Will try best to do matching, but suggest manually inputting start and end.");
                string relativePath = FileUtils.GetRelativePath(filePathToGetStartEnd, Path.GetFullPath(config.input_folder));

                // Since no ps3 region specified, should just search the whole PS3 xml   
                InputInfo wholeFileInputInfo = new InputInfo
                {
                    path = relativePath,
                    ps3_regions = new List<List<int>>(),                 
                };
                ProcessSingleFile(pS3DialogueInstructionsPreFilter, config, wholeFileInputInfo, guessedInputInfos);
            }

            //Save to a file so it can be copied into toml file (if already correct)
            using (StreamWriter sw = FileUtils.CreateDirectoriesAndOpen(config.guessed_matches, FileMode.Create))
            {
                foreach (InputInfo info in guessedInputInfos)
                {
                    sw.WriteLine("# Autogenerated Match");
                    sw.WriteLine("[[input]]");
                    sw.WriteLine($"filename = {info.path}");
                    sw.WriteLine($"ps3_regions = [[{info.ps3_regions[0][0]},{info.ps3_regions[0][1]}]]");
                    sw.WriteLine();
                }
            }

            Console.WriteLine("\n\nProgram Finished!");
            Console.ReadLine();

            return 0;
        }
    }
}
