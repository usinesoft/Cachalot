﻿using System.Collections.Generic;
using Client.Core;

namespace Server.FullTextSearch;

public class SearchResult
{
    public KeyValue PrimaryKey { get; set; }

    public double Score { get; set; }

    public IList<LinePointer> LinePointers { get; set; } = new List<LinePointer>();
}