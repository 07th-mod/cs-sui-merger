using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiMerger.MergedScriptPostProcessing
{
    //base class for all MangaGamer Instructions
    abstract class MangaGamerInstruction
    {
        private readonly bool isPS3;   //True if instruction was generated from ps3 script, False otherwise.

        protected MangaGamerInstruction(bool isPS3, bool noTab)
        {
            this.isPS3 = isPS3;
        }

        //gets the instruction, without the tab character or newline
        public abstract string GetInstruction();

        //gets the instruction, appending 
        public abstract string GetInstructionStandalone();

        //returns true if instruction originated from PS3 xml
        public bool IsPS3() => isPS3;
    }

    /// <summary>
    /// Represents a MangaGamer PlaySE call
    /// For example: PlaySE(3, "jakinimititahaikyo", 256, 64);
    /// </summary>
    class MGPlaySE : MangaGamerInstruction
    {
        readonly int channel;
        readonly string filename; //filename does not include file extension
        readonly int volume;
        readonly int panning;

        public MGPlaySE(int channel, string filename, bool isPS3) : base(isPS3, false)
        {
            this.channel = channel; //game scripts seem to use channel 3 for playing sound effects
            this.filename = filename;
            volume = 256; //default volume - not sure what ranges it is
            panning = 64; //default panning - not sure what ranges it is from-to?
        }

        public override string GetInstruction()
        {
            string ps3Prefix = IsPS3() ? "ps3/" : String.Empty;
            return $"PlaySE({channel}, \"{ps3Prefix}{filename}\", {volume}, {panning});";
        }

        public override string GetInstructionStandalone()
        {
            return "\t" + GetInstruction();
        }
    }

    class MGFadeOutBGM : MangaGamerInstruction
    {
        readonly int channel;
        readonly int fadeTime;

        public MGFadeOutBGM(int channel, int fadeTime, bool isPS3) : base(isPS3, false)
        {
            this.channel = channel;
            this.fadeTime = fadeTime;
        }

        public override string GetInstruction()
        {
            return $"FadeOutBGM( {channel}, {fadeTime}, FALSE );";
        }

        public override string GetInstructionStandalone()
        {
            return "\t" + GetInstruction();
        }
    }

    class MGPlayBGM : MangaGamerInstruction
    {
        readonly int channel;
        readonly string bgmFileName;

        public MGPlayBGM(int channel, string bgmFileName, bool isPS3) : base(isPS3, false)
        {
            this.channel = channel;
            this.bgmFileName = bgmFileName;
        }

        public override string GetInstruction()
        {
            string ps3Prefix = IsPS3() ? "ps3/" : String.Empty;
            return $"PlayBGM( {channel}, \"{ps3Prefix}{bgmFileName}\", 128, 0 );";
        }

        public override string GetInstructionStandalone()
        {
            return "\t" + GetInstruction();
        }
    }

    class GenericInstruction : MangaGamerInstruction
    {
        readonly string data;

        public GenericInstruction(string data, bool isPS3) : base(isPS3, true)
        {
            this.data = data;
        }

        public override string GetInstruction()
        {
            return data.TrimStart();
        }

        public override string GetInstructionStandalone()
        {
            return data;
        }
    }
}
