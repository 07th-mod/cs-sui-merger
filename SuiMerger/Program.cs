using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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

    class Program
    {

        /// <summary>
        /// Given a list of PS3DialogueInstructions, returns a new list formed by the given "regions", filtered by PS3.ID
        /// Eg, if regions is a list like [[1,3], [10,11]], it will return dialogues with PS3IDs = 1,2,3,10,11
        /// </summary>
        /// <param name="inputList"></param>
        /// <param name="regions">INCLUSIVE list of PS3 ID regions to that the inputList should be filtered for</param>
        /// <returns></returns>
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

        /// <summary>
        /// After the alignment points are created, call this function to do some sanity checks against the original mangagamer and ps3 dialog fragments.
        /// It checks that
        ///     - the ordering of each list is maintained
        ///     - all input dialogues are still present in the output
        /// </summary>
        /// <param name="alignmentPoints"></param>
        /// <param name="allMangaGamerDialogue"></param>
        /// <param name="fragments"></param>
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

        /// <summary>
        /// If you match the very small mangagamer script against the huge Sui script, the list of alignment points will
        /// have a small amount of matching and the rest which won't match anything. This function performs a 
        /// postprocessing step, which removes PS3 instructions at the start and end of the which don't match anything,
        /// preventing an excessively large diff output.
        /// </summary>
        /// <param name="alignmentPoints"></param>
        /// <returns></returns>
        static List<AlignmentPoint> TrimAlignmentPoints(List<AlignmentPoint> alignmentPoints)
        {
            //assume entire match region (incase triming can't be done found)
            int firstMatch = 0;
            int lastMatch = alignmentPoints.Count-1;
            
            //remove non-matching PS3 alignmentpoints at the start of the file
            for(int i = 0; i < alignmentPoints.Count; i++)
            {
                AlignmentPoint ap = alignmentPoints[i];
                if (ap.mangaGamerDialogue != null)
                {
                    firstMatch = i;
                    break;
                }
            }

            //remove non-matching PS3 alignmentpoints at the end of the file
            for (int i = alignmentPoints.Count - 1; i > 0; i--)
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

        //Write statisics on which lines were not matched 
        static void WriteAlignmentStatistics(List<AlignmentPoint> alignmentPoints, string outputPath)
        {
            List<AlignmentPoint> unalignedPoints = alignmentPoints.Where(alignmentPoint => alignmentPoint.mangaGamerDialogue == null || alignmentPoint.ps3DialogFragment == null).ToList();
            List<AlignmentPoint> unalignedMGPoints = unalignedPoints.Where(alignmentPoint => alignmentPoint.ps3DialogFragment == null).ToList();
            List<AlignmentPoint> unalignedPS3Points = unalignedPoints.Where(alignmentPoint => alignmentPoint.mangaGamerDialogue == null).ToList();

            StringBuilder reportSB = new StringBuilder();

            reportSB.AppendLine("See [INPUT_SCRIPT_NAME]_side_by_side_debug.html for detailed diff information.");
            reportSB.AppendLine($"Num unaligned MG : {unalignedMGPoints.Count ,10} instructions ({(double)unalignedMGPoints.Count  / alignmentPoints.Count:P})");
            reportSB.AppendLine($"Num unaligned PS3: {unalignedPS3Points.Count,10} instructions ({(double)unalignedPS3Points.Count / alignmentPoints.Count:P})");
            reportSB.AppendLine($"Unaligned Entries Follow....");
            reportSB.AppendLine("---------------------------------------------------------------------------------\n");

            foreach (var alignmentPoint in unalignedPoints)
            {
                bool isMangaGamerDialogue = alignmentPoint.mangaGamerDialogue != null;
                string alignmentPointType = isMangaGamerDialogue ? "MG " : "PS3";
                DialogueBase dialogueToPrint = isMangaGamerDialogue ? (DialogueBase)alignmentPoint.mangaGamerDialogue : (DialogueBase)alignmentPoint.ps3DialogFragment;
                string IDString = dialogueToPrint.ID.ToString();

                if(!isMangaGamerDialogue)
                {
                    //Format ID string differently for PS3 because it has a fragment ID
                    IDString = $"{alignmentPoint.ps3DialogFragment.ID}.{alignmentPoint.ps3DialogFragment.fragmentID}";

                    //Only for PS3: print out any previous XML instructions
                    foreach (var previousLineOrInstruction in dialogueToPrint.previousLinesOrInstructions)
                    {
                        reportSB.AppendLine($"\t\t|------: {previousLineOrInstruction} ({alignmentPointType} [{IDString,7}])");
                    }
                }                

                reportSB.AppendLine($"{alignmentPointType} [{IDString,8}]: {dialogueToPrint.data}");
            }

            File.WriteAllText(outputPath, reportSB.ToString());
        }

        static PS3DialogueFragment SearchForBestPS3Dialogues(List<PS3DialogueFragment> ps3DialogueFragments, MangaGamerDialogue mgDialogueToSearch)
        {
            string mangaGamerChoiceForDiff = Differ.PrepareStringForDiff(mgDialogueToSearch.data);

            float lowestDistance = float.MaxValue;
            PS3DialogueFragment bestFrag = null;

            foreach (var frag in ps3DialogueFragments)
            {
                string fragmentForDiff = Differ.PrepareStringForDiff(frag.data);

                //if ps3 fragment is of length 0, skip it
                if (fragmentForDiff.Length == 0)
                {
                    continue;
                }

                float rawLevenshtein = (float)Fastenshtein.Levenshtein.Distance(mangaGamerChoiceForDiff, fragmentForDiff);
                float scaledLevenshtein = rawLevenshtein / Math.Max(mangaGamerChoiceForDiff.Length, fragmentForDiff.Length);

                if (scaledLevenshtein < lowestDistance)
                {
                    lowestDistance = scaledLevenshtein;
                    bestFrag = frag;
                }
            }

            return bestFrag;
        }

        static int? AnalyseEntries(List<PS3DialogueFragment> ps3DialogueFragments, List<MangaGamerDialogue> allMangaGamerDialogue, int amount, bool isStart)
        {
            int startIndex = isStart ? 0 : allMangaGamerDialogue.Count - amount;
            int endIndex = isStart ? amount : allMangaGamerDialogue.Count;

            List<int> resultIndexes = new List<int>();
            for (int i = startIndex; i < endIndex; i++)
            {
                var mgdialogue = allMangaGamerDialogue[i];
                var bestMatch = SearchForBestPS3Dialogues(ps3DialogueFragments, mgdialogue);
                Console.WriteLine($"MG : {mgdialogue.ToString()}");
                Console.WriteLine($"PS3: {bestMatch.ToString()}\n");
                resultIndexes.Add(bestMatch.ID);
            }

            bool resultsAreSequential = true;
            for (int i = 0; i < resultIndexes.Count-1; i++)
            {
                if(resultIndexes[i+1] - resultIndexes[i] > 1)
                {
                    resultsAreSequential = false;
                }
            }

            if(resultsAreSequential)
            {
                return isStart ? resultIndexes[0] : resultIndexes[resultIndexes.Count-1];
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// processes a single mangagamer script, attempting to merge the matching ps3 instructions
        /// </summary>
        /// <param name="pS3DialogueInstructionsPreFilter"></param>
        /// <param name="config"></param>
        /// <param name="mgInfo"></param>
        /// <param name="guessedInputInfos"></param>
        static List<PartialSubScriptToMerge> ProcessSingleFile(List<PS3DialogueInstruction> pS3DialogueInstructionsPreFilter, MergerConfiguration config, InputInfo mgInfo, List<InputInfo> guessedInputInfos)
        {
            string fullPath = Path.Combine(config.input_folder, mgInfo.path);
            string pathNoExt = Path.GetFileNameWithoutExtension(fullPath);

            string debug_side_by_side_diff_path_MG  = Path.Combine(config.output_folder, pathNoExt + "_side_by_side_debug.html");
            string debug_alignment_statistics = Path.Combine(config.output_folder, pathNoExt + "_statistics_debug.txt");

            List<PS3DialogueInstruction> pS3DialogueInstructions = GetFilteredPS3Instructions(pS3DialogueInstructionsPreFilter, mgInfo.ps3_regions);           

            //load all the mangagamer lines from the mangagamer file
            List<MangaGamerDialogue> allMangaGamerDialogue = MangaGamerScriptReader.GetDialogueLinesFromMangaGamerScript(fullPath, out List<string> mg_leftovers);

            //PS3 Dialogue fragments
            List<PS3DialogueFragment> ps3DialogueFragments = new List<PS3DialogueFragment>();
            int ps3DialogueIndex = 0;
            foreach (PS3DialogueInstruction ps3Dialogue in pS3DialogueInstructions)
            {
                List<string> splitDialogueStrings = PS3DialogueTools.SplitPS3StringNoNames(ps3Dialogue.data);
                PS3DialogueFragment previousPS3DialogueFragment = null;
                for (int i = 0; i < splitDialogueStrings.Count; i++)
                {
                    //dummy instructions index into the ps3DialogueList (for now...)
                    PS3DialogueFragment ps3DialogueFragment = new PS3DialogueFragment(ps3Dialogue, splitDialogueStrings[i], i, previousPS3DialogueFragment);
                    ps3DialogueFragments.Add(ps3DialogueFragment);
                    previousPS3DialogueFragment = ps3DialogueFragment;
                }
                ps3DialogueIndex++;
            }

            //If no ps3 regions specified, scan for regions, then print and let user fill in?
            if (mgInfo.ps3_regions.Count == 0)
            {
                Console.WriteLine($"The file [{mgInfo.path}] does not have the PS3 region marked in the conf.toml file!");
                Console.WriteLine($"Scanning for PS3 region...");

                //print the first few and last mangagamer instructions
                //skip if length too small?
                Console.WriteLine("------- Finding first 5 entries -------");
                int? startResult = AnalyseEntries(ps3DialogueFragments, allMangaGamerDialogue, amount: 10, isStart: true);
                if(startResult.HasValue)
                {
                    Console.WriteLine($"Best guess at start PS3 ID: {startResult.Value}");
                }
                else
                {
                    Console.WriteLine("Not sure about start PS3 ID. Please inspect manually.");
                }

                Console.WriteLine("------- Finding last 5 entries -------");
                int? endResult = AnalyseEntries(ps3DialogueFragments, allMangaGamerDialogue, amount: 10, isStart: false);
                if (endResult.HasValue)
                {
                    Console.WriteLine($"Best guess at last PS3 ID: {endResult.Value}");
                }
                else
                {
                    Console.WriteLine("Not sure about last PS3 ID. Please inspect manually.");
                }

                string result_start_id = "<START_REGION>";
                string result_end_id = "<END_REGION>";

                if(startResult.HasValue && endResult.HasValue)
                {
                    Console.WriteLine("AUTOREGION SUCCESS: You can copy this into the conf.toml file\n\n");
                    result_start_id = startResult.Value.ToString();
                    result_end_id = endResult.Value.ToString();
                }
                else
                {
                    Console.WriteLine($"AUTOREGION FAIL: Region couldn't be determined confidently. Please use the above information and the ps3 script" +
                        $"to determine the PS3 region manually, then place the results in the conf.toml file as per below");
                }

                Console.WriteLine("[[input]]");
                Console.WriteLine($@"path = ""{mgInfo.path}""");
                Console.WriteLine($"ps3_regions = [[{result_start_id}, {result_end_id}]]");
                Console.WriteLine("No output will be generated for this script until the program is run again.");
                Console.WriteLine("Press ENTER to move to the next script...");

                Console.ReadKey();
                return new List<PartialSubScriptToMerge>();
            }

            //Diff the dialogue
            List<AlignmentPoint> allAlignmentPoints = Differ.DoDiff(config.temp_folder, allMangaGamerDialogue, ps3DialogueFragments, debugFilenamePrefix: pathNoExt);

            //Sanity check the alignment points by making sure there aren't missing any values
            SanityCheckAlignmentPoints(allAlignmentPoints, allMangaGamerDialogue, ps3DialogueFragments);

            //trim alignment points to reduce output
            List<AlignmentPoint> alignmentPoints = config.trim_after_diff ? TrimAlignmentPoints(allAlignmentPoints) : allAlignmentPoints;

            //Write statistics on how well matched the alignment points are
            WriteAlignmentStatistics(alignmentPoints, debug_alignment_statistics);

            //DEBUG: generate the side-by-side diff
            DifferDebugUtilities.PrintHTMLSideBySideDiff(alignmentPoints, debug_side_by_side_diff_path_MG);

            //Insert PS3 instructions
            string mergedOutputPath = Path.Combine(config.output_folder, pathNoExt + "_merged.xml.txt");
            SaveMergedMGScript(alignmentPoints, mergedOutputPath, mg_leftovers);

            // >>>> UnMerge ModCallScriptSection: Before using the results, we need to reverse the step we did earlier, by unmerging any merged files back into multiple files.

            //Use the inserted instructions
            string finalOutputWithMergedForkedScripts = Path.Combine(config.output_folder, pathNoExt + "_OUTPUT.txt");
            MergedScriptPostProcessing.PostProcessingMain.InsertMGLinesUsingPS3XML(mergedOutputPath, finalOutputWithMergedForkedScripts, config);

            return ForkingScriptMerger.GetForkedScriptContentFromMergedScript(config.pre_input_folder, finalOutputWithMergedForkedScripts);
        }

        //wrapper to allow 'pausing' of program but still garbage collect everything to force files to write-out
        static void RunProgram()
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
            else if (Directory.Exists(config.ps3_xml_path))
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

            // >>>> Merge ModCallScriptSection: pre-process each file in the pre-input folder, ignoring sub scripts
            // >>>> this inserts the subscripts inline into the script
            Regex emptyMainRegex = new Regex(@"void\s*main\(\s*\)\s*{\s*}");

            foreach (string pathOfPossibleScript in Directory.EnumerateFiles(config.pre_input_folder, "*.*", SearchOption.AllDirectories))
            {
                // Skip scripts which have an empty main file - these are sub scripts
                string fileText = File.ReadAllText(pathOfPossibleScript);
                if (emptyMainRegex.Match(fileText).Success)
                {
                    Console.WriteLine($"Skipping script {pathOfPossibleScript} as it looks like a Sub-Script");
                    continue;
                }

                string filename = Path.GetFileName(pathOfPossibleScript);

                // >>>> Merge ModCallScriptSection: Before loading the manga gamer dialog, copy in any ModCallScriptSection(...) calls. This will be undone at a later stage
                List<string> mergedScriptLines = ForkingScriptMerger.MergeForkedScript(config.pre_input_folder, filename, true);

                File.WriteAllLines(Path.Combine(config.input_folder, filename), mergedScriptLines);
            }


            //TODO: scan for files, generate dummy input infos for files which haven't got specified regions.
            //ProcessSingleFile should then attempt to find the correct regions for those files and dump to toml file
            //TODO: clean up console output
            HashSet<string> filePathsToGetStartEnd = new HashSet<string>(); //note: this path includes the input folder name eg "input/test.txt"
            foreach (string fileInInputFolder in Directory.EnumerateFiles(config.input_folder, "*.*", SearchOption.AllDirectories))
            {
                filePathsToGetStartEnd.Add(Path.GetFullPath(fileInInputFolder));
            }

            List<PartialSubScriptToMerge> forkedScriptContentToMergeList = new List<PartialSubScriptToMerge>();
            foreach (InputInfo inputInfo in config.input)
            {
                string tomlInputFilePathNormalized = Path.GetFullPath(Path.Combine(config.input_folder, inputInfo.path));

                if (filePathsToGetStartEnd.Contains(tomlInputFilePathNormalized))
                {
                    Console.WriteLine($"\n[  TOML OK   ]: {tomlInputFilePathNormalized} found in config file with region {StringUtils.PrettyPrintListOfListToString(inputInfo.ps3_regions)}");
                    filePathsToGetStartEnd.Remove(tomlInputFilePathNormalized);
                    forkedScriptContentToMergeList.AddRange(
                        ProcessSingleFile(pS3DialogueInstructionsPreFilter, config, inputInfo, new List<InputInfo>())
                    );
                }
            }

            //Unmerge all the forked scripts (zonik_....txt)
            ForkingScriptMerger.UnMergeForkedScripts(config.pre_input_folder, config.output_folder, forkedScriptContentToMergeList);

            //Save to a file so it can be copied into toml file (if already correct)
            using (StreamWriter sw = FileUtils.CreateDirectoriesAndOpen(config.guessed_matches, FileMode.Create))
            {
                List<InputInfo> guessedInputInfos = new List<InputInfo>();
                foreach (string filePathToGetStartEnd in filePathsToGetStartEnd)
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

                    foreach (InputInfo info in guessedInputInfos)
                    {
                        sw.WriteLine("# Autogenerated Match");
                        sw.WriteLine("[[input]]");
                        sw.WriteLine($"filename = {info.path}");
                        sw.WriteLine($"ps3_regions = {StringUtils.PrettyPrintListOfListToString(info.ps3_regions)}");
                        sw.WriteLine();
                        sw.Flush();
                    }
                    guessedInputInfos.Clear();
                }
            }

        }

        /// <summary>
        /// processes multiple mangagamer scripts and attempts to merge the ps3 instructions into them
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        static int Main(string[] args)
        {
            RunProgram();
            
            Console.WriteLine("\n\nProgram Finished!");
            Console.ReadKey();

            return 0;
        }

        static void Pause()
        {
            Console.WriteLine(">>>> PROGRAM IS PAUSED <<<<<<\n\n Press ENTER to continue....");
            Console.ReadKey();
        }

        static void PauseThenErrorExit()
        {
            Pause();
            Environment.Exit(-1);
        }
    }
}
