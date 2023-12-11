namespace ThingsDB
{
    //
    // Exceptions used by the connector.
    //
    public class StreamIsNullException : Exception { };

    //
    // Package exceptions.
    // These exceptions are raised when we receive data but the package
    // somehow contains illegal data.
    //
    public class PackageException : Exception { }
    public class UnknownType : PackageException { }
    public class InvalidCheckBit : PackageException { }
    public class SizeMismatch : PackageException { }
    public class Overwritten : PackageException { }
    public class InvalidData : PackageException { }

    //
    // Room exceptions.
    //
    public class RoomException : Exception { public RoomException(string msg) : base(msg) { } }
    public class RoomAlreadyJoined : RoomException { public RoomAlreadyJoined(string msg) : base(msg) { } }
    public class EmptyCodeAndRoomId : RoomException { public EmptyCodeAndRoomId(string msg) : base(msg) { } }
    public class RoomNotFound : RoomException { public RoomNotFound(string msg) : base(msg) { } }
    public class InvalidRoomCode : RoomException { public InvalidRoomCode(string msg) : base(msg) { } }

    //
    // ThingsDB response exceptions.
    // These exceptions are raised based on a response from ThingsDB,
    // when the response represents an error. A ThingsDB error response
    // always inclused an `error_msg` and an `error_code`.
    //
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
}
