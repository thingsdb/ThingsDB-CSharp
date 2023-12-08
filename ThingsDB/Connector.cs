using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;

namespace ThingsDB
{
    public class StreamIsNullException : Exception { };
    public class Connector
    {
        public string DefaultScope { get; set; }
        public TimeSpan DefaultTimeout { get; set; }
        private static readonly int maxWaitTimeRecoonect = 120000;  // in milliseconds
        private static readonly int bufferSize = 8192;
        private readonly string host;
        private readonly int port;
        private string[]? auth;
        private string? token;
        private bool autoReconnect;
        private bool isReconnecting;
        private bool closed;
        private Stream? stream;
        private readonly bool useSsl;
        private readonly TcpClient client = new();
        private readonly Dictionary<int, TaskCompletionSource<Package>> lookup = new();
        private ushort next_pid;
        private StreamWriter? logStream;

        public Connector(string host) : this(host, 9000, false) { }
        public Connector(string host, int port) : this(host, port, false) { }
        public Connector(string host, bool useSsl) : this(host, 9000, useSsl) { }
        public Connector(string host, int port, bool useSsl)
        {
            DefaultScope = "/thingsdb";
            DefaultTimeout = TimeSpan.FromSeconds(30.0);

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

        public async Task Connect(string token) { await Connect(token, DefaultTimeout); }
        public async Task Connect(string token, TimeSpan timeout)
        {
            this.token = token ?? throw new ArgumentNullException(nameof(token));
            await ConnectAttempt(timeout);
        }

        public async Task Connect(string username, string password) { await Connect(username, password, DefaultTimeout); }
        public async Task Connect(string username, string password, TimeSpan timeout)
        {
            auth = new string[2] { username, password };
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

        public async Task<byte[]> Query<T>(string code, T? args)
        {
            return await Query(DefaultScope, code, args, DefaultTimeout);
        }

        public async Task<byte[]> Query<T>(string scope, string code, T? args)
        {
            return await Query(scope, code, args, DefaultTimeout);
        }

        public async Task<byte[]> Query<T>(string scope, string code, T? args, TimeSpan timeout)
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
            Package result = await EnsureWrite(pkg, timeout);
            Package.RaiseOnErr(result);
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
            object[] query;
            if (argsOrKwargs == null)
            {
                query = new object[2];
                query[0] = scope;
                query[1] = procedure;
            }
            else
            {
                query = new object[3];
                query[0] = scope;
                query[1] = procedure;
                query[2] = argsOrKwargs;
            }

            byte[] data = MessagePack.MessagePackSerializer.Serialize(query);
            Package pkg = new(PackageType.ReqRun, GetNextPid(), data);
            Package result = await EnsureWrite(pkg, timeout);
            Package.RaiseOnErr(result);
            return result.Data();
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

            Package result = await Write(pkg, timeout);
            Package.RaiseOnErr(result);

            Debug.Assert(result.Tp() == PackageType.ResAuth, "Package type must be ResAuth or an error");
        }

        private static async Task<TResult> TimeoutAfter<TResult>(Task<TResult> task, TimeSpan timeout)
        {

            using (var timeoutCancellationTokenSource = new CancellationTokenSource())
            {

                var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));
                if (completedTask == task)
                {
                    timeoutCancellationTokenSource.Cancel();
                    return await task;
                }
                else
                {
                    throw new TimeoutException("The operation has timed out.");
                }
            }
        }

        private async Task<Package> Write(Package pkg, TimeSpan timeout)
        {
            Package result;
            TaskCompletionSource<Package> promise = new();

            if (lookup.TryGetValue(pkg.Pid(), out TaskCompletionSource<Package>? prev))
            {
                prev.SetException(new Overwritten());
            }

            // Overwrite Pid if it existed
            lookup[pkg.Pid()] = promise;

            try
            {
                if (stream == null)
                {
                    throw new StreamIsNullException();
                }

                await stream.WriteAsync(pkg.GetBytes());

                result = await TimeoutAfter(promise.Task, timeout);
                return result;
            }
            finally
            {
                lookup.Remove(pkg.Pid());
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
                        if (!closed && autoReconnect && (ex is StreamIsNullException || ex is CancelledException))
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
            foreach (TaskCompletionSource<Package> promise in lookup.Values)
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
                CloseClient();
            }
        }
    }
}