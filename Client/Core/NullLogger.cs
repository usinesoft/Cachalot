namespace Client.Core
{
    public class NullLogger : ICommandLogger
    {
        #region ICommandLogger Members

        public void Write(string message)
        {
        }

        public void Write(string format, params object[] parameters)
        {
        }

        public void WriteError(string message)
        {
        }

        public void WriteError(string format, params object[] parameters)
        {
        }

        #endregion
    }
}