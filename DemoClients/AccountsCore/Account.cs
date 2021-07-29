using Client.Core;
using Client.Interface;

namespace Accounts
{
    public class Account
    {

        [ServerSideValue(IndexType.Primary)]
        public int Id { get; set; }

        [ServerSideValue(IndexType.Ordered)]
        public decimal Balance { get; set; }
    }
}