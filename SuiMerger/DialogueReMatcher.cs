using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiMerger
{
    /// <summary>
    /// This class atttempts to match up chunks of unmatched alignment points, which are output by the 
    /// 'standard' differ.
    /// </summary>
    class DialogueReMatcher
    {
        /// <summary>
        /// After basic matching, some alignment points will still remain unmatched. This function takes
        /// a single chunk of unmatched alignment points (not the whole list), and attempts to re-match them.
        /// </summary>
        /// <param name="unmatchedSequence"></param>
        /// <returns></returns>
        public static List<AlignmentPoint> ReMatchUnmatchedDialogue(List<AlignmentPoint> unmatchedSequence)
        {
            //if empty list given, just return an empty list
            if (unmatchedSequence.Count == 0)
            {
                return new List<AlignmentPoint>();
            }

            List<MangaGamerDialogue> unmatchedMGs = new List<MangaGamerDialogue>();
            List<PS3DialogueFragment> unmatchedPS3Fragments = new List<PS3DialogueFragment>();
            List<PS3DialogueInstruction> unmatchedPS3s = new List<PS3DialogueInstruction>();

            HashSet<int> alreadySeenPS3ParentIDs = new HashSet<int>();
            Dictionary<int, PS3DialogueFragment> ps3DialogueIDToFirstFragmentMapping = new Dictionary<int, PS3DialogueFragment>();

            DebugUtils.Print("------------------------------------");
            foreach (AlignmentPoint ap in unmatchedSequence)
            {
                if (ap.mangaGamerDialogue != null)
                {
                    DebugUtils.Print($"MG line: {ap.mangaGamerDialogue.data}");
                    unmatchedMGs.Add(ap.mangaGamerDialogue);
                }

                if (ap.ps3DialogFragment != null)
                {
                    unmatchedPS3Fragments.Add(ap.ps3DialogFragment);

                    if (!alreadySeenPS3ParentIDs.Contains(ap.ps3DialogFragment.parent.ID))
                    {
                        ps3DialogueIDToFirstFragmentMapping.Add(ap.ps3DialogFragment.parent.ID, ap.ps3DialogFragment);
                        alreadySeenPS3ParentIDs.Add(ap.ps3DialogFragment.parent.ID);
                        DebugUtils.Print($"PS3 parent of below missing fragments [{ap.ps3DialogFragment.parent.ID}]: {ap.ps3DialogFragment.parent.data}");
                        unmatchedPS3s.Add(ap.ps3DialogFragment.parent);
                    }

                    DebugUtils.Print($"PS3 child [{ap.ps3DialogFragment.parent.ID}]: {ap.ps3DialogFragment.data}");
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
            foreach (AlignmentPoint ap in reAssociatedAlignmentPoints)
            {
                DebugUtils.Print(ap.ToString());
            }

            return reAssociatedAlignmentPoints;
        }

        /// <summary>
        /// This function is used in the re-matching process
        /// </summary>
        /// <param name="rematchedMGs"></param>
        /// <param name="rematchedPS3s"></param>
        /// <returns></returns>
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
                if (rematchedMGsEnumerator.Current != null)
                {
                    if (rematchedMGsEnumerator.Current.otherDialogue == null)
                    {
                        returnedAlignmentPoints.Add(new AlignmentPoint(rematchedMGsEnumerator.Current, null));
                        rematchedMGsEnumerator.MoveNext();
                    }
                    else
                    {
                        mgHasMatch = true;
                    }
                }

                if (rematchedPS3sEnumerator.Current != null)
                {
                    if (rematchedPS3sEnumerator.Current.otherDialogue == null)
                    {
                        returnedAlignmentPoints.Add(new AlignmentPoint(null, rematchedPS3sEnumerator.Current));
                        rematchedPS3sEnumerator.MoveNext();
                    }
                    else
                    {
                        ps3HasMatch = true;
                    }
                }

                if (mgHasMatch && ps3HasMatch)
                {
                    //the first child shall match the mg line - all other children should have NO match
                    returnedAlignmentPoints.Add(new AlignmentPoint(rematchedMGsEnumerator.Current, rematchedPS3sEnumerator.Current));

                    rematchedMGsEnumerator.MoveNext();
                    rematchedPS3sEnumerator.MoveNext();
                }
            }

            return returnedAlignmentPoints;
        }
    }
}
