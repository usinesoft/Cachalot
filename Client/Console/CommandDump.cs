using Client.Core;

namespace Client.Console
{
    /// <summary>
    ///     Dump the output to a text file.
    ///     Usage:
    ///     - dump filename = activates the dump and redirects it to the specified file
    ///     - dump  = switch the dump activation on/off
    /// </summary>
    public class CommandDump : CommandBase
    {
        public override bool TryExecute()
        {
            if (!CanExecute) return false;

            if (Params.Count == 0)
                Logger.SwitchDump();
            else
                Logger.DumpFile(Params[0]);

            return true;
        }
    }
}