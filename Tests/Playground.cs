using MessagePack;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThingsDB;

namespace Tests
{
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8604 // Possible null reference argument.

    [MessagePackObject]
    public struct TestAB
    {
        [Key("a")]
        public int A;
        [Key("b")]
        public int B;
    }

    public class MyRoom(Connector conn) : Room(conn, ".emitter.id();")
    {
        public string? Msg = null;

        [Event("set-message")]
        public void OnSetMessage(byte[][] args)
        {
            Msg = Unpack.Deserialize<string>(args[0]);
        }

        public override async Task OnJoin()
        {
            var response = await Conn.Query(".test_msg;");
            Msg = Unpack.Deserialize<string>(response);
        }
    }

    public class Tests
    {
        private readonly string token = "Fai6NmH7QYxA6WLYPdtgcy";
        private Connector? conn;

        [SetUp]
        public void SetUp()
        {
            conn = new("playground.thingsdb.net", 9400, true)
            {
                DefaultScope = "//Doc"
            };
            conn.SetLogStream(Console.Out);
        }

        [Test]
        public async Task TestConnectAndAuthenticate()
        {
            await conn.Connect(token);
            Assert.Pass("Connect and authenticate success");
        }

        [Test]
        public async Task TestQuery()
        {
            await conn.Connect(token);

            var data = await conn.Query(@"
                1 + 2;
            ");
            Assert.IsNotNull(data);
            var intResult = MessagePackSerializer.Deserialize<int>(data);
            Assert.AreEqual(intResult, 3);

            var args = new TestAB
            {
                A = 3,
                B = 4
            };
            data = await conn.Query(@"
                a + b;
            ", args);
            intResult = MessagePackSerializer.Deserialize<int>(data);
            Assert.AreEqual(intResult, 7);

            var args2 = new Dictionary<string, int> { { "a", 6 }, { "b", 7 } };
            data = await conn.Query("a * b;", args2);
            intResult = Unpack.Deserialize<int>(data);  // Same as MessagePackSerializer.Deserialize
            Assert.AreEqual(intResult, 42);

            data = await conn.Query("nil;");
            Assert.IsTrue(Unpack.IsNil(data));

            Assert.Pass("Query success");
        }

        [Test]
        public async Task TestErrQuery()
        {
            await conn.Connect(token);

            ZeroDivException? expectedException = null;
            try
            {
                _ = await conn.Query("1/0;");
            }
            catch (ZeroDivException ex)
            {
                expectedException = ex;
            }

            Assert.AreEqual("division or modulo by zero", expectedException.Msg);
            Assert.AreEqual(-58, expectedException.Code);

            Assert.Pass("Query with error success");
        }

        [Test]
        public async Task TestRun()
        {
            await conn.Connect(token);
            var kwargs = new TestAB
            {
                A = 6,
                B = 7
            };
            var data = await conn.Run("multiply", kwargs);
            var intResult = MessagePackSerializer.Deserialize<int>(data);
            Assert.AreEqual(intResult, 42);

            int[] args = [4, 5];
            data = await conn.Run("multiply", args);
            intResult = MessagePackSerializer.Deserialize<int>(data);
            Assert.AreEqual(intResult, 20);

            Assert.Pass("Run success");
        }

        [Test]
        public async Task TestRoom()
        {
            var myRoom = new MyRoom(conn);
            await conn.Connect(token);

            Assert.IsNull(myRoom.Msg);
            await myRoom.Join();
            Assert.AreEqual("Used for Connector testing", myRoom.Msg);

            await myRoom.Emit("set-message", "test message");

            // wait for one second so we have enough time to receive the emit
            await Task.Delay(1000);
            Assert.AreEqual("test message", myRoom.Msg);

            Assert.Pass("Room success");
        }

        [TearDown]
        public void TearDown()
        {
            conn?.Close();
        }
    }
#pragma warning restore CS8604 // Possible null reference argument.
#pragma warning restore CS8602 // Dereference of a possibly null reference.
}