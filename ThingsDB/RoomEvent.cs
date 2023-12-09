using MessagePack;
using MessagePack.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ThingsDB
{
    public class RoomEvent : RoomEventEmitFormatter
    {
        public PackageType Tp { get; set; }
        public readonly ulong Id;
        public readonly string? Event;
        private byte[] args;
        private readonly long startPos;
        private readonly long size;

        public RoomEvent(ulong roomId, string? eventName, long startPos, long size)
        {
            Id = roomId;
            Event = eventName;
            args = new byte[size];
            this.startPos = startPos;
            this.size = size;
        }

        public byte[] Args() {  return args; }
        public void SetArgs(byte[] bytes)
        {
            Array.Copy(bytes, startPos, args, 0, size);
        }
    }

    public class RoomEventEmitFormatter : IMessagePackFormatter<RoomEvent>
    {
        public void Serialize(ref MessagePackWriter writer, RoomEvent value, MessagePackSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public RoomEvent Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            options.Security.DepthStep(ref reader);

            int count = reader.ReadMapHeader();
            ulong? roomId = null;
            string? eventName = null;
            long start = 0;
            long size = 0;
           
            for (int i = 0; i < count; i++)
            {
                string? key = reader.ReadString();
                if (key == "id")
                {
                    roomId = reader.ReadUInt64();
                }
                else if (key == "event")
                {
                    eventName = reader.ReadString();
                }
                else if (key == "args")
                {
                    start = reader.Consumed;
                    reader.Skip();
                    size = reader.Consumed - start;
                }
            }
            
            reader.Depth--;

            if (roomId == null)
            {
                throw new Exception("Failed to unpack emit event");
            }
            return new RoomEvent((ulong)roomId, eventName, start, size);
        }
    }
}
