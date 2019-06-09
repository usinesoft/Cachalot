using System;

namespace UnitTests.TestData
{
    /// <summary>
    ///     No primary key so it can not be serialized
    /// </summary>
    [Serializable]
    public class CacheableTypeKo
    {
        private string _objectData = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";

        public string ObjectData
        {
            get => _objectData;
            set => _objectData = value;
        }
    }
}