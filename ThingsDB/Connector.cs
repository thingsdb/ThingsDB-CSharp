using MessagePack;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;

namespace ThingsDB
{
    public class Connector
    {
        public string DefaultScope { get; set; }
        public TimeSpan DefaultTimeout { get; set; }
        public OnNodeStatus? OnNodeStatus { get; set; }

        private static readonly int maxWaitTimeRecoonect = 120000;  // in milliseconds
        private static readonly int bufferSize = 8192;
        private static readonly int defaultPort = 9200;
        private readonly string host;
        private readonly int port;
        private string[]? auth;
        private string? token;
        private bool autoReconnect;
        private bool isReconnecting;
        private bool closed;
        private Stream? stream;
        private readonly bool useSsl;
        private readonly TcpClient client;
        private readonly Dictionary<int, TaskCompletionSource<Package>> pkgLookup;
        private readonly Dictionary<ulong, Room> roomLookup;
        private ushort next_pid;
        private TextWriter? logStream;

        public Connector(string host) : this(host, defaultPort, false) { }
        public Connector(string host, int port) : this(host, port, false) { }
        public Connector(string host, bool useSsl) : this(host, defaultPort, useSsl) { }
        public Connector(string host, int port, bool useSsl)
        {
            DefaultScope = "/thingsdb";
            DefaultTimeout = TimeSpan.FromSeconds(30.0);
            OnNodeStatus = null;

            this.host = host;
            this.port = port;
            this.useSsl = useSsl;

            token = null;
            auth = null;
            stream = null;
            closed = false;
            autoReconnect = true;
            next_pid = 0;
            isReconnecting = false;
            pkgLookup = [];
            roomLookup = [];
            client = new();

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        public void SetAutoReconnect(bool autoReconnect)
        {
            this.autoReconnect = autoReconnect;
        }

        public bool IsAutoReconnect() { return autoReconnect; }

        public void SetLogStream(TextWriter? logStream)
        {
            this.logStream = logStream;
        }

        public async Task Connect(string token) { await Connect(token, DefaultTimeout); }
        public async Task Connect(string token, TimeSpan timeout)
        {
            this.token = token ?? throw new ArgumentNullException(nameof(token));
            await ConnectAttempt(timeout);
        }

        public async Task Connect(string username, string password) { await Connect(username, password, DefaultTimeout); }
        public async Task Connect(string username, string password, TimeSpan timeout)
        {
            auth = [username, password];
            await ConnectAttempt(timeout);
        }

        public void Close()
        {
            if (!closed && stream != null)
            {
                closed = true;
                CloseClient();
            }
        }

        public async Task<byte[]> Query(string code)
        {
            return await Query<string>(DefaultScope, code, null, DefaultTimeout);
        }

        public async Task<byte[]> Query(string scope, string code)
        {
            return await Query<string>(scope, code, null, DefaultTimeout);
        }

        public async Task<byte[]> Query<T>(string code, T? kwargs)
        {
            return await Query(DefaultScope, code, kwargs, DefaultTimeout);
        }

        public async Task<byte[]> Query<T>(string scope, string code, T? kwargs)
        {
            return await Query(scope, code, kwargs, DefaultTimeout);
        }

        public async Task<byte[]> Query<T>(string scope, string code, T? kwargs, TimeSpan timeout)
        {
            object[] query;
            if (kwargs == null)
            {
                query = new object[2];
                query[0] = scope;
                query[1] = code;
            }
            else
            {
                query = new object[3];
                query[0] = scope;
                query[1] = code;
                query[2] = kwargs;
            }

            byte[] data = MessagePackSerializer.Serialize(query);
            Package pkg = new(PackageType.ReqQuery, GetNextPid(), data);
            Package result = await EnsureWrite(pkg, timeout);
            result.RaiseOnErr();
            return result.Data();
        }
        public async Task<byte[]> Run(string procedure)
        {
            return await Run<string>(DefaultScope, procedure, null, DefaultTimeout);
        }

        public async Task<byte[]> Run(string scope, string procedure)
        {
            return await Run<string>(scope, procedure, null, DefaultTimeout);
        }

        public async Task<byte[]> Run<T>(string procedure, T? argsOrKwargs)
        {
            return await Run(DefaultScope, procedure, argsOrKwargs, DefaultTimeout);
        }

        public async Task<byte[]> Run<T>(string scope, string procedure, T? argsOrKwargs)
        {
            return await Run(scope, procedure, argsOrKwargs, DefaultTimeout);
        }
        public async Task<byte[]> Run<T>(string scope, string procedure, T? argsOrKwargs, TimeSpan timeout)
        {
            object[] run;
            if (argsOrKwargs == null)
            {
                run = new object[2];
                run[0] = scope;
                run[1] = procedure;
            }
            else
            {
                run = new object[3];
                run[0] = scope;
                run[1] = procedure;
                run[2] = argsOrKwargs;
            }

            byte[] data = MessagePackSerializer.Serialize(run);
            Package pkg = new(PackageType.ReqRun, GetNextPid(), data);
            Package result = await EnsureWrite(pkg, timeout);
            result.RaiseOnErr();
            return result.Data();
        }
        internal async Task Emit<T>(Room room, string eventName, T[]? args)
        {
            int n = args?.Length ?? 0;
            object[] emit = new object[3 + n];
            emit[0] = room.Scope();
            emit[1] = room.Id();
            emit[2] = eventName;
            for (var i = 0; i < n; i++)
            {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8601 // Possible null reference assignment.
                emit[3 + i] = args[i];
#pragma warning restore CS8601 // Possible null reference assignment.
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            }

            byte[] data = MessagePackSerializer.Serialize(emit);
            Package pkg = new(PackageType.ReqEmit, GetNextPid(), data);
            Package result = await EnsureWrite(pkg, DefaultTimeout);
            result.RaiseOnErr();
        }

        internal void SetRoom(Room room)
        {
            roomLookup[room.Id()] = room;
        }
        internal void UnsetRoom(Room room)
        {
            _ = roomLookup.Remove(room.Id());
        }
        internal async Task<ulong?[]> Join(string scope, ulong[] roomIds)
        {
            return await JoinOrLeave(PackageType.ReqJoin, scope, roomIds);
        }
        internal async Task<ulong?[]> Leave(string scope, ulong[] roomIds)
        {
            return await JoinOrLeave(PackageType.ReqLeave, scope, roomIds);
        }
        private async Task<ulong?[]> JoinOrLeave(PackageType tp, string scope, ulong[] roomIds)
        {
            object[] query = new object[1 + roomIds.Length];
            query[0] = scope;
            Array.Copy(roomIds, 0, query, 1, roomIds.Length);

            byte[] data = MessagePackSerializer.Serialize(query);
            Package pkg = new(tp, GetNextPid(), data);
            Package result = await EnsureWrite(pkg, DefaultTimeout);
            result.RaiseOnErr();
            var response = MessagePackSerializer.Deserialize<ulong?[]>(result.Data());
            return response;
        }
        private ushort GetNextPid()
        {
            ushort pid = next_pid;
            next_pid++;
            return pid;
        }
        private async Task ConnectAttempt(TimeSpan timeout)
        {
            Package pkg;

            if (token != null)
            {
                byte[] data = MessagePackSerializer.Serialize(token);
                pkg = new(PackageType.ReqAuth, GetNextPid(), data);
            }
            else
            {
                byte[] data = MessagePackSerializer.Serialize(auth);
                pkg = new(PackageType.ReqAuth, GetNextPid(), data);
            }

            if (stream == null)
            {
                await client.ConnectAsync(host, port);
                if (useSsl)
                {
                    var sslStream = new SslStream(client.GetStream());
                    await sslStream.AuthenticateAsClientAsync(host);
                    stream = sslStream;
                }
                else
                {
                    stream = client.GetStream();
                }
                _ = ListenAsync();
            }

            Package result = await Write(pkg, timeout);
            result.RaiseOnErr();
            try
            {
                var roomMap = GetRoomMap();
                foreach (KeyValuePair<string, List<ulong>> entry in roomMap)
                {
                    await Join(entry.Key, entry.Value.ToArray());
                }
            }
            catch (Exception ex)
            {
                logStream?.WriteLine(ex);
            }
            Debug.Assert(result.Tp() == PackageType.ResAuth, "Package type must be ResAuth or an error");
        }

        private Dictionary<string, List<ulong>> GetRoomMap()
        {
            var roomMap = new Dictionary<string, List<ulong>>();
            foreach (Room room in roomLookup.Values)
            {
                if (roomMap.TryGetValue(room.Scope(), out var roomIds))
                {
                    roomIds.Add(room.Id());
                }
                else
                {
                    roomIds = [room.Id()];
                    roomMap[room.Scope()] = roomIds;
                }
            }
            return roomMap;
        }

        private async Task<Package> Write(Package pkg, TimeSpan timeout)
        {
            Package result;
            TaskCompletionSource<Package> promise = new();

            if (pkgLookup.TryGetValue(pkg.Pid(), out TaskCompletionSource<Package>? prev))
            {
                prev.SetException(new Overwritten());
            }

            // Overwrite Pid if it existed
            pkgLookup[pkg.Pid()] = promise;

            try
            {
                if (stream == null)
                {
                    throw new StreamIsNullException();
                }

                await stream.WriteAsync(pkg.GetBytes());

                result = await Util.TimeoutAfter(promise.Task, timeout);
                return result;
            }
            finally
            {
                pkgLookup.Remove(pkg.Pid());
            }
        }
        private async Task<Package> EnsureWrite(Package pkg, TimeSpan timeout)
        {
            int wait = 250;  // start with 250 milliseconds
            Stopwatch sw = new();
            sw.Start();
            try
            {
                while (true)
                {
                    try
                    {
                        var result = await Write(pkg, timeout);
                        return result;
                    }
                    catch (Exception ex)
                    {
                        if (logStream != null)
                        {
                            logStream.WriteLine(ex.ToString());
                        }
                        if (sw.Elapsed > timeout)
                        {
                            throw new TimeoutException("The query has timed out");
                        }
                        if (!closed && autoReconnect && (
                            ex is StreamIsNullException ||
                            ex is CancelledException ||
                            ex is NodeError))
                        {
                            if (!isReconnecting)
                            {
                                CloseClient();
                            }
                            await Task.Delay(wait);
                            wait *= 2;
                            continue;
                        }
                        else
                            throw;
                    }
                }
            }
            finally
            {
                sw.Stop();
            }
        }
        private void CloseClient()
        {
            client.Close();
            stream = null;
            foreach (TaskCompletionSource<Package> promise in pkgLookup.Values)
            {
                promise.SetCanceled();
            }
            if (!closed && autoReconnect && !isReconnecting)
            {
                isReconnecting = true;
                _ = Reconnect();
            }
        }

        private async Task Reconnect()
        {
            int wait = 1000;  // start with one second
            while (true)
            {
                try
                {
                    await ConnectAttempt(TimeSpan.FromSeconds(10.0));
                }
                catch (Exception ex)
                {
                    if (logStream != null)
                    {
                        logStream.WriteLine(ex.ToString());
                    }
                    await Task.Delay(wait);
                    wait *= 2;
                    if (wait > maxWaitTimeRecoonect)
                    {
                        wait = maxWaitTimeRecoonect;
                    }
                    continue;
                }
                break;  // success
            }
            isReconnecting = false;
        }
        private void HandleRoom(Package pkg)
        {
            try
            {
                RoomEvent roomEvent = new(pkg.Tp(), pkg.Data());

                if (roomLookup.TryGetValue(roomEvent.Id, out Room? room))
                {
                    room.OnEvent(roomEvent);
                }
                else
                {
                    logStream?.WriteLine(string.Format("No promise found for PID {0}", pkg.Pid()));
                }
            }
            catch (Exception ex)
            {
                logStream?.WriteLine(ex.ToString());
            }
        }
        private void HandleWarning(Package pkg)
        {
            if (logStream != null)
            {
                try
                {
                    WarningType warn = MessagePackSerializer.Deserialize<WarningType>(pkg.Data());
                    logStream.WriteLine(string.Format("{0} ({1})", warn.Msg, warn.Code));
                }
                catch (Exception ex)
                {
                    logStream.WriteLine(ex.ToString());
                }
            }
        }
        private void HandleNodeStatus(Package pkg)
        {
            NodeStatus nodeStatus;
            try
            {
                nodeStatus = MessagePackSerializer.Deserialize<NodeStatus>(pkg.Data());
            }
            catch (Exception ex)
            {
                logStream?.WriteLine(ex.ToString());
                return;
            }

            OnNodeStatus?.Invoke(nodeStatus);
            if (nodeStatus.Status == "SHUTTING_DOWN")
            {
                CloseClient();
            }

            logStream?.WriteLine(string.Format("Node {0} has a new status: {1}", nodeStatus.Id, nodeStatus.Status));
        }

        private async Task ListenAsync()
        {
            var buffer = new byte[bufferSize];
            int offset, n = 0;
            Package? pkg = null;

            try
            {
                while (true)
                {
                    if (stream == null)
                    {
                        break;
                    }
                    int numBytesRead = await stream.ReadAsync(buffer.AsMemory(n, bufferSize - n));
                    if (numBytesRead < 0)
                    {
                        throw new SocketException();
                    }
                    n += numBytesRead;
                    while (n > 0)
                    {
                        offset = 0;
                        if (pkg == null)
                        {
                            if (n < Package.HeaderSize)
                            {
                                break;
                            }
                            pkg = new(buffer);
                            offset = Package.HeaderSize;
                        }

                        offset += pkg.CopyData(buffer, offset, n - offset);
                        if (pkg.IsComplete())
                        {
                            switch (pkg.Tp())
                            {
                                case PackageType.NodeStatus:
                                    HandleNodeStatus(pkg);
                                    break;
                                case PackageType.Warn:
                                    HandleWarning(pkg);
                                    break;
                                case PackageType.RoomJoin:
                                case PackageType.RoomLeave:
                                case PackageType.RoomEmit:
                                case PackageType.RoomDelete:
                                    HandleRoom(pkg);
                                    break;
                                default:
                                    if (pkgLookup.TryGetValue(pkg.Pid(), out TaskCompletionSource<Package>? promise))
                                    {
                                        promise.SetResult(pkg);
                                    }
                                    else
                                    {
                                        logStream?.WriteLine(string.Format("No promise found for PID {0}", pkg.Pid()));
                                    }
                                    break;
                            }
                            pkg = null;
                        }

                        n -= offset;
                        if (n > 0)
                        {
                            Array.Copy(buffer, offset, buffer, 0, n);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!closed && logStream != null)
                {
                    logStream.WriteLine(ex.ToString());
                }
                CloseClient();
            }
        }
    }
}