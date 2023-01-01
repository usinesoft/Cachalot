#region


// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable UnusedMember.Global

// ReSharper disable FieldCanBeMadeReadOnly.Local
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable NonReadonlyMemberInGetHashCode

#endregion

namespace Client.Core
{
    public enum Layout
    {
        /// <summary>
        /// By default server-side values are stored as KeyValue and the whole object is stored as UTF8 encoded json
        /// </summary>
        Default,
        /// <summary>
        /// Same as default but the json is compressed - efficient for complex documents where only a small subset of 
        /// properties is server-side visible
        /// </summary>
        Compressed,
        /// <summary>
        /// Very efficient storage for objects that have only one level of properties. No json is stored, everything is 
        /// server-side visible
        /// </summary>
        Flat
    }
}