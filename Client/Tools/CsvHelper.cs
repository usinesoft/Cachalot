using Client.Core;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

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


        /// <summary>
        /// Spli a csv line into values. If the separator is inside "" it is not taken into acount
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public static List<string> SplitCsvLine(string line, char separator)
        {
            var stringValues = new List<string>();

            bool ignoreSeparator = false;

            var currentValue = new StringBuilder();

            foreach (char c in line)
            {
                if (c == '"') // ignore separator inside "" according to csv specification
                {
                    if (ignoreSeparator)
                    {
                        ignoreSeparator = false;
                    }
                    else
                    {
                        ignoreSeparator = true;
                    }
                }
                else if (c == separator && !ignoreSeparator)
                {
                    var stringValue = currentValue.ToString();
                    stringValues.Add(stringValue);
                    currentValue.Clear();
                }
                else
                {
                    currentValue.Append(c);
                }
            }

            // add the last column
            if (!line.EndsWith(separator))
            {
                var stringValue = currentValue.ToString();

                stringValues.Add(stringValue);

            }

            return stringValues;
        }

    }
}