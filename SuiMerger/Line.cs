using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiMerger
{
    public class DiffResultProcessor
    {
        //generate side by side diff from the diff result
    }

    public class DiffMatcher
    {
        enum MatchResult
        {
            MatchFound,
            OnlyInA,
            OnlyInB,
        }

        // ALWAYS check MatchResult to determine the correct match type
        // once this is generated, can iterate over all the lines in A 
        // and associate them with some lines from B
        public struct DiffResult
        {
            MatchResult resultType;
            int LineAIndex;
            int LineBIndex;
        }

        void DiffTwoFiles()
        {

        }

        //Note: if you wish to exclude some lines in A or B, modify A and B before passing into this function
        void DoMatching(List<LineInfo> ALines, List<LineInfo> BLines)
        {
            //copy A and B lines into their own file

            //do the diff
            DiffTwoFiles();

            //open the resultant diff

            //check which lines matched, which lines were only present in A, and which lines were only present in B.

            //return a list of DiffResults (do final processing later)
        }
    }

    public struct LineInfo
    {
        //maybe the id should ALWAYS be the line number in each respoecgive file?
        //since you can more or less ignore the XML structure and pretend it's just a line based file...
        public int id;    //the line number in the file this line originally came from (if applicable), or the PS3 ID for the line
        public string text;  //the actual string value of the line
    }

    //this class keeps track of each line's line number
    public class LineTracker
    {
        protected List<LineInfo> lines;

        public LineTracker()
        {
        }

        protected void AddLineWithIndex(string lineToAdd, int lineID)
        {
            lines.Add(new LineInfo()
            {
                id = lineID,
                text = lineToAdd,
            });
        }

        public List<LineInfo> GetLineInfoList()
        {
            return lines;
        }
    }

    public class LineTrackerPS3 : LineTracker
    {
        public LineTrackerPS3() : base()
        {

        }

        void AddLine(string lineToAdd, int lineID)
        {
            AddLineWithIndex(lineToAdd, lineID);
        }
    }

    public class LineTrackerMG : LineTracker
    {
        int lineCount;

        public LineTrackerMG() : base()
        {
            lineCount = 0;
        }

        public void AddLine(string lineToAdd)
        {
            AddLineWithIndex(lineToAdd, lineCount);
            lineCount += 1;
        }
    }
}
