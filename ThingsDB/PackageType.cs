namespace ThingsDB
{
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
        ReqEmitPeers = 41,   // [scope, room_id, event, ...args]
    }
}
