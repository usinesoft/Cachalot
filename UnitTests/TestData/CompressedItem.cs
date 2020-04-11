using System;
using System.Collections.Generic;
using System.Text;
using Client.Interface;

namespace UnitTests.TestData
{

    [Storage(true)]
    public class CompressedItem
    {
        [PrimaryKey(KeyDataType.IntKey)]
        public int Id { get; set; }

        public string Data { get; set; } = new string('a', 2000);
    }
}
