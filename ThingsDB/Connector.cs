using System.Net.Security;
using System.Net.Sockets;

namespace ThingsDB
{
    public class Connector
    {
        private readonly string host;
        private readonly int port;
        private readonly string defaultScope;
        private string[]? auth;
        private string? token;
        private bool autoReconnect;
        private bool closed;
        private Stream? stream;
        private readonly TcpClient client = new();
        private readonly Dictionary<int, TaskCompletionSource<Package>> lookup = new();
        private ushort next_pid;
        private StreamWriter? logStream;

        public Connector(string host) : this(host, 9000, "/thingsdb") { }
        public Connector(string host, int port) : this(host, port, "/thingsdb") { }
        public Connector(string host, string defaultScope) : this(host, 9000, defaultScope) { }
        public Connector(string host, int port, string defaultScope)
        {
            this.host = host;
            this.port = port;
            token = null;
            auth = null;
            this.defaultScope = defaultScope;
            stream = null;
            closed = false;
            autoReconnect = true;
            next_pid = 0;
            _ = ListenAsync();
        }

        public void SetAutoReconnect(bool autoReconnect)
        {
            this.autoReconnect = autoReconnect;
        }

        public bool IsAutoReconnect() { return autoReconnect; }

        public void SetLogStream(StreamWriter? logStream) { 
            this.logStream = logStream; 
        }


        public async Task Connect(string token)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }
            this.token = token;
        }

        public async Task Connect(string username, string password)
        {
            if (username == null)
            {
                throw new ArgumentNullException(nameof(username));
            }
            if (password == null)
            {
                throw new ArgumentNullException(nameof(password));
            }
            auth = new string[2] { username, password };
        }

        public void Close()
        {
            if (!closed && stream != null)
            {
                client.Close();
                closed = true;

            }
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

            if (stream == null)
            {
                await client.ConnectAsync(host, port);
                stream = new SslStream(client.GetStream());
            }

            if (token != null)
            {
                byte[] data = MessagePack.MessagePackSerializer.Serialize(token);
                pkg = new(Package.Type.ReqAuth, GetNextPid(), data);
            }
            else
            {
                byte[] data = MessagePack.MessagePackSerializer.Serialize(auth);
                pkg = new(Package.Type.ReqAuth, GetNextPid(), data);
            }
        }

        private async Task<Package> Write(Package pkg)
        {
            Package result;
            var promise = new TaskCompletionSource<Package>();
            lookup[pkg.Pid()] = promise;

            try
            {
                if (stream != null)
                {
                    stream.Write(pkg.Header(), 0, Package.HeaderSize);
                    stream.Write(pkg.Data(), 0, pkg.Length());
                    stream.Flush();
                }

                result = await promise.Task;
                return result;
            }
            finally
            {
                lookup.Remove(pkg.Pid());
            }
        }

        private async Task Connect()
        {
            await Task.Delay(1);
        }

        private async Task ListenAsync()
        {
            var buffer = new byte[512];
            int offset = 0;
            Package? package = null;

            while (true)
            {
                if (stream == null)
                {
                    await Task.Delay(1);
                    continue;
                }

                int n = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset));
                if (n <= 0)
                {
                    client.Close();
                    stream = null;
                    continue;
                }
                n += offset;
                offset = 0;

                while (offset < n)
                {
                    if (package == null)
                    {
                        if (n - offset < Package.HeaderSize)
                        {
                            offset = n;
                            break;
                        }
                        package = new(buffer, offset);
                        offset += Package.HeaderSize;
                        continue;
                    }

                    offset += package.CopyData(buffer, offset, n - offset);
                    if (package.IsComplete())
                    {
                        var promise = lookup[package.Pid()];
                        if (promise != null)
                        {
                            promise.SetResult(package);
                        }
                        else if (logStream != null)
                        {
                            logStream.WriteLine(string.Format("No promise found for PID {0}", package.Pid()));
                        }
                        package = null;
                    }
                }

            }
        }

    }
}