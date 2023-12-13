using MessagePack;
using System.Reflection;

namespace ThingsDB
{
    [AttributeUsage(AttributeTargets.Method)]
    public class Event(string eventName) : Attribute
    {
        internal string Name { get; set; } = eventName;
    }

    abstract public class Room
    {
        public delegate void OnEmitHandler(byte[][] args);

        virtual public void OnInit() { }
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        virtual public async Task OnJoin() { }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        virtual public void OnLeave() { }
        virtual public void OnDelete() { }
        virtual public void OnEmit(string eventName, byte[][] args) { }
        public readonly Connector Conn;
        private ulong roomId;
        private bool isJoined;
        private readonly string code;
        private readonly string scope;
        private readonly Dictionary<string, OnEmitHandler> onEmitHandlers;
        private TaskCompletionSource<int>? joinPromise;

        [MessagePackObject]
        private struct TestRoomId
        {
            [Key("room_id")]
            public ulong RoomId;
        }

        public Room(Connector conn, string code) : this(conn, conn.DefaultScope, code) { }
        public Room(Connector conn, string scope, string code)
        {
            roomId = 0;
            isJoined = false;
            onEmitHandlers = [];
            joinPromise = null;
            Conn = conn;
            this.code = code;
            this.scope = scope;
            foreach (MethodInfo prop in GetType().GetMethods())
            {
                foreach (Attribute attribute in prop.GetCustomAttributes(true))
                {
                    if (attribute is Event ev)
                    {
                        SetOnEmitHandler(ev.Name, (OnEmitHandler)Delegate.CreateDelegate(typeof(OnEmitHandler), this, prop));
                    }
                }
            }
        }
        public ulong Id() { return roomId; }
        public string Scope() { return scope; }
        public async Task Join() { await Join(TimeSpan.FromSeconds(60.0)); }
        public async Task Join(TimeSpan wait)
        {
            if (wait.TotalSeconds > 0)
            {
                joinPromise = new();
            }

            if (isJoined)
            {
                throw new RoomAlreadyJoined(string.Format("Room {0} already joinded", roomId));
            }
            isJoined = true;
            try
            {
                await GetRoomId();
                ulong[] roomIds = [roomId];
                var response = await Conn.Join(scope, roomIds);
                if (response[0] != roomId)
                {
                    throw new RoomNotFound(string.Format("Room with Id {0} not found", roomId));
                }
                Conn.SetRoom(this);
                OnInit();
                if (joinPromise != null)
                {
                    await Util.TimeoutAfter(joinPromise.Task, wait);
                    joinPromise = null;
                }
            }
            catch (Exception)
            {
                isJoined = false;
                throw;
            }
        }
        public async Task NoJoin()
        {
            await GetRoomId();
            TestRoomId testRoomId = new()
            {
                RoomId = roomId,
            };
            var result = await Conn.Query(scope, @"//ti
                !is_err(try(room(room_id)));
            ", testRoomId);
            var isRoom = MessagePackSerializer.Deserialize<bool>(result);
            if (!isRoom)
            {
                throw new RoomNotFound(string.Format("Room with Id {0} not found", roomId));
            }
        }

        public async Task Leave()
        {
            ulong[] roomIds = [roomId];
            await Conn.Leave(scope, roomIds);
        }
        public async Task Emit(string eventName)
        {
            await Emit<string>(eventName, null);
        }
        public async Task Emit<T>(string eventName, params T[]? args)
        {
            await Conn.Emit(this, eventName, args);
        }
        internal void OnEvent(RoomEvent ev)
        {
            switch (ev.Tp)
            {
                case PackageType.RoomJoin:
                    _ = HandleOnJoin();
                    break;
                case PackageType.RoomLeave:
                    Conn.UnsetRoom(this);
                    OnLeave();
                    break;
                case PackageType.RoomDelete:
                    Conn.UnsetRoom(this);
                    OnDelete();
                    break;
                case PackageType.RoomEmit:
#pragma warning disable CS8604 // Possible null reference argument.
                    if (onEmitHandlers.TryGetValue(ev.Event, out var handler))
                    {
                        handler.Invoke(ev.Args);
                    }
                    else
                    {
                        OnEmit(ev.Event, ev.Args);
                    }
#pragma warning restore CS8604 // Possible null reference argument.
                    break;
            }
        }
        private async Task HandleOnJoin()
        {
            await OnJoin();
            joinPromise?.SetResult(1);
        }
        private async Task GetRoomId()
        {
            if (code == "")
            {
                throw new EmptyCodeAndRoomId("Code is required to find the room Id");
            }

            var result = await Conn.Query(scope, code);
            try
            {
                roomId = MessagePackSerializer.Deserialize<ulong>(result);
            }
            catch (Exception)
            {
                throw new InvalidRoomCode("The result from the given code could not be deserialized as a room Id (type ulong).");
            }
        }
        private void SetOnEmitHandler(string eventName, OnEmitHandler handler)
        {
            onEmitHandlers[eventName] = handler;
        }
    }
}
