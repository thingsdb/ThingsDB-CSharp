using MessagePack;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using ThingsDB;

namespace Tests
{
    [MessagePackObject]
    public struct TestAB
    {
        [Key("a")]
        public int A;
        [Key("b")]
        public int B;
    }

    public class MyRoom : Room
    {
        public string? Msg;

        public MyRoom(Connector conn) : base(conn, ".emitter.id();") 
        {
            Msg = null;
        }

        public override void OnInit() { }
        public override void OnJoin() { }
        public override void OnLeave() { }
        public override void OnDelete() { }
        public override void OnEmit(string _eventName, object[] _args) { }

        [Event("set-message")]
        public void SetMessage(object[] args)
        {
            Msg = (string)args[0];
        }
    }

    public class Tests
    {
        private readonly string token = "Fai6NmH7QYxA6WLYPdtgcy";
        private Connector? conn;

        [SetUp]
        public void SetUp()
        {
            conn = new("playground.thingsdb.net", 9400, true);
            conn.DefaultScope = "//Doc";
            conn.SetLogStream(Console.Out);
        }

        [Test]
        public async Task TestConnectAndAuthenticate()
        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            await conn.Connect(token);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            Assert.Pass("Connect and authenticate success");
        }

        [Test]
        public async Task TestQuery()
        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            await conn.Connect(token);
            var data = await conn.Query("1 + 2;");
            Assert.IsNotNull(data);
            var intResult = MessagePackSerializer.Deserialize<int>(data);
            Assert.AreEqual(intResult, 3);
            var args = new TestAB
            {
                A = 3,
                B = 4
            };
            data = await conn.Query("a + b;", args);
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

#pragma warning restore CS8602 // Dereference of a possibly null reference.
            Assert.Pass("Query with error success");
        }

        [Test]
        public async Task TestRun()
        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            await conn.Connect(token);
            var args = new TestAB
            {
                A = 6,
                B = 7
            };
            var data = await conn.Run("multiply", args);
            Assert.IsNotNull(data);
            var intResult = MessagePackSerializer.Deserialize<int>(data);
            Assert.AreEqual(intResult, 42);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            Assert.Pass("Run success");
        }

        [Test]
        public async Task TestRoom()
        {
#pragma warning disable CS8604 // Possible null reference argument.
            var myRoom = new MyRoom(conn);
            await conn.Connect(token);
            await myRoom.Join();
            await conn.Query(".emitter.emit('set-message', 'test message');");
            
            // wait for one second so we have enough time to receive the emit
            await Task.Delay(1000);
            Assert.AreEqual(myRoom.Msg, "test message");

#pragma warning restore CS8604 // Possible null reference argument.
            Assert.Pass("Room success");
        }

        [TearDown]
        public void TearDown()
        {
            if (conn != null)
            {
                conn.Close();
            }
        }
    }
}