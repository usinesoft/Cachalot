using Client.Core;
using Client.Interface;

namespace Tests.TestData
{
    [Storage(Layout.Compressed)]
    public class CompressedItem
    {
        [ServerSideValue(IndexType.Primary)] public int Id { get; set; }

        public string Data { get; set; } = new string('a', 2000);
    }
}