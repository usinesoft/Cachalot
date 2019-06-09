namespace Server.Persistence
{
    internal class NullObjectProcessor : IPersistentObjectProcessor
    {
        public void Process(byte[] data)
        {
        }

        public void EndProcess()
        {
        }
    }
}