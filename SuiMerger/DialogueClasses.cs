using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiMerger
{
    /// <summary>
    /// base class for both PS3 style and MangaGamer style dialogue
    /// </summary>
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

        public int ID;      //ID differs depending base class - see below
        public string data; //raw string data from the translated XML file
        public DialogueBase otherDialogue; //the other dialogue object (PS3 or Mangagamer) which "matches" this object. Null if doesn't match.
        public List<string> previousLinesOrInstructions; //the previous xml instructions to this one, or previous lines to this one
    }

    /// <summary>
    /// Holds the data for a single Dialogue instruction (from the XML file).
    /// Automatically translates the string portion in the constructor
    /// </summary>
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

    /// <summary>
    /// should really convert all these classes to things which implement interfaces...
    /// This represents a PS3 dialogue fragment - some ps3 dialogues contain multiple sentence fragments within them
    /// The ps3 dialogues are split up into fragments so that match better with the MangaGamer Script.
    /// </summary>
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

    /// <summary>
    /// Represents a line of mangagamer dialogue
    /// </summary>
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

    /// <summary>
    /// This class represents a possible alignment (matching) of a ps3 dialog fragment and a manga gamer dialogue line
    /// Note that it is possible for one of the fields to be null (eg, the ps3 does not match any particular manga gamer dialogue, or the other way round)
    /// It is used to represent the matching between two scripts and all the information in two scripts as a list of AlignmentPoints.
    /// </summary>
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
