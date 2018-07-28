using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiMerger
{
    class InOrderLevenshteinMatcher
    {
        public class LevenshteinResult : IComparable<LevenshteinResult>
        {
            public int mgIndex;
            public int ps3Index;
            public int score;

            public int CompareTo(LevenshteinResult other)
            {
                //should return positive value if first object (this object) > other object
                return score - other.score;
            }

            public override string ToString()
            {
                return $"{mgIndex}->{ps3Index} Dist:{score}";
            }
        }

        //Returns an array of LevenshteinResults, sorted lowest DISTANCE [i=0] to highest DISTANCE [i=end] (eg from highest simliarity to lowest similarity)
        private static List<LevenshteinResult> GetAllLevenshteinSorted(List<MangaGamerDialogue> unmatchedmgs, List<PS3DialogueInstruction> unmatchedPS3)
        {
            List<LevenshteinResult> allLevenshteinResults = new List<LevenshteinResult>();

            //For each mg dialogue, Calculate similarity against ps3 dialogue
            for (int mgIndex = 0; mgIndex < unmatchedmgs.Count; mgIndex++)
            {
                for (int ps3Index = 0; ps3Index < unmatchedPS3.Count; ps3Index++)
                {
                    string mgString = Differ.PrepareStringForDiff(unmatchedmgs[mgIndex].data);
                    string ps3String = Differ.PrepareStringForDiff(unmatchedPS3[ps3Index].data);
                    int rawLevenshtein = Fastenshtein.Levenshtein.Distance(mgString, ps3String);

                    //divide the raw levenschtein by length of the longer string, to get a percentage match
                    //double scaledLevenshtein = rawLevenshtein / Math.Max(mgString.Length, ps3String.Length) * 100.0;

                    allLevenshteinResults.Add(new LevenshteinResult()
                    {
                        mgIndex = mgIndex,
                        ps3Index = ps3Index,
                        score = rawLevenshtein, //(int) Math.Round(scaledLevenshtein),
                    });
                }
            }

            //sort the list
            allLevenshteinResults.Sort();

            return allLevenshteinResults;
        }

        //takes in a sorted list of levenstein results, outputs a greedily chosne subset of those results, sorted by mgIndex
        //lengthMG  - number of entries in the original manga gamer list of dialogue
        //lengthPS3 - number of entries in the original PS3 list of dialogue
        //Note: this function can return an empty list if one of the input lengths is 0
        private static List<LevenshteinResult> GetBestMatchCombination(List<LevenshteinResult> levenshteinResultsSorted, int lengthMG, int lengthPS3)
        {
            //these variables define the highest/lowest dialogues chosen so far in each given list/
            //define the bounds of the upper and lower range of dialogue
            // They are updated each time a new match is chosen. TODO: draw a diagram, explain high level algorithm
            int minMG = lengthMG;
            int maxMG = -1;

            int minPS3 = lengthPS3;
            int maxPS3 = -1;

            List<LevenshteinResult> greedyLevenshteinSubset = new List<LevenshteinResult>();

            foreach (LevenshteinResult bestResult in levenshteinResultsSorted)
            {
                bool inLowerRange = bestResult.mgIndex < minMG && bestResult.ps3Index < minPS3;
                bool inUpperRange = bestResult.mgIndex > maxMG && bestResult.ps3Index > maxPS3;

                if (inLowerRange || inUpperRange)
                {
                    DebugUtils.Print($"\n Adding {bestResult.ToString()}");
                    greedyLevenshteinSubset.Add(bestResult);
                }

                if (inLowerRange)
                {
                    minMG = bestResult.mgIndex;
                    minPS3 = bestResult.ps3Index;

                    DebugUtils.Print($"min mg was  {minMG} now {bestResult.mgIndex}");
                    DebugUtils.Print($"min ps3 was {minPS3} now {bestResult.ps3Index}");
                }

                if (inUpperRange)
                {
                    maxMG = bestResult.mgIndex;
                    maxPS3 = bestResult.ps3Index;

                    DebugUtils.Print($"max mg was  {maxMG} now {bestResult.mgIndex}");
                    DebugUtils.Print($"max ps3 was {maxPS3} now {bestResult.ps3Index}");
                }

                if((minMG == 0 || minPS3 == 0) && (maxMG == (lengthMG - 1)  || (maxPS3 == lengthPS3 - 1)))
                {
                    DebugUtils.Print($"Finishing early min:({minMG},{minPS3}) max:({maxMG},{maxPS3})");
                    break;
                }
            }

            return greedyLevenshteinSubset;
        }

        //Note: the 'unmatchedps3fragment' argument is only an output, and this function fills in the "otherDialogue" property if applicable.
        //the function also fills in the "otherDialogue" property of the unmatchedmgs
        //TODO: move the ps3fragment-> full dialogue convrsion into this function, or make a separate function
        public static List<LevenshteinResult> DoMatching(List<MangaGamerDialogue> unmatchedmgs, List<PS3DialogueInstruction> unmatchedPS3)
        {
            List<LevenshteinResult> allLevenshteinResultsSorted = GetAllLevenshteinSorted(unmatchedmgs, unmatchedPS3);

            foreach (LevenshteinResult result in allLevenshteinResultsSorted)
            {
                DebugUtils.Print("\n" + result.ToString());
                DebugUtils.Print(unmatchedmgs[result.mgIndex].data);
                DebugUtils.Print(unmatchedPS3[result.ps3Index].data);
            }

            //Now greedily retreive the best matches. This step is required if the best matches imply out-of-order matching, 
            //which is not allowed (eg line 1 of mg matches to line 2 of ps3, but line 2 of mg matches line 1 of ps3).
            List<LevenshteinResult> greedyMatchResults = GetBestMatchCombination(allLevenshteinResultsSorted, unmatchedmgs.Count, unmatchedPS3.Count);

            //foreach (LevenshteinResult result in greedyMatchResults)
            //{
            //    MangaGamerDialogue mgToAssign = unmatchedmgs[result.mgIndex];
            //    PS3DialogueInstruction ps3DialogueToAssign = unmatchedPS3[result.ps3Index];
            //    PS3DialogueFragment ps3FragmentToAssign = ps3DialogueToAssign.
            //    mgToAssign.
            //}

            //After sorting complete, don't forget to print/save lines which did not match! Can do this by scanning which lines still have association=null


            //Choose the best match, preserving order
            return greedyMatchResults;
        }
    }
}
