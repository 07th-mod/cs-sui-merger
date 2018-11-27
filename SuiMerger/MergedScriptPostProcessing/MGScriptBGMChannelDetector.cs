using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SuiMerger.MergedScriptPostProcessing
{
    class MGScriptBGMChannelDetector
    {
        static Regex playBGMGetFileName = new Regex(@"PlayBGM\(\s*(\d)\s*,\s*""([^""]*)""");

        /// <summary>
        /// Same as DetectBGMChannel() but if bgm channel couldn't be detected, uses user supplied default
        /// </summary>
        /// <returns></returns>
        public static int DetectBGMChannelOrDefault(string mgScriptPath, MergerConfiguration configuration, int defaultChannel, bool PrintOnFoundChannelAndWarnings)
        {
            int? maybeBGMChannel = MGScriptBGMChannelDetector.DetectBGMChannel(mgScriptPath, configuration);

            if (maybeBGMChannel != null)
            {
                if (PrintOnFoundChannelAndWarnings) { Console.WriteLine($"Detected channel [{maybeBGMChannel.Value}] as BGM Channel number"); };
                return maybeBGMChannel.Value;
            }
            else
            {
                if (PrintOnFoundChannelAndWarnings) { Console.WriteLine($"WARNING: Could not detect bgmChannel. Will use channel {defaultChannel} when inserting PS3 music"); }
                return defaultChannel;
            }
        }

        /// <summary>
        /// Given an original manga gamer script file, attempts to detect which channel is used for BGM
        /// As input it needs to know the filenames of the BGM music files.
        /// It obtains this from the MergerConfiguration argument (the .toml file)
        /// It also uses the bgm length threshold to determine the difference between a BGM and a sound effect.
        /// </summary>
        /// <param name="mgScriptPath"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static int? DetectBGMChannel(string mgScriptPath, MergerConfiguration configuration)
        {
            Dictionary<int, int> channelCounter = new Dictionary<int, int>();

            using (StreamReader mgScript = new StreamReader(mgScriptPath, Encoding.UTF8))
            {
                //Count how many times each channel plays a BGM
                //channel is considered to play a BGM if the file being played is longer than the configured length
                string line;
                while ((line = mgScript.ReadLine()) != null)
                {
                    int? maybeBGMChannel = TryGetBGMMusicChannelOnSingleLine(line, searchFolders: configuration.bgm_folders, bgmLengthThresholdSeconds: configuration.music_threshold_seconds);
                    if (maybeBGMChannel != null)
                    {
                        int BGMChannel = (int)maybeBGMChannel;
                        if (channelCounter.ContainsKey(BGMChannel))
                        {
                            channelCounter[BGMChannel] += 1;
                        }
                        else
                        {
                            channelCounter[BGMChannel] = 0;
                        }
                    }
                }

                //TODO: Debug - remove later
                foreach (KeyValuePair<int, int> item in channelCounter)
                {
                    Console.WriteLine($"channel: {item.Key} count: {item.Value}");
                }

                //return the channel which has the max number of BGM plays
                foreach (KeyValuePair<int, int> item in channelCounter.OrderByDescending(key => key.Value))
                {
                    return item.Key;
                }
            }

            //if no playBGMs were found, return null
            return null;
        }

        //This function tries to determine the BGM channel, given a single script line.
        //If the channel couldn't be determined or is not a music file, will return null. Otherwise returns the channel.
        // Reasons for channel determination failure are below:
        //  - doesn't contain a BGMPlay command or has an invalid BGMPlay command
        //  - audio file in the BGMPlay command couldn't be found
        //  - audio file in the BGMPlay command is < 30 seconds indicating it's not a music file
        // List<string> searchFolders - folders to search for the music file
        // double bgmLengthThresholdSeconds - length in seconds above which an audio file is considered to be BGM
        private static int? TryGetBGMMusicChannelOnSingleLine(string line, List<string> searchFolders, double bgmLengthThresholdSeconds)
        {
            //if can't parse line, assume not a playbgm line
            Match match = playBGMGetFileName.Match(line);
            if (!match.Success)
                return null;

            int channel = int.Parse(match.Groups[1].Value);
            string audioFileName = match.Groups[2].Value;

            //Try to get the audio length, scanning each given folder. If file not found, assume not a playbgm line
            //Note that in the script, the file extension is not specified - therfore add '.ogg' to filename
            double? audioLength = null;
            foreach (string searchFolder in searchFolders)
            {
                try
                {
                    audioLength = GetAudioLength(Path.Combine(searchFolder, audioFileName + ".ogg"));
                    break;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"ERROR: Couldn't open BGM folder [{searchFolder}]. BGM Folder Detection might not work correctly!");
                }
            }

            if (audioLength == null)
                return null;

            bool isMusic = audioLength >= bgmLengthThresholdSeconds;

            Console.WriteLine($"Audio file {audioFileName} on channel {channel} is {audioLength} seconds long. Type: {(isMusic ? "Music" : "Not Music")}");

            if (isMusic)
            {
                return channel;
            }

            return null;
        }

        /// <summary>
        /// Returns the audio length of a given file in seconds (includes fractional part)
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static double GetAudioLength(string path) => TagLib.File.Create(path).Properties.Duration.TotalSeconds;
    }
}
