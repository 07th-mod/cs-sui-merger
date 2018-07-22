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
        public static List<MangaGamerDialogue> GetDialogueLinesFromMangaGamerScript(string filePath)
        {
            IEnumerable<string> allLines = File.ReadLines(filePath);
            List<MangaGamerDialogue> dialogues = new List<MangaGamerDialogue>();

            int lineNumber = 0;
            List<string> previousLines = new List<string>();
            foreach (string line in allLines)
            {
                if (line.Contains("OutputLine") && Differ.PrepareStringForDiff(line).Length > 4)
                {
                    dialogues.Add(new MangaGamerDialogue(lineNumber, line, previousLines));
                    previousLines.Clear();
                }
                else
                {
                    previousLines.Add(line);
                }

                lineNumber += 1;
            }

            return dialogues;
        }
    }
}
