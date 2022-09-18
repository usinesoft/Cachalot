using Client.Core;
using System.Collections.Generic;

namespace AdminConsole.AutoCompleteUtils
{
    public class CyclingAutoComplete
    {
        private readonly List<string> _commands = new List<string>
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
            "search"
        };

        private readonly char[] _tokenDelimiters = { ' ', ',' };
        private int _autoCompleteIndex;
        private IList<string> _autoCompleteList;
        private string _previousAutoComplete = string.Empty;

        public IList<CollectionSchema> KnownTypes { get; set; } = new List<CollectionSchema>();

        public string AutoComplete(string lineBefore, string lineAfter, CyclingDirections cyclingDirection = CyclingDirections.Forward, bool ignoreCase = true)
        {
            if (IsPreviousCycle(lineBefore))
            {
                if (cyclingDirection == CyclingDirections.Forward)
                    return ContinueCycle();
                return ContinueCycleReverse();
            }


            _autoCompleteList = AutoCompleteUtils.AutoComplete.GetVariants(lineBefore, lineAfter, KnownTypes);


            //var parts = line.Split(_tokenDelimiters).Select(p => p.ToLower()).Where(p => !string.IsNullOrEmpty(p))
            //    .ToList();

            //var tables = KnownTypes?.Select(t => t.CollectionName.ToLower()).ToList();


            //if (parts.Count == 0)
            //{
            //    _autoCompleteList = AutoCompleteUtils.AutoComplete.GetAutoCompletedLines("", "", "", _commands);
            //}
            //else if (parts.Count == 1)
            //{
            //    var part1 = parts.First();

            //    if (_commands.Contains(part1))
            //    {
            //        if (part1 == "delete" || part1 == "select" || part1 == "count")
            //            _autoCompleteList =
            //                AutoCompleteUtils.AutoComplete.GetAutoCompletedLines(part1 + " from ", " where", "", tables);
            //        else if (part1 == "help")
            //            _autoCompleteList =
            //                AutoCompleteUtils.AutoComplete.GetAutoCompletedLines(part1 + " ", "", "", _commands);
            //        else if (part1 == "desc")
            //            _autoCompleteList =
            //                AutoCompleteUtils.AutoComplete.GetAutoCompletedLines(part1 + " ", "", "", tables);
            //        else if (part1 == "truncate")
            //            _autoCompleteList =
            //                AutoCompleteUtils.AutoComplete.GetAutoCompletedLines(part1 + " ", "", "", tables);
            //        else if (part1 == "search")
            //            _autoCompleteList =
            //                AutoCompleteUtils.AutoComplete.GetAutoCompletedLines(part1 + " ", "", "", tables);
            //    }
            //    else
            //    {
            //        _autoCompleteList =
            //            AutoCompleteUtils.AutoComplete.GetAutoCompletedLines("", "", part1, _commands);
            //    }
            //}
            //else if (parts.Count == 2)
            //{
            //    var part1 = parts.First();
            //    var part2 = parts.Last();

            //    if (_commands.Contains(part1))
            //    {
            //        if (part1 == "delete" || part1 == "select" || part1 == "count")
            //        {
            //            _autoCompleteList =
            //                AutoCompleteUtils.AutoComplete.GetAutoCompletedLines(part1 + " ", " where", part2, tables);
            //        }
            //        else if (part1 == "help")
            //        {
            //            _autoCompleteList =
            //                AutoCompleteUtils.AutoComplete.GetAutoCompletedLines(part1 + " ", "", part2, _commands);
            //        }
            //        else if (part1 == "desc")
            //        {
            //            _autoCompleteList =
            //                AutoCompleteUtils.AutoComplete.GetAutoCompletedLines(part1 + " ", "", part2, tables);
            //        }

            //        else if (part1 == "connect")
            //        {
            //            var configFiles = Directory.EnumerateFiles(Directory.GetCurrentDirectory(), part2 + "*.xml")
            //                .Select(Path.GetFileName).ToList();
            //            _autoCompleteList =
            //                AutoCompleteUtils.AutoComplete.GetAutoCompletedLines(part1 + " ", "", part2, configFiles);
            //        }
            //        else
            //        {
            //            _autoCompleteList =
            //                AutoCompleteUtils.AutoComplete.GetAutoCompletedLines("", "", part1, _commands);
            //        }
            //    }
            //    else
            //    {
            //        _autoCompleteList =
            //            AutoCompleteUtils.AutoComplete.GetAutoCompletedLines("", "", part1, _commands);
            //    }
            //}
            //else if (parts.Count >= 3)
            //{
            //    var part1 = parts[0];
            //    var part2 = parts[1];
            //    var part3 = parts[2];


            //    if (_commands.Contains(part1) && tables.Contains(part2) && part3 == "where")
            //    {
            //        if (part1 == "delete" || part1 == "select" || part1 == "count")
            //        {
            //            var typeDescription = KnownTypes.FirstOrDefault(d => d.CollectionName.ToLower() == part2);

            //            var endsWithComma = line.Trim().Last() == ',';

            //            var toComplete = "";

            //            if (!endsWithComma && parts.Count > 3) toComplete = parts.Last();


            //            var beforeLastSeperator = line.Substring(0, line.Length - toComplete.Length);

            //            if (typeDescription != null)
            //            {
            //                var allFields = typeDescription.IndexFields
            //                    .Union(typeDescription.UniqueKeyFields).ToList();

            //                allFields.Add(typeDescription.PrimaryKeyField);

            //                _autoCompleteList =
            //                    AutoCompleteUtils.AutoComplete.GetAutoCompletedLines(beforeLastSeperator, "",
            //                        toComplete, allFields.Select(f => f.Name.ToLower()).ToList());
            //            }
            //        }
            //    }
            //    else
            //    {
            //        _autoCompleteList =
            //            AutoCompleteUtils.AutoComplete.GetAutoCompletedLines("", "", part1, _commands);
            //    }
            //}


            if (_autoCompleteList?.Count == 0)
                return lineBefore + lineAfter;

            return StartNewCycle();
        }

        private string StartNewCycle()
        {
            _autoCompleteIndex = 0;
            var autoCompleteLine = _autoCompleteList[_autoCompleteIndex];
            _previousAutoComplete = autoCompleteLine;
            return autoCompleteLine;
        }

        private string ContinueCycle()
        {
            _autoCompleteIndex++;
            if (_autoCompleteIndex >= _autoCompleteList.Count)
                _autoCompleteIndex = 0;
            var autoCompleteLine = _autoCompleteList[_autoCompleteIndex];
            _previousAutoComplete = autoCompleteLine;
            return autoCompleteLine;
        }

        private string ContinueCycleReverse()
        {
            _autoCompleteIndex--;
            if (_autoCompleteIndex < 0)
                _autoCompleteIndex = _autoCompleteList.Count - 1;
            var autoCompleteLine = _autoCompleteList[_autoCompleteIndex];
            _previousAutoComplete = autoCompleteLine;
            return autoCompleteLine;
        }

        private bool IsPreviousCycle(string line)
        {
            return _autoCompleteList != null && _autoCompleteList.Count != 0 && _previousAutoComplete == line;
        }
    }
}