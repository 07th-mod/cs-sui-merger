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
        void GetDialogueLinesFromMangaGamerScript(string filePath)
        {
            IEnumerable<string> allLines = File.ReadLines(filePath);
            List<MangagamerDialogue> dialogues = new List<MangagamerDialogue>();

            int lineNumber = 0;
            foreach (string line in allLines)
            {
                if (line.Contains("OutputLine") && Differ.PrepareStringForDiff(line).Length > 4)
                {
                    dialogues.Add(new MangagamerDialogue(lineNumber, line));
                }

                lineNumber += 1;
            }
        }
    }
}
