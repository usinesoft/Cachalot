using System;
using Client.Messages;

namespace Server
{
    public static class IndexFactory
    {
        public static IndexBase CreateIndex(KeyInfo keyInfo)
        {
            if (keyInfo == null)
                throw new ArgumentNullException(nameof(keyInfo));

            if (keyInfo.IsOrdered)
                return new OrderedIndex(keyInfo);
            return new DictionaryIndex(keyInfo);
        }
    }
}