namespace Server.Persistence
{


    /// <summary>
    /// Convert raw data into objects and put them in the in-memory datastore
    /// </summary>
    public interface IPersistentObjectProcessor
    {
        void Process(byte[] data);

        void EndProcess();
    }
}