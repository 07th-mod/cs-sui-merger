using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiMerger
{
    public class Differ
    {
        //keep only japanese characters
        //remove spaces
        //is_japanese_punct = in_range(code_point, 0x3000, 0x303f) NO
        //is_hiragana = in_range(code_point, 0x3040, 0x309f) YES
        //is_katakana = in_range(code_point, 0x30a0, 0x30ff) YES
        //is_ideograph = in_range(code_point, 0x4e00, 0x9faf) YES
        //this function should also remove newlines etc.
        public static string PrepareStringForDiff(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (StringUtils.CharIsJapanese(c))
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        //Note: this function may not work on files with greater than 1_000_000 lines
        public static string RunDiffTool(string inputPathA, string inputPathB)
        {
            string command = "git";
            string arguments = $"diff --no-index --ignore-blank-lines -w -U1000000 \"{inputPathA}\" \"{inputPathB}\"";
            Console.WriteLine($"Command: [{command}] Arguments: [{arguments}]");
            ProcessStartInfo psi = new ProcessStartInfo
            {
                Arguments = arguments,
                FileName = command,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                RedirectStandardError = true,
            };
            
            Process p = Process.Start(psi);

            //diff stderr->console, diff stdout->string
            p.ErrorDataReceived += GitDiffErrorDataReceived;
            p.BeginErrorReadLine();
            string output = p.StandardOutput.ReadToEnd();

            p.WaitForExit();
            if (p.ExitCode != 0 && p.ExitCode != 1) //I think exit code 1 is when there is a warning printed by git
            {
                throw new Exception($"Git returned non-zero or one exit code: {p.ExitCode}");
            }

            return output;
        }

        static void GitDiffErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine($"Git Diff: {e.Data }\n");
        }

        //NOTE: this function adds a newline to the end of each string.
        static public void WriteListOfDialogueToFile(IEnumerable<DialogueBase> dialogues, string outputFileName, bool isPS3)
        {
            //write the diff-prepared manga gamer dialogue to a file
            using (FileStream fs = new FileStream(outputFileName, FileMode.Create))
            {
                foreach (DialogueBase line in dialogues)
                {
                    string preprocessedLine = line.data;

                    preprocessedLine = PrepareStringForDiff(preprocessedLine);

                    byte[] stringAsBytes = new UTF8Encoding(true).GetBytes($"{preprocessedLine}\n");
                    fs.Write(stringAsBytes, 0, stringAsBytes.Length);
                }
            }
        }

        public static List<AlignmentPoint> GetAlignmentPointsFromMGPS3Array(List<MangaGamerDialogue> rematchedMGs, List<PS3DialogueFragment> rematchedPS3s)
        {
            List<AlignmentPoint> returnedAlignmentPoints = new List<AlignmentPoint>();

            IEnumerator<MangaGamerDialogue> rematchedMGsEnumerator = rematchedMGs.GetEnumerator();
            IEnumerator<PS3DialogueFragment> rematchedPS3sEnumerator = rematchedPS3s.GetEnumerator();

            rematchedMGsEnumerator.MoveNext();
            rematchedPS3sEnumerator.MoveNext();

            //Continue to iterate if either enumerator has items left
            while (rematchedMGsEnumerator.Current != null || rematchedPS3sEnumerator.Current != null)
            {
                bool mgHasMatch = false;
                bool ps3HasMatch = false;
                if(rematchedMGsEnumerator.Current != null)
                {
                    if(rematchedMGsEnumerator.Current.otherDialogue == null)
                    {
                        returnedAlignmentPoints.Add(new AlignmentPoint(rematchedMGsEnumerator.Current, null));
                        rematchedMGsEnumerator.MoveNext();
                    }
                    else
                    {
                        mgHasMatch = true;
                    }
                }

                if(rematchedPS3sEnumerator.Current != null)
                {
                    if(rematchedPS3sEnumerator.Current.otherDialogue == null)
                    {
                        returnedAlignmentPoints.Add(new AlignmentPoint(null, rematchedPS3sEnumerator.Current));
                        rematchedPS3sEnumerator.MoveNext();
                    }
                    else
                    {
                        ps3HasMatch = true;
                    }
                }

                if(mgHasMatch && ps3HasMatch)
                {
                    //the first child shall match the mg line - all other children should have NO match
                    returnedAlignmentPoints.Add(new AlignmentPoint(rematchedMGsEnumerator.Current, rematchedPS3sEnumerator.Current));

                    rematchedMGsEnumerator.MoveNext();
                    rematchedPS3sEnumerator.MoveNext();
                }
            }

            return returnedAlignmentPoints;
        }

        //This function performs the diff given the two lists of dialogue.
        //It then UPDATES the values in the mangaGamerDialogueList and the ps3DialogueList (the DialogueBase.other value is updated on each dialogue object!)
        //If a dialogue cannot be associated, it is set to NULL.
        public static List<PS3DialogueFragment> DoDiff(string tempFolderPath, List<MangaGamerDialogue> mangaGamerDialogueList, List<PS3DialogueInstruction> ps3DialogueList, out List<AlignmentPoint> alignmentPoints)
        {
            //Convert PS3 Dialogue list into list of subsections before performing diff - this can be re-assembled later!
            string mangaGamerDiffInputPath = Path.Combine(tempFolderPath, "diffInputA.txt");
            string PS3DiffInputPath = Path.Combine(tempFolderPath, "diffInputB.txt");
            string diffOutputPath = Path.Combine(tempFolderPath, "diffOutput.txt");

            //Generate dummy mangaGamerDialogues here
            List<PS3DialogueFragment> dummyPS3Instructions = new List<PS3DialogueFragment>();
            int ps3DialogueIndex = 0;
            foreach (PS3DialogueInstruction ps3Dialogue in ps3DialogueList)
            {
                List<string> splitDialogueStrings = PS3DialogueTools.SplitPS3StringNoNames(ps3Dialogue.data);
                PS3DialogueFragment previousPS3DialogueFragment = null;
                for (int i = 0; i < splitDialogueStrings.Count; i++)
                {
                    //dummy instructions index into the ps3DialogueList (for now...)
                    PS3DialogueFragment ps3DialogueFragment = new PS3DialogueFragment(ps3Dialogue, splitDialogueStrings[i], i, previousPS3DialogueFragment);
                    dummyPS3Instructions.Add(ps3DialogueFragment);
                   previousPS3DialogueFragment = ps3DialogueFragment;
                }
                ps3DialogueIndex++;
            }

            //write the diff-prepared manga gamer dialogue to a file
            WriteListOfDialogueToFile(mangaGamerDialogueList, mangaGamerDiffInputPath, isPS3: false);

            //write the diff-prepared ps3 dialogue to a file
            WriteListOfDialogueToFile(dummyPS3Instructions, PS3DiffInputPath, isPS3: true);

            //do the diff
            string diffResult = RunDiffTool(mangaGamerDiffInputPath, PS3DiffInputPath);

            //save the diff to file for debugging
            using (FileStream fs = new FileStream(diffOutputPath, FileMode.Create))
            {
                byte[] stringAsBytes = new UTF8Encoding(true).GetBytes(diffResult);
                fs.Write(stringAsBytes, 0, stringAsBytes.Length);
            }

            alignmentPoints = new List<AlignmentPoint>();
            using (StringReader reader = new StringReader(diffResult))
            {
                string line;
                //skip the header information
                while ((line = reader.ReadLine()) != null)
                {
                    if (line[0] == '@')
                        break;
                }

                //TODO: need to think of the best way to categorize all mangagamer lines...
                //a ' ' means linesare the same
                //a '+' means line is present in PS3 ONLY           (it was 'added' in the ps3 version)
                //a '-' means line is present in mangagamer ONLY    (it was 'removed' from the ps3 version)
                List<AlignmentPoint> unmatchedSequence = new List<AlignmentPoint>();
                int mgIndex = 0;
                int ps3Index = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    //classify the line type:
                    char lineType = line[0];
                    if(lineType == ' ') //lines match
                    {
                        PS3DialogueFragment dummyPS3Instruction = dummyPS3Instructions[ps3Index];
                        MangaGamerDialogue currentMangaGamer = mangaGamerDialogueList[mgIndex];

                        //associate the fragment with the mangagamer dialogue
                        currentMangaGamer.Associate(dummyPS3Instruction);

                        if (unmatchedSequence.Count > 0)
                        {
                            List<MangaGamerDialogue> unmatchedMGs = new List<MangaGamerDialogue>();
                            List<PS3DialogueFragment> unmatchedPS3Fragments = new List<PS3DialogueFragment>();
                            List<PS3DialogueInstruction> unmatchedPS3s = new List<PS3DialogueInstruction>();

                            HashSet<int> alreadySeenPS3ParentIDs = new HashSet<int>();
                            Dictionary<int, PS3DialogueFragment> ps3DialogueIDToFirstFragmentMapping = new Dictionary<int, PS3DialogueFragment>();

                            Console.WriteLine("------------------------------------");
                            foreach (AlignmentPoint ap in unmatchedSequence)
                            {
                                if (ap.mangaGamerDialogue != null)
                                {
                                    Console.WriteLine($"MG line: {ap.mangaGamerDialogue.data}");
                                    unmatchedMGs.Add(ap.mangaGamerDialogue);
                                }

                                if (ap.ps3DialogFragment != null)
                                {
                                    unmatchedPS3Fragments.Add(ap.ps3DialogFragment);

                                    if (!alreadySeenPS3ParentIDs.Contains(ap.ps3DialogFragment.parent.ID))
                                    {
                                        ps3DialogueIDToFirstFragmentMapping.Add(ap.ps3DialogFragment.parent.ID, ap.ps3DialogFragment);
                                        alreadySeenPS3ParentIDs.Add(ap.ps3DialogFragment.parent.ID);
                                        Console.WriteLine($"PS3 parent of below missing fragments [{ap.ps3DialogFragment.parent.ID}]: {ap.ps3DialogFragment.parent.data}");
                                        unmatchedPS3s.Add(ap.ps3DialogFragment.parent);
                                    }

                                    Console.WriteLine($"PS3 child [{ap.ps3DialogFragment.parent.ID}]: {ap.ps3DialogFragment.data}");
                                }
                            }

                            //Try and match the unmatched lines
                            List<InOrderLevenshteinMatcher.LevenshteinResult> greedyMatchResults = InOrderLevenshteinMatcher.DoMatching(unmatchedMGs, unmatchedPS3s);

                            //Use the match results to set associations
                            foreach (var result in greedyMatchResults)
                            {
                                MangaGamerDialogue mgToAssign = unmatchedMGs[result.mgIndex];
                                //want to get the first ps3 fragment associated with the Dialogue. Use hashmap we made earlier.
                                PS3DialogueFragment ps3FragmentToAssign = ps3DialogueIDToFirstFragmentMapping[unmatchedPS3s[result.ps3Index].ID];
                                mgToAssign.Associate(ps3FragmentToAssign);
                            }

                            //iterate through the list and add alignment points appropriately
                            List<AlignmentPoint> reAssociatedAlignmentPoints = GetAlignmentPointsFromMGPS3Array(unmatchedMGs, unmatchedPS3Fragments);
                            
                            //Debug: Print out re-assigned alignment points for debugging
                            foreach(AlignmentPoint ap in reAssociatedAlignmentPoints)
                            {
                                Console.WriteLine(ap.ToString());
                            }

                            //add the backlog of re-matched alignment points to the output 
                            alignmentPoints.AddRange(reAssociatedAlignmentPoints);

                            //add the just found instruction
                            alignmentPoints.Add(new AlignmentPoint(currentMangaGamer, dummyPS3Instruction));

                            unmatchedSequence.Clear();
                        }
                        mgIndex++;
                        ps3Index++;
                    }
                    else if(lineType == '+') //only exist in ps3
                    {
                        PS3DialogueFragment currentDialog = dummyPS3Instructions[ps3Index];
                        unmatchedSequence.Add(new AlignmentPoint(null, currentDialog));
                        ps3Index++;
                    }
                    else if(lineType == '-') //only exist in mangagamer
                    {
                        MangaGamerDialogue currentDialog = mangaGamerDialogueList[mgIndex];
                        unmatchedSequence.Add(new AlignmentPoint(currentDialog, null));
                        mgIndex++;
                    }
                }
            }

            return dummyPS3Instructions;
        }
    }
}
