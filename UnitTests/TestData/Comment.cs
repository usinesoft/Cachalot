using System;

namespace Tests.TestData
{
    public class Comment
    {
        public DateTime Date { get; set; }

        public string User { get; set; }

        public string Text { get; set; }


        public override string ToString()
        {
            return Text;
        }
    }
}