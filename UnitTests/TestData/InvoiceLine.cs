namespace Tests.TestData
{
    public class InvoiceLine
    {
        public string ProductId { get; set; }

        public int Quantity { get; set; }

        public decimal UnitaryPrice { get; set; }

        public decimal Price => UnitaryPrice * Quantity;
    }
}