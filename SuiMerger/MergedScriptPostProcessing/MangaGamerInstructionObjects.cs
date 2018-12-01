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
        private readonly bool noTab;

        protected MangaGamerInstruction(bool isPS3, bool noTab)
        {
            this.isPS3 = isPS3;
            this.noTab = noTab;
        }

        //gets the instruction, without the tab character or newline
        protected abstract string GetInstruction();

        //returns the instruction string with tab character
        public string GetInstructionForScript()
        {
            if (noTab)
            {
                return GetInstruction();
            }
            else
            {
                return $"\t{GetInstruction()}";
            }
        }

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

        public MGPlaySE(string filename, bool isPS3) : base(isPS3, false)
        {
            channel = 3; //game scripts seem to use channel 3 for playing sound effects
            this.filename = filename;
            volume = 256; //default volume - not sure what ranges it is
            panning = 64; //default panning - not sure what ranges it is from-to?
        }

        protected override string GetInstruction() => $"PlaySE({channel}, \"{filename}\", {volume}, {panning});";
    }

    class MGFadeOutBGM : MangaGamerInstruction
    {
        readonly int channel;
        readonly int fadeTime;

        public MGFadeOutBGM(int channel, int ps3Duration, bool isPS3) : base(isPS3, false)
        {
            this.channel = channel;
            this.fadeTime = (int)Math.Round(ps3Duration / 60.0 * 1000.0);
        }

        protected override string GetInstruction()
        {
            return $"FadeOutBGM( {channel}, {fadeTime}, FALSE );";
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

        protected override string GetInstruction()
        {
            return $"PlayBGM( {channel}, \"{bgmFileName}\", 128, 0 );";
        }
    }

    class GenericInstruction : MangaGamerInstruction
    {
        readonly string data;

        public GenericInstruction(string data, bool isPS3) : base(isPS3, true)
        {
            this.data = data;
        }

        protected override string GetInstruction()
        {
            return data;
        }
    }
}
