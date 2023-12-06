using MessagePack;

namespace ThingsDB
{
    [MessagePackObject]
    public struct ErrorType
    {
        [Key("error_msg")]
        public string Msg;
        [Key("error_code")]
        public int Code;
    }

    internal class Package
    {
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

        static public readonly int HeaderSize = 8;

        public class PackageException : Exception { }
        public class UnknownType : PackageException { }
        public class InvalidCheckBit : PackageException { }
        public class SizeMismatch : PackageException { }
        public class Overwritten : PackageException { }
        public class InvalidData : PackageException { }

        [Serializable]
        public class TiResponseException : Exception
        {
            public readonly string Msg;
            public readonly int Code;

            public TiResponseException(string msg, int code) : base(msg)
            {
                Msg = msg;
                Code = code;                
            }
        }        

        // custom error
        public class TiError : TiResponseException { public TiError(string msg, int code) : base(msg, code) { } }               // ...

        // build-in errors
        public class Cancelled : TiResponseException { public Cancelled(string msg, int code) : base(msg, code) { } }           // -64
        public class Operation : TiResponseException { public Operation(string msg, int code) : base(msg, code) { } }           // -63
        public class NumArguments : TiResponseException { public NumArguments(string msg, int code) : base(msg, code) { } }     // -62
        public class TypeError : TiResponseException { public TypeError(string msg, int code) : base(msg, code) { } }           // -61
        public class ValueError : TiResponseException { public ValueError(string msg, int code) : base(msg, code) { } }         // -60
        public class Overflow : TiResponseException { public Overflow(string msg, int code) : base(msg, code) { } }             // -59
        public class ZeroDiv : TiResponseException { public ZeroDiv(string msg, int code) : base(msg, code) { } }               // -58
        public class MaxQuota : TiResponseException { public MaxQuota(string msg, int code) : base(msg, code) { } }             // -57
        public class AuthError : TiResponseException { public AuthError(string msg, int code) : base(msg, code) { } }           // -56
        public class Forbidden : TiResponseException { public Forbidden(string msg, int code) : base(msg, code) { } }           // -55
        public class LookupError : TiResponseException { public LookupError(string msg, int code) : base(msg, code) { } }       // -54
        public class BadData : TiResponseException { public BadData(string msg, int code) : base(msg, code) { } }               // -53
        public class SyntaxError : TiResponseException { public SyntaxError(string msg, int code) : base(msg, code) { } }       // -52
        public class NodeError : TiResponseException { public NodeError(string msg, int code) : base(msg, code) { } }           // -51
        public class AssertError : TiResponseException { public AssertError(string msg, int code) : base(msg, code) { } }       // -50

        // internal errors
        public class ResultTooLarge : TiResponseException { public ResultTooLarge(string msg, int code) : base(msg, code) { } } // -6
        public class RequestTimeout : TiResponseException { public RequestTimeout(string msg, int code) : base(msg, code) { } } // -5
        public class RequestCancel : TiResponseException { public RequestCancel(string msg, int code) : base(msg, code) { } }   // -4
        public class WriteUV : TiResponseException { public WriteUV(string msg, int code) : base(msg, code) { } }               // -3
        public class Memory : TiResponseException { public Memory(string msg, int code) : base(msg, code) { } }                 // -2
        public class Internal : TiResponseException { public Internal(string msg, int code) : base(msg, code) { } }             // -1
        public class Success : TiResponseException { public Success(string msg, int code) : base(msg, code) { } }               // 0

        private readonly byte tp;
        private readonly byte checkBit;
        private readonly ushort pid;
        private readonly uint length;

        private readonly byte[] data;
        private int size;  // size actually written in data; if less than length the package is not complete

        public Package(byte[] header, int offset)
        {
            tp = header[offset+6];
            checkBit = header[offset + 7];

            if (BitConverter.IsLittleEndian)
            {
                length = header[offset];
                pid = header[offset + 4];
            }
            else
            {
                byte[] tmp = new byte[6];
                Array.Copy(header, offset, tmp, 0, 6);
                Array.Reverse(tmp);
                length = tmp[2];
                pid = tmp[0];
            }

            if (tp != (checkBit ^ 0xff))
            {
                throw new InvalidCheckBit();
            }
            size = 0;
            data = new byte[length];
        }

        public Package(Type tp, ushort pid, byte[] data)
        {
            this.tp = (byte)tp;
            checkBit = (byte)(this.tp ^ 0xff);
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

        public Type Tp() { return (Type)tp; }
        public int Pid() { return pid; }
        public int Length() { return (int)length; }

        public byte[] Data() { return data; }

        public byte[] Header()
        {
            byte[] data = new byte[HeaderSize];
            Pack(data, 0, length);
            Pack(data, 4, pid);
            data[6] = tp;
            data[7] = checkBit;
            return data;
        }

        static private void Pack(byte[] destination, int offset, ushort value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            Array.Copy(bytes, 0, destination, offset, bytes.Length);
        }

        static private void Pack(byte[] destination, int offset, uint value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            Array.Copy(bytes, 0, destination, offset, bytes.Length);
        }

        static public void RaiseOnErr(Package pkg)
        {
            if (pkg.Tp() == Type.ResError)
            {
                ErrorType err = MessagePackSerializer.Deserialize<ErrorType>(pkg.data);
                throw err.Code switch
                {
                    -64 => new Cancelled(err.Msg, err.Code),
                    -63 => new Operation(err.Msg, err.Code),
                    -62 => new NumArguments(err.Msg, err.Code),
                    -61 => new TypeError(err.Msg, err.Code),
                    -60 => new ValueError(err.Msg, err.Code),
                    -59 => new Overflow(err.Msg, err.Code),
                    -58 => new ZeroDiv(err.Msg, err.Code),
                    -57 => new MaxQuota(err.Msg, err.Code),
                    -56 => new AuthError(err.Msg, err.Code),
                    -55 => new Forbidden(err.Msg, err.Code),
                    -54 => new LookupError(err.Msg, err.Code),
                    -53 => new BadData(err.Msg, err.Code),
                    -52 => new SyntaxError(err.Msg, err.Code),
                    -51 => new NodeError(err.Msg, err.Code),
                    -50 => new AssertError(err.Msg, err.Code),
                    -6 => new ResultTooLarge(err.Msg, err.Code),
                    -5 => new RequestTimeout(err.Msg, err.Code),
                    -4 => new RequestCancel(err.Msg, err.Code),
                    -3 => new WriteUV(err.Msg, err.Code),
                    -2 => new Memory(err.Msg, err.Code),
                    -1 => new Internal(err.Msg, err.Code),
                    -0 => new Success(err.Msg, err.Code),
                    _ => new TiError(err.Msg, err.Code),
                };
            }
        }
    }
}
