using System;
using System.Collections.Generic;
using System.IO;
using Client;
using Client.Core;

namespace Server.Persistence
{
    internal class ObjectProcessor : IPersistentObjectProcessor
    {
        private readonly DataContainer _dataContainer;

        private readonly Dictionary<string, List<CachedObject>> _temporaryStorage =
            new Dictionary<string, List<CachedObject>>();

        public ObjectProcessor(DataContainer dataContainer)
        {
            _dataContainer = dataContainer;
        }

        public void Process(byte[] data)
        {
            var item = SerializationHelper.ObjectFromBytes<CachedObject>(data, SerializationMode.ProtocolBuffers,
                false); // the json itself may be compressed, but the persisted object is never compressed

            Dbg.Trace($"processing persistent object {data.Length} bytes {item}");

            if (!_temporaryStorage.TryGetValue(item.FullTypeName, out var list))
            {
                list = new List<CachedObject>();
                _temporaryStorage[item.FullTypeName] = list;
            }

            list.Add(item);

            if (!_dataContainer.DataStores.ContainsKey(item.FullTypeName))
                throw new NotSupportedException($"The type {item.FullTypeName} is not present in the database schema");
        }

        public void EndProcess(string dataPath = null)
        {

            HashSet<string> frequentTokens = new HashSet<string>();
            foreach (var pair in _temporaryStorage)
            {
                var store = _dataContainer.DataStores[pair.Key];
                store.InternalPutMany(pair.Value, true, null);
                frequentTokens.UnionWith(store.GetMostFrequentTokens(100));
                
            }
                
            // generate a helper file containing most frequent tokens

            if (dataPath != null && frequentTokens.Count > 0)
            {
                File.WriteAllLines(Path.Combine(dataPath, "Most_reequent_tokens.txt"),  frequentTokens);
            }
            
            _temporaryStorage.Clear();

            Dbg.Trace("done loading persistent objects into memory");
        }
    }
}