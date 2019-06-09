using Client.Interface;

namespace Cachalot.Linq
{
    /// <summary>
    ///     All administration methods
    /// </summary>
    public class DataAdmin
    {
        private readonly ICacheClient _client;

        internal DataAdmin(ICacheClient client)
        {
            _client = client;
        }


        /// <summary>
        ///     Dump all data into a directory
        /// </summary>
        /// <param name="path"></param>
        public void Dump(string path)
        {
            _client.Dump(path);
        }


        /// <summary>
        ///     Delete all data from the database
        /// </summary>
        public void DropDatabase()
        {
            _client.DropDatabase();
        }

        /// <summary>
        ///     This is a fast and safe dump import procedure. It is highly optimized but it can not be used if
        ///     the number of nodes has changed
        /// </summary>
        /// <param name="path"></param>
        public void ImportDump(string path)
        {
            _client.ImportDump(path);
        }

        /// <summary>
        ///     This is slower than ImportDump but it allows to change the number of nodes in the database
        /// </summary>
        /// <param name="path"></param>
        public void InitializeFromDump(string path)
        {
            _client.InitializeFromDump(path);
        }


        /// <summary>
        ///     Switch the database ro rad-only mode
        /// </summary>
        /// <param name="rw"> if true switch back to normal mode (read-write) </param>
        public void ReadOnlyMode(bool rw = false)
        {
            _client.SetReadonlyMode(rw);
        }
    }
}