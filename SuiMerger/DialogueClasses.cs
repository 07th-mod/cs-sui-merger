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
        public string translatedRawXML;
        public int dlgtype;
        public int debug_length;
        private List<MangaGamerDialogue> otherMangaGamerDialogues = new List<MangaGamerDialogue>();

        public PS3DialogueInstruction(int num, int dlgtype, string data, List<string> previousXML, string rawXML, bool autoTranslate = true)
        {
            //PS3 ID is the dialogue number
            this.ID = num;
            this.dlgtype = dlgtype;
            this.data = autoTranslate ? FileTranslator.TranslateString(data) : data;
            this.previousLinesOrInstructions = new List<string>();
            this.previousLinesOrInstructions.AddRange(previousXML);
            this.debug_length = previousXML.Count;
            this.translatedRawXML = FileTranslator.TranslateString(rawXML);
        }

        public void Add(MangaGamerDialogue mgDialog)
        {
            otherMangaGamerDialogues.Add(mgDialog);
        }

        public List<MangaGamerDialogue> GetOtherMangaGamerDialogues()
        {
            return new List<MangaGamerDialogue>(otherMangaGamerDialogues);
        }

        public string GetPS3StringNoName()
        {
            return PS3DialogueTools.GetPS3StringNoNames(this.data);
        }
    }

    //should really convert all these classes to things which implement interfaces...
    public class PS3DialogueFragment : DialogueBase
    {
        public PS3DialogueInstruction parent;
        public int fragmentID;
        public PS3DialogueFragment previousFragmentInSeries;

        public PS3DialogueFragment(PS3DialogueInstruction parent, string dataFragment, int fragmentID, PS3DialogueFragment previousFragmentInSeries)
        {
            this.parent = parent;
            this.data = dataFragment;
            this.fragmentID = fragmentID;
            this.previousFragmentInSeries = previousFragmentInSeries;
            this.ID = parent.ID;
            
            this.previousLinesOrInstructions = new List<string>();

            //only associate the first fragment with previous instructions of parent
            if(fragmentID == 0)
            {
                this.previousLinesOrInstructions = parent.previousLinesOrInstructions;
            }
        }

        public override string ToString()
        {
            return $"[{ID}.{fragmentID} -> {(otherDialogue == null ? "NULL" : otherDialogue.ID.ToString())}]: {data}";
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

        public override string ToString()
        {
            return $"[{ID} -> {(otherDialogue == null ? "NULL" : otherDialogue.ID.ToString())}]: {data}";
        }
    }

    public class AlignmentPoint
    {
        public PS3DialogueFragment ps3DialogFragment;
        public MangaGamerDialogue mangaGamerDialogue;

        public AlignmentPoint(MangaGamerDialogue mg, PS3DialogueFragment p)
        {
            mangaGamerDialogue = mg;
            ps3DialogFragment = p;
        }

        public override string ToString()
        {
            return $"[[{(mangaGamerDialogue == null ? "NULL" : mangaGamerDialogue.ToString())}]] <-> [[{(ps3DialogFragment == null ? "NULL" : ps3DialogFragment.ToString())}]]";
        }

        public bool IsMatch()
        {
            return ps3DialogFragment != null && mangaGamerDialogue != null;
        }
    }
        

}
