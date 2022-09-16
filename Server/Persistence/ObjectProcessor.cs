using Client;
using Client.Core;
using System;
using System.Collections.Generic;
using System.IO;

namespace Server.Persistence
{
    internal class ObjectProcessor : IPersistentObjectProcessor
    {
        private readonly DataContainer _dataContainer;

        private readonly Dictionary<string, List<PackedObject>> _temporaryStorage =
            new Dictionary<string, List<PackedObject>>();

        public ObjectProcessor(DataContainer dataContainer)
        {
            _dataContainer = dataContainer;
        }

        public void Process(byte[] data)
        {
            var item = SerializationHelper.ObjectFromBytes<PackedObject>(data, SerializationMode.ProtocolBuffers,
                false); // the json itself may be compressed, but the persisted object is never compressed

            Dbg.Trace($"processing persistent object {data.Length} bytes {item}");

            if (!_temporaryStorage.TryGetValue(item.CollectionName, out var list))
            {
                list = new List<PackedObject>();
                _temporaryStorage[item.CollectionName] = list;
            }

            list.Add(item);

            var store = _dataContainer.TryGetByName(item.CollectionName);

            if (store == null)
                throw new NotSupportedException($"The type {item.CollectionName} is not present in the database schema");
        }

        public void EndProcess(string dataPath = null)
        {

            HashSet<string> frequentTokens = new HashSet<string>();
            foreach (var pair in _temporaryStorage)
            {
                var store = _dataContainer.TryGetByName(pair.Key);

                store.InternalPutMany(pair.Value, true);

                frequentTokens.UnionWith(store.GetMostFrequentTokens(100));

            }

            // generate a helper file containing most frequent tokens

            if (dataPath != null && frequentTokens.Count > 0)
            {
                File.WriteAllLines(Path.Combine(dataPath, "Most_frequent_tokens.txt"), frequentTokens);
            }

            _temporaryStorage.Clear();

            Dbg.Trace("done loading persistent objects into memory");
        }
    }
}