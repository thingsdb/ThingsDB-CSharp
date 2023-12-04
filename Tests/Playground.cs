using NUnit.Framework;
using System.Threading.Tasks;
using ThingsDB;

namespace Tests
{
    public class Tests
    {
        private Connector? thingsdb;

        [SetUp]
        public void Setup()
        {
            thingsdb = new("https://playground.thingsdb.net", 9400, "demo", "test");
        }

        [Test]
        public async Task TestConnect()
        {
            Assert.Pass();
        }
    }
}