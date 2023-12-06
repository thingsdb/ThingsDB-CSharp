using NUnit.Framework;
using System.Threading.Tasks;
using ThingsDB;

namespace Tests
{
    public class Tests
    {
        private readonly string token = "Fai6NmH7QYxA6WLYPdtgcy";
        private Connector? thingsdb;

        [SetUp]
        public void SetUp()
        {
            thingsdb = new("playground.thingsdb.net", 9400, "//Doc", true);
        }

        [Test]
        public async Task TestConnectAndAuthenticate()
        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            await thingsdb.Connect(token);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            Assert.Pass("Connect and authenticate success");
        }

        [Test]
        public async Task TestQuery()
        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            await thingsdb.Connect(token);
            var data = await thingsdb.Query("1 + 2;");
            //Assert.IsNotNull(data);

#pragma warning restore CS8602 // Dereference of a possibly null reference.
            Assert.Pass("Query success");
        }

        [TearDown]
        public void TearDown()
        {
            if (thingsdb != null)
            {
                thingsdb.Close();
            }
        }
    }
}