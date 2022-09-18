using Client.Core;
using Client.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AdminConsole.AutoCompleteUtils
{
    public static class AutoComplete
    {

        static readonly string[] Commands = new[]
        {
            "desc",
            "help",
            "select",
            "count",
            "exit",
            "dump",
            "delete",
            "connect",
            "restore",
            "recreate",
            "stop",
            "readonly",
            "readwrite",
            "truncate",
            "last",
            "import",
            "search",
            "pivot"
        };

        static IList<string> Properties(IList<CollectionSchema> knownTypes, string tableName = null)
        {
            if (tableName != null)
            {
                return knownTypes.Where(t => t.CollectionName.Equals(tableName, StringComparison.InvariantCultureIgnoreCase)).SelectMany(t => t.ServerSide).Select(k => k.Name).ToList();
            }

            return knownTypes.SelectMany(t => t.ServerSide).Select(k => k.Name).ToList();
        }


        static IList<string> AfterSelect(IList<CollectionSchema> knownTypes)
        {
            return Properties(knownTypes).Union(new[] { "distinct" }).ToList();
        }


        static string SmartJoin(params string[] parts)
        {
            var result = new StringBuilder();

            foreach (var part in parts)
            {
                if (!string.IsNullOrWhiteSpace(part))
                {
                    result.Append(part.Trim());
                    result.Append(" ");
                }
            }

            return result.ToString().Trim();
        }


        /// <summary>
        /// If the line contains from get the token after
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        static string TryGetTableName(string line)
        {
            var tokens = Tokenizer.TokenizeOneLine(line);

            var afterFrom = false;

            foreach (var token in tokens)
            {
                if (afterFrom)
                {
                    return token.NormalizedText;
                }

                if (token.NormalizedText == "from")
                {
                    afterFrom = true;
                }
            }

            return null;
        }

        static string TrimLast(string line)
        {
            var parts = line.Split();

            return string.Join(' ', parts.Take(parts.Length - 1));

        }

        public static IList<string> GetVariants(string lineBefore, string lineAfter, IList<CollectionSchema> knownTypes)
        {

            if (!string.IsNullOrWhiteSpace(lineAfter) && !lineAfter.StartsWith(" "))
            {
                return new List<string>();
            }


            if (string.IsNullOrWhiteSpace(lineBefore)) // nothing before so it must be a command
            {
                return Commands.Select(c => c).ToList();
            }

            var partsBefore = lineBefore.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            string command = null;

            if (partsBefore.Length == 1) // may be a full command + space or the beginning of a command
            {
                if (!lineBefore.EndsWith(" "))
                {
                    return Commands.Where(c => c.StartsWith(partsBefore[0].ToLowerInvariant())).ToList();
                }

                command = partsBefore[0].ToLowerInvariant();

                if (command == "desc")
                {
                    // collection name may be specified as optional parameter for these commands
                    return knownTypes.Select(t => command + " " + t.CollectionName).ToList();
                }

                if (command == "help")
                {
                    // collection name may be specified as optional parameter for these commands
                    return Commands.Where(c => c != "help").Select(t => command + " " + t).ToList();
                }

                if (command == "count" || command == "select")
                {
                    return knownTypes.Select(t => SmartJoin(command, "from", t.CollectionName, "where")).ToList();
                }

            }

            if (partsBefore.Length == 2 && !lineBefore.EndsWith(" ")) // command plus the beginning of something
            {

                command = partsBefore[0].ToLowerInvariant();
                var toComplete = partsBefore[1].ToLowerInvariant();

                if (command == "desc")
                {
                    // collection name may be specified as optional parameter for these commands
                    return knownTypes.Where(t => t.CollectionName.ToLowerInvariant().StartsWith(toComplete)).Select(t => SmartJoin(command, t.CollectionName)).ToList();
                }

                if (command == "help")
                {
                    // collection name may be specified as optional parameter for these commands
                    return Commands.Where(c => c != "help" && c.StartsWith(toComplete)).Select(t => SmartJoin(command, t)).ToList();
                }

                if (command == "count" || command == "select")
                {
                    return AfterSelect(knownTypes).Where(t => t.ToLowerInvariant().StartsWith(toComplete)).Select(t => SmartJoin(command, t, lineAfter)).ToList();
                }

            }

            command = partsBefore[0].ToLowerInvariant();

            if (command == "select" || command == "count")
            {

                bool beforeFrom = lineAfter != null && lineAfter.ToLowerInvariant().Contains("from");

                bool afterFrom = lineBefore.ToLowerInvariant().Contains("from");

                bool noFrom = !beforeFrom && !afterFrom;

                var lastBefore = partsBefore.Last().ToLowerInvariant();

                // the projection part of the command
                if (lastBefore == "distinct" || lastBefore.Trim().EndsWith(","))
                {
                    return Properties(knownTypes).Select(p => SmartJoin(lineBefore, p, lineAfter)).ToList();
                }

                if (beforeFrom)
                {
                    return Properties(knownTypes).Where(p => p.ToLowerInvariant().StartsWith(lastBefore)).Select(p => SmartJoin(TrimLast(lineBefore), p, lineAfter)).ToList();
                }

                // end of projection section
                if (noFrom)
                {
                    if (!lineBefore.EndsWith(" "))
                    {
                        return Properties(knownTypes).Where(p => p.ToLowerInvariant().StartsWith(lastBefore))
                            .Select(p => SmartJoin(TrimLast(lineBefore), p, lineAfter)).ToList();
                    }

                    return knownTypes.Select(t => SmartJoin(lineBefore, "from", t.CollectionName, "where")).ToList();
                }

                if (lastBefore == "from")
                {
                    if (string.IsNullOrWhiteSpace(lineAfter))
                    {
                        return knownTypes.Select(t => SmartJoin(lineBefore, t.CollectionName, "where")).ToList();
                    }
                }

                if (afterFrom && string.IsNullOrWhiteSpace(lineAfter))
                {

                    var tableName = TryGetTableName(lineBefore);
                    if (!lineBefore.EndsWith(" "))
                    {
                        return Properties(knownTypes, tableName).Where(p => p.ToLowerInvariant().StartsWith(lastBefore)).Select(p => SmartJoin(TrimLast(lineBefore), p)).ToList();

                    }

                    return Properties(knownTypes, tableName).Select(p => SmartJoin(lineBefore, p)).ToList();
                }


            }




            return new List<string>();
        }
    }
}