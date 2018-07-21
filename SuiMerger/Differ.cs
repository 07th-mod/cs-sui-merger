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
                int c_codepoint = (int)c; //NOTE: doesn't work for >16 bit codepoints (> 0xFFFF)
                if (c_codepoint >= 0x3040 && c_codepoint <= 0x9faf)
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
        static public void WriteListOfDialogueToFile(IEnumerable<DialogueBase> dialogues, string outputFileName)
        {
            //write the diff-prepared manga gamer dialogue to a file
            using (FileStream fs = new FileStream(outputFileName, FileMode.Create))
            {
                foreach (DialogueBase line in dialogues)
                {
                    string preprocessedLine = PrepareStringForDiff(line.data);
                    byte[] stringAsBytes = new UTF8Encoding(true).GetBytes($"{preprocessedLine}\n");
                    fs.Write(stringAsBytes, 0, stringAsBytes.Length);
                }
            }
        }

        public static void DoDiff(string tempFolderPath, List<MangaGamerDialogue> mangaGamerDialogueList, List<PS3DialogueInstruction> ps3DialogueList)
        {
            string mangaGamerDiffInputPath = Path.Combine(tempFolderPath, "diffInputA.txt");
            string PS3DiffInputPath = Path.Combine(tempFolderPath, "diffInputB.txt");
            string diffOutputPath = Path.Combine(tempFolderPath, "diffOutput.txt");

            //write the diff-prepared manga gamer dialogue to a file
            WriteListOfDialogueToFile(mangaGamerDialogueList, mangaGamerDiffInputPath);

            //write the diff-prepared ps3 dialogue to a file
            WriteListOfDialogueToFile(ps3DialogueList, PS3DiffInputPath);

            //do the diff
            string diffResult = RunDiffTool(mangaGamerDiffInputPath, PS3DiffInputPath);

            //save the diff to file for debugging
            using (FileStream fs = new FileStream(diffOutputPath, FileMode.Create))
            {
                byte[] stringAsBytes = new UTF8Encoding(true).GetBytes(diffResult);
                fs.Write(stringAsBytes, 0, stringAsBytes.Length);
            }

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
                int mgIndex = 0;
                int ps3Index = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    //classify the line type:
                    char lineType = line[0];
                    if(lineType == ' ') //lines match
                    {
                        mangaGamerDialogueList[mgIndex].Associate(ps3DialogueList[ps3Index]);
                        Console.WriteLine($"Line {mangaGamerDialogueList[mgIndex].ID} of mangagamer associates with PS3 ID {ps3DialogueList[ps3Index].ID}");

                        mgIndex++;
                        ps3Index++;
                    }
                    else if(lineType == '+') //only exist in ps3
                    {
                        ps3DialogueList[ps3Index] = null;
                        ps3Index++;
                    }
                    else if(lineType == '-') //only exist in mangagamer
                    {
                        mangaGamerDialogueList[mgIndex] = null;
                        mgIndex++;
                    }
                }
            }
        }
    }
}
