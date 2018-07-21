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
            other.otherDialogue = this;
        }

        public int ID;
        public string data; //raw string data from the translated XML file
        public DialogueBase otherDialogue; //the other dialogue object (PS3 or Mangagamer) which "matches" this object. Null if doesn't match.
    }

    // Holds the data for a single Dialogue instruction (from the XML file).
    // Automatically translates the string portion in the constructor
    public class PS3DialogueInstruction : DialogueBase
    {
        public int dlgtype;

        public PS3DialogueInstruction(int num, int dlgtype, string data, bool autoTranslate = true)
        {
            //PS3 ID is the dialogue number
            this.ID = num;
            this.dlgtype = dlgtype;
            this.data = autoTranslate ? FileTranslator.TranslateString(data) : data;
        }
    }

    public class MangaGamerDialogue : DialogueBase
    {
        //mangagamer ID is the line number in mg script file
        public MangaGamerDialogue(int lineNumber, string data)
        {
            this.ID = lineNumber;
            this.data = data;
        }
    }
}
