using Client.Interface;

namespace UnitTests.TestData.MoneyTransfer
{
    public class Account
    {
        [PrimaryKey(KeyDataType.IntKey)] public int Id { get; set; }

        [Index(KeyDataType.IntKey, true)] public decimal Balance { get; set; }
    }
}