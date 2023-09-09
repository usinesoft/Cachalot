using System;
using System.Linq;
using Client.Core;
using Client.Interface;

namespace Tests.TestData
{
    [Storage(Layout.Compressed)]
    public class Invoice
    {
        [ServerSideValue(IndexType.Primary)] public string Id { get; set; }

        [FullTextIndexation] public string Address { get; set; }

        [FullTextIndexation] [ServerSideValue] public string ClientName { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public int ClientId { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public DateTime Date { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public DateTime? PaymentDate { get; set; }

        [ServerSideValue(IndexType.Dictionary)]
        public int Year => Date.Year;

        [ServerSideValue(IndexType.Dictionary)]
        public int Month => Date.Month;

        [ServerSideValue(IndexType.Dictionary)]
        public bool IsPayed => PaymentDate != null;

        [ServerSideValue(IndexType.Ordered)] public decimal TotalAmount => Lines.Sum(l => l.Price);

        [ServerSideValue(IndexType.Ordered)] public decimal DiscountPercentage { get; set; }

        [ServerSideValue(IndexType.Ordered)] public decimal AmountToPay => TotalAmount * (1M - DiscountPercentage);


        public InvoiceLine[] Lines { get; set; } = Array.Empty<InvoiceLine>();
    }
}