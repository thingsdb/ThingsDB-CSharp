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
    * [DefaultScope](#default-scope)
    * [DefaultTimeout](#default-timeout)
    * [OnNodeStatus](#on-node-status)
    * [SetAutoReconnect](#set-auto-reconnect)
    * [IsAutoReconnect](#is-autoreconnect)
    * [SetLogStream](#set-log-stream)
    * [Query](#query)
    * [Run](#run)
  * [Room](#room)
    * [methods](#room-methods)
    * [properties](#room-properties)
    * [Join](#join)
    * [Leave](#leave)
    * [emit](#emit)
    * [no_join](#no_join)
  * [Failed packages](#failed-packages)
---------------------------------------

## Installation

This library is distributed via NuGet.

```
Install-Package ThingsDB
```

## Quick usage

```csharp
// Create a new connector instance
Connector conn = new("playground.thingsdb.net", 9400, true)
{
  DefaultScope = "//Doc";
};
// Optionally, configure a stream for logging
conn.SetLogStream(Console.Out);

// Make the connection
await conn.Connect(token);  // You need either a token or a username + password

// Perform a query
var data = await conn.Query(@"
    'Hello world!';
");
// The result is returned in bytes and can be deserialized using MessagePack.
var msg = MessagePackSerializer.Deserialize<string>(data);

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
Connector(string host, int port, bool useSsl)
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

