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

    public enum PackageType : byte
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
    public class CancelledException : TiResponseException { public CancelledException(string msg, int code) : base(msg, code) { } }         // -64
    public class OperationException : TiResponseException { public OperationException(string msg, int code) : base(msg, code) { } }         // -63
    public class NumArgumentsException : TiResponseException { public NumArgumentsException(string msg, int code) : base(msg, code) { } }   // -62
    public class TypeError : TiResponseException { public TypeError(string msg, int code) : base(msg, code) { } }                           // -61
    public class ValueError : TiResponseException { public ValueError(string msg, int code) : base(msg, code) { } }                         // -60
    public class OverflowException : TiResponseException { public OverflowException(string msg, int code) : base(msg, code) { } }           // -59
    public class ZeroDivException : TiResponseException { public ZeroDivException(string msg, int code) : base(msg, code) { } }             // -58
    public class MaxQuotaException : TiResponseException { public MaxQuotaException(string msg, int code) : base(msg, code) { } }           // -57
    public class AuthError : TiResponseException { public AuthError(string msg, int code) : base(msg, code) { } }                           // -56
    public class ForbiddenException : TiResponseException { public ForbiddenException(string msg, int code) : base(msg, code) { } }         // -55
    public class LookupError : TiResponseException { public LookupError(string msg, int code) : base(msg, code) { } }                       // -54
    public class BadDataException : TiResponseException { public BadDataException(string msg, int code) : base(msg, code) { } }             // -53
    public class SyntaxError : TiResponseException { public SyntaxError(string msg, int code) : base(msg, code) { } }                       // -52
    public class NodeError : TiResponseException { public NodeError(string msg, int code) : base(msg, code) { } }                           // -51
    public class AssertError : TiResponseException { public AssertError(string msg, int code) : base(msg, code) { } }                       // -50

    // internal errors
    public class ResultTooLarge : TiResponseException { public ResultTooLarge(string msg, int code) : base(msg, code) { } }                 // -6
    public class RequestTimeout : TiResponseException { public RequestTimeout(string msg, int code) : base(msg, code) { } }                 // -5
    public class RequestCancel : TiResponseException { public RequestCancel(string msg, int code) : base(msg, code) { } }                   // -4
    public class WriteUVException : TiResponseException { public WriteUVException(string msg, int code) : base(msg, code) { } }             // -3
    public class MemoryException : TiResponseException { public MemoryException(string msg, int code) : base(msg, code) { } }               // -2
    public class InternalException : TiResponseException { public InternalException(string msg, int code) : base(msg, code) { } }           // -1
    public class SuccessException : TiResponseException { public SuccessException(string msg, int code) : base(msg, code) { } }             // 0

    internal class Package
    {
        static public readonly int HeaderSize = 8;

        private readonly byte tp;
        private readonly byte checkBit;
        private readonly ushort pid;
        private readonly uint length;

        private readonly byte[] data;
        private int size;  // size actually written in data; if less than length the package is not complete

        public Package(byte[] header)
        {
            tp = header[6];
            checkBit = header[7];

            if (BitConverter.IsLittleEndian)
            {
                length = header[0];
                pid = header[4];
            }
            else
            {
                byte[] tmp = new byte[6];
                Array.Copy(header, 0, tmp, 0, 6);
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
        public Package(PackageType tp, ushort pid, byte[] data)
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
        public PackageType Tp() { return (PackageType)tp; }
        public int Pid() { return pid; }
        public int Length() { return (int)length; }
        public int Size() { return HeaderSize + (int)length; }
        public byte[] Data() { return data; }
        public byte[] GetBytes()
        {
            byte[] bytes = new byte[HeaderSize + length];
            Pack(bytes, 0, length);
            Pack(bytes, 4, pid);
            bytes[6] = tp;
            bytes[7] = checkBit;
            Array.Copy(data, 0, bytes, 8, length);
            return bytes;
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
            if (pkg.Tp() == PackageType.ResError)
            {
                ErrorType err = MessagePackSerializer.Deserialize<ErrorType>(pkg.data);
                throw err.Code switch
                {
                    -64 => new CancelledException(err.Msg, err.Code),
                    -63 => new OperationException(err.Msg, err.Code),
                    -62 => new NumArgumentsException(err.Msg, err.Code),
                    -61 => new TypeError(err.Msg, err.Code),
                    -60 => new ValueError(err.Msg, err.Code),
                    -59 => new OverflowException(err.Msg, err.Code),
                    -58 => new ZeroDivException(err.Msg, err.Code),
                    -57 => new MaxQuotaException(err.Msg, err.Code),
                    -56 => new AuthError(err.Msg, err.Code),
                    -55 => new ForbiddenException(err.Msg, err.Code),
                    -54 => new LookupError(err.Msg, err.Code),
                    -53 => new BadDataException(err.Msg, err.Code),
                    -52 => new SyntaxError(err.Msg, err.Code),
                    -51 => new NodeError(err.Msg, err.Code),
                    -50 => new AssertError(err.Msg, err.Code),
                    -6 => new ResultTooLarge(err.Msg, err.Code),
                    -5 => new RequestTimeout(err.Msg, err.Code),
                    -4 => new RequestCancel(err.Msg, err.Code),
                    -3 => new WriteUVException(err.Msg, err.Code),
                    -2 => new MemoryException(err.Msg, err.Code),
                    -1 => new InternalException(err.Msg, err.Code),
                    -0 => new SuccessException(err.Msg, err.Code),
                    _ => new TiError(err.Msg, err.Code),
                };
            }
        }
    }
}
