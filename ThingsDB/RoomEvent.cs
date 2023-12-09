using MessagePack;

namespace ThingsDB
{
    public class RoomEvent
    {
        public readonly PackageType Tp;
        public readonly ulong Id;
        public readonly string? Event;
        public readonly byte[][] Args;

        public RoomEvent(PackageType tp, byte[] bytes)
        {
            var reader = new MessagePackReader(bytes);
            int count = reader.ReadMapHeader();
            ulong? roomId = null;
            string? eventName = null;
            Args = Array.Empty<byte[]>();

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
                    int numArguments = reader.ReadArrayHeader();
                    Args = new byte[numArguments][];
                    for (int j = 0; j < numArguments; j++)
                    {
                        long startPos = reader.Consumed;
                        reader.Skip();
                        long size = reader.Consumed - startPos;
                        Args[j] = new byte[size];
                        Array.Copy(bytes, startPos, Args[j], 0, size);
                    }
                }
            }

            if (roomId == null)
            {
                throw new Exception("Failed to unpack emit event");
            }

            Tp = tp;
            Id = (ulong)roomId;
            Event = eventName;
        }
    }
}