using System;
using System.Collections.Generic;
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

        public void EndProcess()
        {
            foreach (var pair in _temporaryStorage)
                _dataContainer.DataStores[pair.Key].InternalPutMany(pair.Value, true, null);

            _temporaryStorage.Clear();

            Dbg.Trace("done loading persistent objects into memory");
        }
    }
}