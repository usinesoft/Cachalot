using Client.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Client.Tools
{
    public static class CsvHelper
    {


        /// <summary>
        /// Parse a string value
        /// </summary>
        /// <param name="stringValue"></param>
        /// <returns></returns>
        public static KeyValue GetTypedValue(string stringValue)
        {
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return new KeyValue(null);
            }

            return new KeyValue(JExtensions.SmartParse(stringValue));
        }

        /// <summary>
        /// Detect the separator from the header of a CSV file
        /// </summary>
        /// <param name="header"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="FormatException"></exception>
        public static char DetectSeparator(string header)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                throw new ArgumentException($"'{nameof(header)}' cannot be null or whitespace.", nameof(header));
            }


            // automatically detect ",", "\t" or ";" used as separator; 
            if (header.Contains(','))
            {
                return ',';
            }
            else if (header.Contains(';'))
            {
                return ';';
            }
            else if (header.Contains('\t'))
            {
                return '\t';
            }


            throw new FormatException($"Can not detect column separator from header {header}");

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