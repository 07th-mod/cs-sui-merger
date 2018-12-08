using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiMerger
{
    class MangaGamerScriptReader
    {
        //Searches for japanese dialogue in the 'OutputLine' functions in the MangaGamer scripts
        public static List<MangaGamerDialogue> GetDialogueLinesFromMangaGamerScript(string filePath, out List<string> mgLeftovers)
        {
            IEnumerable<string> allLines = File.ReadLines(filePath);
            List<MangaGamerDialogue> dialogues = new List<MangaGamerDialogue>();

            int lineNumber = 0;
            List<string> previousLines = new List<string>();
            foreach (string line in allLines)
            {
                if (line.Contains("OutputLine"))
                {
                    if (Differ.PrepareStringForDiff(line).Length > 0)
                    {
                        dialogues.Add(new MangaGamerDialogue(lineNumber, line, previousLines));
                        previousLines.Clear();
                    }
                    else
                    {
                        Console.WriteLine($"WARNING: MG dialogue has no japanese characters - it won't be used for matching: [{line}]");
                        previousLines.Add(line);
                    }
                }
                else
                {
                    previousLines.Add(line);
                }

                lineNumber += 1;
            }

            mgLeftovers = previousLines;

            return dialogues;
        }
    }
}
