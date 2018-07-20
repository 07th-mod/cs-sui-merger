using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiMerger
{
    public struct LineInfo
    {
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
