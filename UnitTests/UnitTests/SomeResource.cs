namespace Tests.UnitTests
{
    internal class SomeResource
    {
        public SomeResource()
        {
            _id++;

            Id = _id;
        }

        public bool IsValid { get; set; } = true;

        private static int _id = 0;

        public int Id { get; }
    }
}