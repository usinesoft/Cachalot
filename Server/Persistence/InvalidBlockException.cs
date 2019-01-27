using System;

namespace Server.Persistence
{
    public class InvalidBlockException:Exception
    {
        public InvalidBlockException(long offset)
        {
            Offset = offset;
        }

        public long Offset { get; private set; }

        public bool HashKo { get; set; }
        public bool BeginMarkerKo { get; set; }
        public bool EndMarkerKo { get; set; }
        public bool IncompleteBlock { get; set; }
    }
}