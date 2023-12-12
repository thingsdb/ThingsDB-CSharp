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
    public class RoomException(string msg) : Exception(msg) { }
    public class RoomAlreadyJoined(string msg) : RoomException(msg) { }
    public class EmptyCodeAndRoomId(string msg) : RoomException(msg) { }
    public class RoomNotFound(string msg) : RoomException(msg) { }
    public class InvalidRoomCode(string msg) : RoomException(msg) { }

    //
    // ThingsDB response exceptions.
    // These exceptions are raised based on a response from ThingsDB,
    // when the response represents an error. A ThingsDB error response
    // always inclused an `error_msg` and an `error_code`.
    //
    [Serializable]
    public class TiResponseException(string msg, int code) : Exception(msg)
    {
        public readonly string Msg = msg;
        public readonly int Code = code;
    }
    // custom error
    public class TiError(string msg, int code) : TiResponseException(msg, code) { }                 // ...

    // build-in errors
    public class CancelledException(string msg, int code) : TiResponseException(msg, code) { }      // -64
    public class OperationException(string msg, int code) : TiResponseException(msg, code) { }      // -63
    public class NumArgumentsException(string msg, int code) : TiResponseException(msg, code) { }   // -62
    public class TypeError(string msg, int code) : TiResponseException(msg, code) { }               // -61
    public class ValueError(string msg, int code) : TiResponseException(msg, code) { }              // -60
    public class OverflowException(string msg, int code) : TiResponseException(msg, code) { }       // -59
    public class ZeroDivException(string msg, int code) : TiResponseException(msg, code) { }        // -58
    public class MaxQuotaException(string msg, int code) : TiResponseException(msg, code) { }       // -57
    public class AuthError(string msg, int code) : TiResponseException(msg, code) { }               // -56
    public class ForbiddenException(string msg, int code) : TiResponseException(msg, code) { }      // -55
    public class LookupError(string msg, int code) : TiResponseException(msg, code) { }             // -54
    public class BadDataException(string msg, int code) : TiResponseException(msg, code) { }        // -53
    public class SyntaxError(string msg, int code) : TiResponseException(msg, code) { }             // -52
    public class NodeError(string msg, int code) : TiResponseException(msg, code) { }               // -51
    public class AssertError(string msg, int code) : TiResponseException(msg, code) { }             // -50

    // internal errors
    public class ResultTooLarge(string msg, int code) : TiResponseException(msg, code) { }          // -6
    public class RequestTimeout(string msg, int code) : TiResponseException(msg, code) { }          // -5
    public class RequestCancel(string msg, int code) : TiResponseException(msg, code) { }           // -4
    public class WriteUVException(string msg, int code) : TiResponseException(msg, code) { }        // -3
    public class MemoryException(string msg, int code) : TiResponseException(msg, code) { }         // -2
    public class InternalException(string msg, int code) : TiResponseException(msg, code) { }       // -1
    public class SuccessException(string msg, int code) : TiResponseException(msg, code) { }        // 0
}
