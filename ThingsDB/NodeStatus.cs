using MessagePack;

namespace ThingsDB
{
    public delegate void OnNodeStatus(NodeStatus nodeStatus);

    [MessagePackObject]
    public struct NodeStatus
    {
        [Key("id")]
        public uint Id;
        [Key("status")]
        public string Status;
    }
}
