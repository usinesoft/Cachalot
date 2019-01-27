using System.Collections.Generic;
using Client.Profiling;
using Client.Queries;

namespace Client.Console
{
    /// <summary>
    ///     Precompiled command issued from a command line string
    /// </summary>
    public class CommandBase
    {
        private readonly List<string> _params;

        internal CommandBase()
        {
            _params = new List<string>();
            if (Profiler == null)
                Profiler = new Profiler();
        }

        /// <summary>
        ///     Type of command (SELECT, COUNT, DESC...)
        /// </summary>
        public CommandType CmdType { get; internal set; }

        /// <summary>
        ///     Command parameters (extracted from the command line)
        /// </summary>
        public IList<string> Params => _params;

        /// <summary>
        ///     Command query (for most of data access commands)
        ///     Can be null
        /// </summary>
        public OrQuery Query { get; internal set; }

        /// <summary>
        ///     Was the command line successfully parsed
        /// </summary>
        public bool Success { get; internal set; }

        /// <summary>
        ///     Parsing error (if <see cref="Success" /> == false)
        /// </summary>
        public string ErrorMessage { get; internal set; }

        /// <summary>
        ///     Can it be executed (true if it was successfully parsed)
        /// </summary>
        public bool CanExecute => CmdType != CommandType.Unknown && Success;

        protected static Profiler Profiler { get; set; }

        /// <summary>
        ///     To be overridden in the derived classes
        /// </summary>
        /// <returns></returns>
        public virtual bool TryExecute()
        {
            if (CanExecute)
                return true;
            return false;
        }
    }
}