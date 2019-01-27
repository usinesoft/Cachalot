using System;
using System.IO;

namespace Client.Core
{
    /// <summary>
    ///     Logging utility for Console and file
    /// </summary>
    public static class Logger
    {
        private static bool _dumpOn;


        private static StreamWriter _dumpStream;

        public static ICommandLogger CommandLogger { get; set; } = new NullLogger();

        /// <summary>
        ///     Activate/Deasctivate file logging
        /// </summary>
        public static void SwitchDump()
        {
            _dumpOn = !_dumpOn;

            _dumpStream?.Flush();

            if (_dumpOn && _dumpStream == null)
                CommandLogger.Write("Can not activate dump as no file was specified. Use DUMP filename...");
            else
                CommandLogger.Write(_dumpOn ? "DUMP is now activated" : "DUMP is now deactivated");
        }

        /// <summary>
        ///     Set the name of the dump file and activates file logging
        /// </summary>
        /// <param name="fileName"></param>
        public static bool DumpFile(string fileName)
        {
            try
            {
                _dumpStream?.Flush();

                _dumpStream = File.CreateText(fileName);

                _dumpOn = true;

                return true;
            }
            catch (Exception ex)
            {
                CommandLogger.WriteError("Error creating output file:" + ex.Message);
                return false;
            }
        }

        public static void EndDump()
        {
            _dumpStream?.Flush();

            _dumpStream?.Close();

            _dumpStream = null;

            _dumpOn = false;
        }


        /// <summary>
        ///     Write an information message using format string
        ///     Allways write to console. If file logging is active also write to file
        /// </summary>
        /// <param name="format"></param>
        /// <param name="parameters"></param>
        public static void Write(string format, params object[] parameters)
        {
            if (_dumpOn)
            {
                Dbg.CheckThat(_dumpStream != null);
                _dumpStream.WriteLine(format, parameters);
            }
            else
            {
                CommandLogger.Write(format, parameters);
            }
        }

        public static void Write(string message)
        {
            if (_dumpOn)
            {
                Dbg.CheckThat(_dumpStream != null);
                _dumpStream.WriteLine(message);
            }
            else
            {
                CommandLogger.Write(message);
            }
        }

        /// <summary>
        ///     Write an error message using format string
        /// </summary>
        /// <param name="format"></param>
        /// <param name="parameters"></param>
        public static void WriteEror(string format, params object[] parameters)
        {
            CommandLogger.WriteError(format, parameters);
            if (_dumpOn)
            {
                Dbg.CheckThat(_dumpStream != null);
                _dumpStream.WriteLine(format, parameters);
            }
        }

        public static void Flush()
        {
            _dumpStream?.Flush();
        }
    }
}