using MessagePack;
using NUnit.Framework;
using System.Threading.Tasks;
using ThingsDB;

namespace Tests
{
    [MessagePackObject]
    public struct TestSum
    {
        [Key("a")]
        public int A;
        [Key("b")]
        public int B;
    }

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
            Assert.IsNotNull(data);
            var intResult = MessagePackSerializer.Deserialize<int>(data);
            Assert.AreEqual(intResult, 3);
            var args = new TestSum
            {
                A = 3,
                B = 4
            };
            data = await thingsdb.Query("a + b;", args);
            Assert.IsNotNull(data);
            intResult = MessagePackSerializer.Deserialize<int>(data);
            Assert.AreEqual(intResult, 7);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            Assert.Pass("Query success");
        }

        [Test]
        public async Task TestErrQuery()
        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            await thingsdb.Connect(token);

            ZeroDiv? expectedException = null;
            try
            {
                _ = await thingsdb.Query("1/0;");
            }
            catch (ZeroDiv ex)
            {
                expectedException = ex;
            }

            Assert.AreEqual("division or modulo by zero", expectedException.Msg);
            Assert.AreEqual(-58, expectedException.Code);

#pragma warning restore CS8602 // Dereference of a possibly null reference.
            Assert.Pass("Query with error success");
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