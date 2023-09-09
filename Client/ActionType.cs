namespace Client;

/// <summary>
///     Type of profiling action
/// </summary>
public enum ActionType
{
    /// <summary>
    ///     Start profiling for a one-shot activity
    /// </summary>
    StartSingle,

    /// <summary>
    ///     End profiling for a one-shot activity
    /// </summary>
    EndSingle,

    /// <summary>
    ///     Start profiling multiple instances of a loop activity
    /// </summary>
    StartMultiple,

    /// <summary>
    ///     ///
    ///     <summary>
    ///         Start profiling multiple instances of a loop activity
    ///     </summary>
    ///     profiling multiple instances of a loop activity
    /// </summary>
    EndMultiple,

    /// <summary>
    ///     Start profiling one instance of a loop activity
    /// </summary>
    StartOne,

    /// <summary>
    ///     End profiling one instance of a loop activity
    /// </summary>
    EndOne
}