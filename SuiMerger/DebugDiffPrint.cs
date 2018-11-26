using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SuiMerger
{
    class DifferDebugUtilities
    {
        /// <summary>
        /// functions used only for debug printing the diff, not the actually diff operation
        /// </summary>
            static string htmlTemplate = @"
<!DOCTYPE html>
<html>
<head>
<meta charset=""UTF-8""> 
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<style>
/* IF line-height IS NOT SET, JAPANESE CHARS WILL MAKE SOME ROWS UNEQUAL HEIGHT - this breaks the alignment! */
* {
    box-sizing: border-box;
    line-height: 1.2em; 
}

/* Create two equal columns that floats next to each other */
.column {
    float: left;
    width: 50%;
    overflow-x: scroll;
}

/* Clear floats after the columns */
.row:after {
    content: """";
    display: table;
    clear: both;
}
</style>
</head>
<body>

<h2>Script Comparison</h2>

<div class=""row"">
  <div class=""column"">
  <pre>|||MANGAGAMER_SCRIPT_HERE|||</pre>
  </div>
  <div class=""column"">
  <pre>|||PS3_SCRIPT_HERE|||</pre>
  </div>
</div>

</body>
</html>
";

        static void ConvertBlankToDot(List<string> lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim().Length == 0)
                {
                    lines[i] = "." + lines[i];
                }
            }
        }

        static void WriteSideBySideTopPad(TextWriter swA, TextWriter swB, List<string> listA_original, List<string> listB_original)
        {
            int numA = listA_original.Count;
            int numB = listB_original.Count;

            List<string> listA = new List<string>();
            List<string> listB = new List<string>();

            if (numA < numB)
            {
                listA.AddRange(Enumerable.Repeat("", numB - numA));
            }

            if (numB < numA)
            {
                listB.AddRange(Enumerable.Repeat("", numA - numB));
            }

            listA.AddRange(listA_original);
            listB.AddRange(listB_original);

            //Need to convert blank lines to dots so that lines are lined up in HTML
            ConvertBlankToDot(listA);
            ConvertBlankToDot(listB);

            StringUtils.WriteStringList(swA, listA);
            StringUtils.WriteStringList(swB, listB);
        }

        public static void PrintSideBySideDiff(List<AlignmentPoint> alignmentPoints, out string mg_debug, out string ps3_debug)
        {
            StringWriter swMG = new StringWriter();
            StringWriter swPS3 = new StringWriter();

            //iterate through each dialogue in PS3 list
            List<string> currentPS3ToSave = new List<string>();
            List<string> currentMangaGamerToSave = new List<string>();
            foreach (AlignmentPoint alignmentPoint in alignmentPoints)
            {
                //add the previous lines (instructions

                if (alignmentPoint.ps3DialogFragment != null)
                {
                    PS3DialogueFragment ps3 = alignmentPoint.ps3DialogFragment;
                    currentPS3ToSave.AddRange(ps3.previousLinesOrInstructions);
                    currentPS3ToSave.Add($">>>> [{ps3.ID}.{ps3.fragmentID} -> {(ps3.otherDialogue == null ? "NoMatch" : ps3.otherDialogue.ID.ToString())}]: {ps3.data}");
                }

                if (alignmentPoint.mangaGamerDialogue != null)
                {
                    MangaGamerDialogue mg = alignmentPoint.mangaGamerDialogue;
                    currentMangaGamerToSave.AddRange(mg.previousLinesOrInstructions);
                    currentMangaGamerToSave.Add($">>>> [{mg.ID} -> {(mg.otherDialogue == null ? "NoMatch" : mg.otherDialogue.ID.ToString())}]: {mg.data}");
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

            mg_debug = swMG.ToString();
            ps3_debug = swPS3.ToString();
        }

        public static void PrintHTMLSideBySideDiff(List<AlignmentPoint> alignmentPoints, string outputPath)
        {
            PrintSideBySideDiff(alignmentPoints, out string mg_debug, out string ps3_debug);

            File.WriteAllText(
                outputPath,
                htmlTemplate.Replace("|||MANGAGAMER_SCRIPT_HERE|||", WebUtility.HtmlEncode(mg_debug))
                .Replace("|||PS3_SCRIPT_HERE|||", WebUtility.HtmlEncode(ps3_debug))
                .Replace("\r\n", "\n")
            );
        }
    }
        
}
