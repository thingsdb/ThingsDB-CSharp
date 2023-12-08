using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThingsDB
{
    [MessagePackObject]
    internal struct RoomEvent
    {
        [IgnoreMember]
        public PackageType Tp { get; set; }

        [Key("id")]
        public ulong Id;
        [Key("event")]
        public string Event;
        [Key("args")]
        public object[] Args;
    }
}
