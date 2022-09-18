using Client.Core;
using Client.Messages;
using System;

namespace Server
{
    public static class IndexFactory
    {
        public static IndexBase CreateIndex(KeyInfo keyInfo)
        {
            if (keyInfo == null)
                throw new ArgumentNullException(nameof(keyInfo));

            if (keyInfo.IndexType == IndexType.None)
                throw new ArgumentNullException(nameof(keyInfo), "Is not an index property");

            if (keyInfo.IndexType == IndexType.Ordered)
                return new OrderedIndex(keyInfo);
            return new DictionaryIndex(keyInfo);
        }
    }
}