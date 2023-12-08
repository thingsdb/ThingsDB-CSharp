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
        public delegate int OnEmitHandler(object[] args);

        public abstract void OnInit();
        public abstract void OnJoin();
        public abstract void OnLeave();
        public abstract void OnDelete();
        public abstract void OnEmit(string eventName, object[] args);

        [MessagePackObject]
        private class SingleRoomId
        {
            [Key(0)]
            public ulong Id { get; set; }
        }

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
            foreach (PropertyInfo prop in GetType().GetProperties())
            {                
                foreach (Attribute attribute in prop.GetCustomAttributes(true))
                {
                    if (attribute is Event)
                    {
                        Event ev = (Event)attribute;
                        MethodInfo? methodInfo = prop.GetMethod;
                        if (methodInfo != null)
                        {
                            SetOnEmitHandler(ev.Name, methodInfo.CreateDelegate<OnEmitHandler>());
                        }                        
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
                throw new RoomAlreadyJoined();
            }
            isJoined = true;
            try
            {
                if (roomId == 0)
                {
                    if (code == "")
                    {
                        throw new EmptyCodeAndRoomId();
                    }

                    var result = await conn.Query(scope, code);
                    SingleRoomId singleRoomId = MessagePackSerializer.Deserialize<SingleRoomId>(result);
                    roomId = singleRoomId.Id;
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
                    if (onEmitHandlers.TryGetValue(ev.Event, out var handler))
                    {
                        handler.Invoke(ev.Args);
                    }
                    else
                    {
                        OnEmit(ev.Event, ev.Args);
                    }
                    break;                    
            }
        }
    }
}
