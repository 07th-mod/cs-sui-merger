using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiMerger
{
    public class DialogueBase
    {
        public void Associate(DialogueBase other)
        {
            this.otherDialogue = other;

            if (other != null)
            { 
                other.otherDialogue = this;
            }
        }

        public int ID;
        public string data; //raw string data from the translated XML file
        public DialogueBase otherDialogue; //the other dialogue object (PS3 or Mangagamer) which "matches" this object. Null if doesn't match.
        public List<string> previousLinesOrInstructions; //the previous xml instructions to this one, or previous lines to this one
    }

    // Holds the data for a single Dialogue instruction (from the XML file).
    // Automatically translates the string portion in the constructor
    public class PS3DialogueInstruction : DialogueBase
    {
        public int dlgtype;
        public int debug_length;
        private List<MangaGamerDialogue> otherMangaGamerDialogues = new List<MangaGamerDialogue>();

        public PS3DialogueInstruction(int num, int dlgtype, string data, List<string> previousXML, bool autoTranslate = true)
        {
            //PS3 ID is the dialogue number
            this.ID = num;
            this.dlgtype = dlgtype;
            this.data = autoTranslate ? FileTranslator.TranslateString(data) : data;
            this.previousLinesOrInstructions = new List<string>();
            this.previousLinesOrInstructions.AddRange(previousXML);
            this.debug_length = previousXML.Count;
        }

        public void Add(MangaGamerDialogue mgDialog)
        {
            otherMangaGamerDialogues.Add(mgDialog);
        }

        public List<MangaGamerDialogue> GetOtherMangaGamerDialogues()
        {
            return new List<MangaGamerDialogue>(otherMangaGamerDialogues);
        }
    }

    public class MangaGamerDialogue : DialogueBase
    {
        //mangagamer ID is the line number in mg script file
        public MangaGamerDialogue(int lineNumber, string data, List<string> previousLines)
        {
            this.ID = lineNumber;
            this.data = data;
            this.previousLinesOrInstructions = new List<string>();
            this.previousLinesOrInstructions.AddRange(previousLines);
        }
    }
}
