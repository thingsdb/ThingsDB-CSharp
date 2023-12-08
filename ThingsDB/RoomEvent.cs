using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThingsDB
{
    [MessagePackObject(false)]
    public class RoomEvent
    {
        [IgnoreMember]
        public PackageType Tp { get; set; }

        [Key("id")]
        public ulong Id;
    }

    [MessagePackObject]
    public class RoomEventEmit : RoomEvent
    {
        [Key("event")]
        public string? Event;
        [Key("args")]
        public object[]? Args;
    }
}
