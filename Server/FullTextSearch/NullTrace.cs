namespace Server.FullTextSearch
{
    internal class NullTrace : ITrace
    {
        public void Trace(string line)
        {
        }
    }
}