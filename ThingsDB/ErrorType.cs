using MessagePack;

namespace ThingsDB
{
    [MessagePackObject]
    internal struct ErrorType
    {
        [Key("error_msg")]
        public string Msg;
        [Key("error_code")]
        public int Code;
    }
}
