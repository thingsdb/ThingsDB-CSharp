using MessagePack;

namespace ThingsDB
{
    [MessagePackObject]
    public struct WarningType
    {
        [Key("warn_msg")]
        public string Msg;
        [Key("warn_code")]
        public int Code;
    }
}
