namespace Client.Profiling
{
    /// <summary>
    ///     Profilig activation and output is controlled by an external component implementing this interface
    /// </summary>
    public interface IProfilerLogger
    {
        /// <summary>
        ///     If false profiling is disabled
        /// </summary>
        bool IsProfilingActive { get; }

        /// <summary>
        ///     The output is already formatted by the profiler but the external component can do extra profiling and filtering
        /// </summary>
        /// <param name="actionName">name of the action as specified by the user</param>
        /// <param name="actionType">action type (can be used for filtering)</param>
        /// <param name="format">proposed format</param>
        /// <param name="parameters">parameters used by the format string</param>
        void Write(string actionName, ActionType actionType, string format, params object[] parameters);
    }
}