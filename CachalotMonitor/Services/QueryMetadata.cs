﻿using CachalotMonitor.Model;

namespace CachalotMonitor.Services;

/// <summary>
///     Metadata to assist the graphical creation of a query for a property in a collection
/// </summary>
public class QueryMetadata
{
    /// <summary>
    ///     Max values that are retrieved for client-side search
    /// </summary>
    public const int MaxValues = 1000;

    public string? CollectionName { get; set; }

    public string? PropertyName { get; set; }

    public bool Found { get; set; }

    public PropertyType PropertyType { get; set; }

    public bool PropertyIsCollection { get; set; }


    /// <summary>
    ///     List of possible values if not more than MaxValues
    /// </summary>
    public string[] PossibleValues { get; set; } = Array.Empty<string>();


    /// <summary>
    ///     Total count of distinct values (useful if more than MaxValues are available)
    /// </summary>
    public int PossibleValuesCount { get; set; }


    public string[] AvailableOperators { get; set; } = Array.Empty<string>();
}