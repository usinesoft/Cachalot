using Client.Core;
using System.Runtime.CompilerServices;

namespace Client.Tools
{
    public static class CsvHelper
    {

        public static KeyValue GetTypedValue(string stringValue)
        {
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return new KeyValue(null);
            }

            return new KeyValue(JExtensions.SmartParse(stringValue));
        }

    }
}