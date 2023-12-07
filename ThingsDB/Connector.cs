using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;

namespace ThingsDB
{
    public class Connector
    {
        private static readonly int bufferSize = 8192;
        private readonly string host;
        private readonly int port;
        private readonly string defaultScope;
        private string[]? auth;
        private string? token;
        private bool autoReconnect;
        private bool closed;
        private Stream? stream;
        private readonly bool useSsl;
        private readonly TcpClient client = new();
        private readonly Dictionary<int, TaskCompletionSource<Package>> lookup = new();
        private ushort next_pid;
        private StreamWriter? logStream;

        public Connector(string host) : this(host, 9000, "/thingsdb", false) { }
        public Connector(string host, int port) : this(host, port, "/thingsdb", false) { }
        public Connector(string host, string defaultScope) : this(host, 9000, defaultScope, false) { }
        public Connector(string host, int port, string defaultScope) : this(host, port, defaultScope, false) { }
        public Connector(string host, bool useSsl) : this(host, 9000, "/thingsdb", useSsl) { }
        public Connector(string host, int port, bool useSsl) : this(host, port, "/thingsdb", useSsl) { }
        public Connector(string host, string defaultScope, bool useSsl) : this(host, 9000, defaultScope, useSsl) { }
        public Connector(string host, int port, string defaultScope, bool useSsl)
        {
            this.host = host;
            this.port = port;
            this.useSsl = useSsl;
            token = null;
            auth = null;
            this.defaultScope = defaultScope;
            stream = null;
            closed = false;
            autoReconnect = true;
            next_pid = 0;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        public void SetAutoReconnect(bool autoReconnect)
        {
            this.autoReconnect = autoReconnect;
        }

        public bool IsAutoReconnect() { return autoReconnect; }

        public void SetLogStream(StreamWriter? logStream)
        {
            this.logStream = logStream;
        }

        public async Task Connect(string token)
        {
            this.token = token ?? throw new ArgumentNullException(nameof(token));
            await ConnectAttempt();
        }

        public async Task Connect(string username, string password)
        {
            auth = new string[2] { username, password };
            await ConnectAttempt();
        }

        public void Close()
        {
            if (!closed && stream != null)
            {
                client.Close();
                closed = true;

            }
        }

        public async Task<byte[]> Query(string code)
        {
            return await Query<string>(defaultScope, code, null);
        }

        public async Task<byte[]> Query(string scope, string code)
        {
            return await Query<string>(scope, code, null);
        }

        public async Task<byte[]> Query<T>(string code, T? args)
        {
            return await Query(defaultScope, code, args);
        }

        public async Task<byte[]> Query<T>(string scope, string code, T? args)
        {
            object[] query;
            if (args == null)
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
                query[2] = args;
            }

            byte[] data = MessagePack.MessagePackSerializer.Serialize(query);
            Package pkg = new(PackageType.ReqQuery, GetNextPid(), data);
            Package result = await Write(pkg);
            Package.RaiseOnErr(result);
            return result.Data();
        }

        private ushort GetNextPid()
        {
            ushort pid = next_pid;
            next_pid++;
            return pid;
        }

        private async Task ConnectAttempt()
        {
            Package pkg;

            if (token != null)
            {
                byte[] data = MessagePack.MessagePackSerializer.Serialize(token);
                pkg = new(PackageType.ReqAuth, GetNextPid(), data);
            }
            else
            {
                byte[] data = MessagePack.MessagePackSerializer.Serialize(auth);
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

            Package result = await Write(pkg);
            Package.RaiseOnErr(result);

            Debug.Assert(result.Tp() == PackageType.ResAuth, "Package type must be ResAuth or an error");
        }

        private async Task<Package> Write(Package pkg)
        {
            Package result;
            TaskCompletionSource<Package> promise = new();

            TaskCompletionSource<Package>? prev;
            if (lookup.TryGetValue(pkg.Pid(), out prev))
            {
                prev.SetException(new Overwritten());
            }

            // Overwrite Pid if it existed
            lookup[pkg.Pid()] = promise;

            try
            {
                if (stream != null)
                {
                    await stream.WriteAsync(pkg.Header().AsMemory(0, Package.HeaderSize));
                    await stream.WriteAsync(pkg.Data().AsMemory(0, pkg.Length()));
                }

                result = await promise.Task;
                return result;
            }
            finally
            {
                lookup.Remove(pkg.Pid());
            }
        }

        private void CloseOnError()
        {
            client.Close();
            stream = null;
            foreach (TaskCompletionSource<Package> promise in lookup.Values)
            {
                promise.SetCanceled();
            }
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
                        throw new InvalidOperationException();
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
                            TaskCompletionSource<Package>? promise;
                            if (lookup.TryGetValue(pkg.Pid(), out promise))
                            {
                                promise.SetResult(pkg);
                            }
                            else if (logStream != null)
                            {
                                logStream.WriteLine(string.Format("No promise found for PID {0}", pkg.Pid()));
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
                if (logStream != null)
                {
                    logStream.WriteLine(ex.ToString());
                }
                CloseOnError();
            }
        }
    }
}