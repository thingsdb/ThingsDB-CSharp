[![CI](https://github.com/thingsdb/ThingsDB-CSharp/workflows/CI/badge.svg)](https://github.com/thingsdb/ThingsDB-CSharp/actions)
[![Release Version](https://img.shields.io/github/release/thingsdb/ThingsDB-CSharp)](https://github.com/thingsdb/ThingsDB-CSharp/releases)

# C# connector for ThingsDB

---------------------------------------

  * [Installation](#installation)
  * [Quick usage](#quick-usage)
  * [Connector](#connector)
    * [Constructor](#constructor)
    * [Close](#close)
    * [Connect](#connect)
    * [DefaultScope](#defaultscope)
    * [DefaultTimeout](#defaulttimeout)
    * [OnNodeStatus](#onnodestatus)
    * [SetAutoReconnect](#setautoreconnect)
    * [IsAutoReconnect](#is-autoreconnect)
    * [SetLogStream](#setlogstream)
    * [Query](#query)
    * [Run](#run)
  * [Room](#room)
    * [Overrides](#overrides)
      * [OnInit](#oninit)
      * [OnJoin](#onjoin)
      * [OnEmit](#onemit)
      * [OnLeave](#onleave)
      * [OnDelete](#ondelete)
    * [Id](#id)
    * [Scope](#scope)
    * [Join](#join)
    * [NoJoin](#nojoin)
    * [Emit](#emit)
    * [Leave](#leave)

---------------------------------------

## Installation

This library is distributed via NuGet.

```
Install-Package ThingsDB
```

## Quick usage

```csharp
// Create a new connector instance and configure a default scope
Connector conn = new("playground.thingsdb.net", 9400, true)
{
  DefaultScope = "//Doc";
};
// Optionally, configure a stream for logging
conn.SetLogStream(Console.Out);

// Make the connection
await conn.Connect(token);  // You need either a token or a username + password

// Perform a query
var response = await conn.Query(@"
    'Hello world!';
");
// The response is in bytes and can be deserialized using MessagePack.
var msg = MessagePackSerializer.Deserialize<string>(response);

Console.WriteLine(msg);  // Hello world!
```

## Connector

To interact with ThingsDB using this library, you'll always require a
Connector instance. Even when employing a Room to monitor events, you must
first attach the Room to a Connector before it can function.

The Connector is designed to handle asynchronous operations and is not
thread-safe. While we anticipate providing a thread-safe Connector in the
future, for the time being, each thread must be furnished with its own
Connector instance.


### Constructor

```csharp
Connector(string host);
Connector(string host, int port);
Connector(string host, int port, bool useSsl);
```

- *host (string, required)*:
    A hostname, IP address, FQDN to connect to.
- *useSsl (bool)*:
    Enable for creating a secure connection using SSL/TLS.
- *port (int)*:
    TCP port to connect to. The default port is `9200`.

### Close

```csharp
void Close()
```

Closed an open connection. Usually called once when the application is closed.

### Connect

```csharp
async Task Connect(string token);
async Task Connect(string username, string password);
```

Connect using either a token or by username and password.

### DefaultScope

```csharp
string DefaultScope = "/thingsdb";
```

If not set, the scope `/thingsdb` is used as default scope.

### DefaultTimeout

```csharp
TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30.0);
```

If not set, a default timeout of 30 seconds is used.

### OnNodeStatus

```csharp
delegate void OnNodeStatus(NodeStatus nodeStatus) = null;
```

You may configure a callback function to handle `NodeStatus` changes.


### SetAutoReconnect

```csharp
void SetAutoReconnect(bool autoReconnect);
```

By default, auto reconnect is `true`.

### IsAutoReconnect

```csharp
void IsAutoReconnect();
```

Returns `true` if auto reconnect is enabled.

### SetLogStream

```csharp
void SetLogStream(TextWriter? logStream);
```

Configure a log stream. For example:

```csharp
conn.SetLogStream(Console.Out);
```

### Query

```csharp
async Task<byte[]> Query(string code);
async Task<byte[]> Query(string scope, string code);
async Task<byte[]> Query<T>(string code, T? kwargs);
async Task<byte[]> Query<T>(string scope, string code, T? kwargs);
async Task<byte[]> Query<T>(string scope, string code, T? kwargs, TimeSpan timeout);
```

- *scope (string)*:
    If not given, the default scope is used.
- *code (string, required)*:
    The code to query.
- *kwargs (Dictionary<string, T>)*:
    Variable which are used in the code.
- *timeout (int)*:
    If not given, the default timeout is used.

#### Example

```csharp
var kwargs = new Dictionary<string, int> {
  { "a", 6 },
  { "b", 7 }
};
var response = await conn.Query("a * b;", kwargs);
var result = Unpack.Deserialize<int>(response);
// result = 42;
```

### Run

```csharp
async Task<byte[]> Run(string procedure);
async Task<byte[]> Run(string scope, string procedure);
async Task<byte[]> Run<T>(string procedure, T? argsOrKwargs);
async Task<byte[]> Run<T>(string scope, string procedure, T? argsOrKwargs);
async Task<byte[]> Run<T>(string scope, string procedure, T? argsOrKwargs, TimeSpan timeout);
```

- *scope (string)*:
    If not given, the default scope is used.
- *procedure (string, required)*:
    The procedure to execute.
- *argsOrKwargs (Dictionary<string, T> or T[])*:
    Arguments for the procedure may be given either using a dictionary with string keys,
    or positional by supplying the arguments in an array.
- *timeout (int)*:
    If not given, the default timeout is used.

#### Example

```csharp
// This example assumes a procedure "multiply" exists. The procedure can be
// created using the following code:
//
//     new_procedure('multiply', |a, b| a * b);
//
int[] args = [6, 7];
var response = await conn.Run("multiply", args);
var result = Unpack.Deserialize<int>(response);
// result = 42;
```

## Room

The Room class is supposed be subclassed and can be initiated using code to bind the instance to a room in ThingsDB.

```csharp
using ThingsDB;

// The Room constructor needs two arguments:
//   * Connector conn
//       This a ThingsDB Connector instance
//   * string code
//       This is ThingsDB code which will be executed on join and must return
//       the Id for the room to join. For example ".my_room.id();"
//       If you beforehand know the Id, you can simple create a string with the
//       room Id in it. For example: "123".
//  Optionally, you may provide a scope for the room. If not given, the default
//  scope of the collector will be used. For example:
//    Room(conn, "//my_scope", ".my_room.id()")
public class MyRoom(Connector conn) : Room(conn, ".roomd();")
{
    public string? Msg = null;

    [Event("new-message")]
    public void OnNewMessage(byte[][] args)
    {
        Msg = Unpack.Deserialize<string>(args[0]);
    }
}
```

With the **Event** argument you can create event handlers for specific events.
The example above shows how the **Event** argument must be used.

### Overrides

When creating a Room class, some methods exist which may be overwritten.
Each of the methods have their own purpose which will be explained here.

> Note: It is not required to call the base class method for these overrides.

#### OnInit
```csharp
public override void OnInit()
{
    // This method will only be called only once when the Join() function is
    // called. When the connection is lost and the room is re-joined, the method
    // will *not* be called again. Use the OnJoin override if you require this.
}
```

#### OnJoin
```csharp
public override Task OnJoin()
{
    // This is an async method and is the best function to be used if the room
    // requires information from ThingsDB to function. This method will be
    // called on each join. A room may join again after a connection was lost.
    // If this happens, we usually want to make sure our room is synchronized
    // with the latest state of ThingsDB.
}
```

#### OnEmit
```csharp
public override void OnEmit(string eventName, byte[][] args)
{
    // This method will only be called if no event handler exists for when an
    // event is received and accepts two arguments:
    //  * string eventName
    //      Contains the name for the event.
    //  * byte[][] args
    //      Array with arguments. The array may be empty and each argument in
    //      the array is of type bytes and can be deserialized using
    //      MessagePack (or the Unpack.Deserialize method which is exactly the
    //      same).
}
```

#### OnLeave
```csharp
public override void OnLeave()
{
    // This method is called the Leave() method is called. It will *not* be
    // triggered when for example the connection is lost.
}
```

#### OnDelete
```csharp
public override void OnDelete()
{
    // This method will be called when the room is removed. Be aware that it may
    // take an iteration of the garbage collector in ThingsDB before a room is
    // truly deleted.
}
```

### Id
```csharp
ulong Id();
```

Return the Id of the room.

### Scope
```csharp
string Scope();
```

Return the scope of the room.

### Join
```csharp
public async Task Join();
public async Task Join(TimeSpan wait);
```

The Join method must be called to actually join the room. The Join method by default
waits before the first call to the `OnJoin()` method has finished. Thus, you know that
all the initializers in the `OnJoin` override have finished once the `Join` method has
finished. If it takes longer than wait, a TimeoutException is raised. The `wait` argument
may also be set to `0` in which case we disable this behavior and do not wait for
the `OnJoin` method to finish.

### NoJoin
```csharp
public async Task NoJoin();
```

If you do not want to Join the Room, but only use the room to emit events, you
can use the NoJoin method. This room will *not* listen for events and can only be
used for the `Emit(..)` method.

### Emit
```csharp
public async Task Emit(string eventName);
public async Task Emit<T>(string eventName, params T[]? args)
```

Emit an event to the room.

- *eventName (string, required)*:
    The name for the event to emit.
- *args (...T)*:
    Arguments for the event. Multiple argument are allowed.

#### Example

```csharp
await myRoom.Emit("new-message", "This is a test message!");
```

### Leave
```csharp
public async Task Leave();
```

No longer listen to events for this room.
