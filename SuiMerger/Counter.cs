using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuiMerger
{
    public class Counter
    {
        Dictionary<string, Dictionary<string, int>> counterDict;

        public Counter()
        {
            this.counterDict = new Dictionary<string, Dictionary<string, int>>();
        }

        public void Add(string key, string countedValue)
        {
            //create the counter for the key if it doesn't exist
            if (!counterDict.ContainsKey(key))
            {
                counterDict[key] = new Dictionary<string, int>();
            }

            var counterToUpdate = counterDict[key];

            //update the count if a count already exists, otherwise set count to 1
            if (!counterToUpdate.ContainsKey(countedValue))
            {
                counterToUpdate[countedValue] = 1;
            }
            else
            {
                counterToUpdate[countedValue] += 1;
            }
        }

        public string AssociationsAsString()
        {
            StringBuilder sb = new StringBuilder();

            foreach (KeyValuePair<string, Dictionary<string, int>> counter in counterDict)
            {
                sb.AppendLine($"Matches for {counter.Key}:");
                foreach (KeyValuePair<string, int> individualCount in counter.Value)
                {
                    sb.AppendLine($"{individualCount.Key}: {individualCount.Value}");
                }
            }

            return sb.ToString();
        }

        public void WriteStatistics(string path)
        {
            File.WriteAllText(path, AssociationsAsString());
        }
    }
}
