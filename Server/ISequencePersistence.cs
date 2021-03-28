using System.Collections.Generic;

namespace Server
{
    public interface ISequencePersistence
    {
        Dictionary<string, int> LoadValues(string path);

        void SaveValues(Dictionary<string, int> lastValueByName, string fullPath = null);
    }
}