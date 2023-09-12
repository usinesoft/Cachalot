

namespace CashalotMonitot.Tests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
            if (File.Exists("user_hashes.txt"))
            {
                File.Delete("user_hashes.txt");
            }
        }

        [Test]
        public void Admin_authentication()
        {
            var authentication = new CachalotMonitor.Services.AuthenticationService();

            // empty code is not accepted
            var result = authentication.CheckAdminCode("");
            Assert.That(result, Is.False);

            // no file present so accept as new code
            result = authentication.CheckAdminCode("abc");
            Assert.That(result, Is.True);

            // invalid code 
            result = authentication.CheckAdminCode("bcd");
            Assert.That(result, Is.False);

            // valid code
            result = authentication.CheckAdminCode("abc");
            Assert.That(result, Is.True);

            // empty code is not accepted
            result = authentication.CheckAdminCode("");
            Assert.That(result, Is.False);

        }
    }
}