using MessagePack;
using System.Reflection;

namespace ThingsDB
{
    [AttributeUsage(AttributeTargets.Method)]
    public class Event : Attribute
    {
        internal string Name { get; set; }
        public Event(string eventName) { Name = eventName; }
    }

    abstract public class Room
    {
        public delegate void OnEmitHandler(object[] args);

        public abstract void OnInit();
        public abstract void OnJoin();
        public abstract void OnLeave();
        public abstract void OnDelete();
        public abstract void OnEmit(string eventName, object[] args);

        private ulong roomId;
        private bool isJoined;
        private readonly string code;
        private readonly string scope;
        private readonly Connector conn;
        private readonly Dictionary<string, OnEmitHandler> onEmitHandlers;

        public Room(Connector conn, string code) : this(conn, conn.DefaultScope, code) { }
        public Room(Connector conn, string scope, string code)
        {
            roomId = 0;
            isJoined = false;
            onEmitHandlers = new();
            this.conn = conn;
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
        public void SetOnEmitHandler(string eventName, OnEmitHandler handler)
        {
            onEmitHandlers[eventName] = handler;
        }
        public async Task Join()
        {
            if (isJoined)
            {
                throw new RoomAlreadyJoined(string.Format("Room {0} already joinded", roomId));
            }
            isJoined = true;
            try
            {
                if (roomId == 0)
                {
                    if (code == "")
                    {
                        throw new EmptyCodeAndRoomId("Either a roomId or code to find the room Id must be given");
                    }

                    var result = await conn.Query(scope, code);
                    roomId = MessagePackSerializer.Deserialize<ulong>(result);
                }
                ulong[] roomIds = new ulong[1] { roomId };
                var response = await conn.Join(scope, roomIds);
                if (response[0] != roomId)
                {
                    throw new RoomNotFound(string.Format("Room with Id {0} not found", roomId));
                }
                conn.SetRoom(this);
                OnInit();
            }
            catch (Exception)
            {
                isJoined = false;
                throw;
            }
        }


        internal void OnEvent(RoomEvent ev)
        {
            switch (ev.Tp)
            {
                case PackageType.RoomJoin:
                    OnJoin();
                    break;
                case PackageType.RoomLeave:
                    conn.UnsetRoom(this);
                    OnLeave();
                    break;
                case PackageType.RoomDelete:
                    conn.UnsetRoom(this);
                    OnDelete();
                    break;
                case PackageType.RoomEmit:
                    var evEmit = (RoomEventEmit)ev;
#pragma warning disable CS8604 // Possible null reference argument.
                    if (onEmitHandlers.TryGetValue(evEmit.Event, out var handler))
                    {
                        handler.Invoke(evEmit.Args);
                    }
                    else
                    {
                        OnEmit(evEmit.Event, evEmit.Args);
                    }
#pragma warning restore CS8604 // Possible null reference argument.
                    break;                    
            }
        }
    }
}
