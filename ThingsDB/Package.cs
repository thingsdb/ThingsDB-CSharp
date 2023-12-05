using System.Runtime.InteropServices;

namespace ThingsDB
{
    internal class Package
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Header
        {
            readonly byte tp;
            readonly byte checkBit;
            readonly ushort pid;
            readonly uint length;
        }

        public enum Type : byte
        {
            NodeStatus = 0, // {id: x, status:...}

            Warn = 5,       // {warn_msg:..., warn_code: x}

            RoomJoin = 6,   // {id: x}
            RoomLeave = 7,  // {id: x}
            RoomEmit = 8,   // {id: x, event: ..., args:[...]}
            RoomDelete = 9, // {id: x}

            ResPong = 16,   // Empty
            ResAuth = 17,   // Empty
            ResData = 18,   // ...
            ResError = 19,  // {error_msg: ..., error_code: x}

            ReqPing = 32,   // Empty
            ReqAuth = 33,   // [user, pass] or token            
            ReqQuery = 34,  // [scope, code, {variable}]

            ReqRun = 37,    // [scope, procedure, [[args]/{kw}]
            ReqJoin = 38,   // [scope, ...room ids]
            ReqLeave = 39,  // [scope, ...room ids]
            ReqEmit = 40,   // [scope, room_id, event, ...args]
        }

        public class PackageException : Exception { }
        public class UnknownType : PackageException { }
        public class InvalidCheckBit : PackageException { }
        public class SizeMismatch : PackageException { }

        private readonly byte tp;
        private readonly byte checkBit;
        private readonly ushort pid;
        private readonly uint length;

        private readonly byte[] data;
        private int size;  // size actually written in data; if less than length the package is not complete

        public Package(byte[] header, int offset)
        {
            try
            {
                _ = (Type)header[offset];
            }
            catch (Exception)
            {
                throw new UnknownType();
            }
            tp = (byte)header[offset];
            checkBit = (byte)header[offset + 1];
            pid = (ushort)header[offset + 2];
            length = (uint)header[offset + 4];

            if (tp != ~checkBit)
            {
                throw new InvalidCheckBit();
            }
            size = 0;
            data = new byte[length];
        }

        public Package(Type tp, ushort pid, byte[] data)
        {
            this.tp = (byte)tp;
            checkBit = (byte)~this.tp;
            this.pid = pid;
            length = (uint)data.Length;
            this.data = data;
        }

        public int CopyData(byte[] data, int offset, int length)
        {
            if (size + length > length)
            {
                length = (int)length - size;
            }
            Array.Copy(data, offset, this.data, size, length);
            size += length;
            return length;
        }

        public bool IsComplete()
        {
            return size == length;
        }

        public ushort Pid() { return pid; }
        public Type Tp() { return (Type)tp; }
        public uint Length() { return length; }
    }
}
