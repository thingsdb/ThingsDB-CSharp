using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace ThingsDB
{
    public class Connector
    {
        private readonly string host;
        private readonly int port;
        private readonly string defaultScope;
        private string? username;
        private string? password;
        private string? token;
        private bool autoReconnect;
        private bool closed;
        private Stream? stream;
        private readonly TcpClient client = new();

        public Connector(string host) : this(host, 9000, "/thingsdb") { }
        public Connector(string host, int port) : this(host, port, "/thingsdb") { }
        public Connector(string host, string defaultScope) : this(host, 9000, defaultScope) { }
        public Connector(string host, int port, string defaultScope)
        {
            this.host = host;
            this.port = port;
            token = null;
            username = null;
            password = null;
            this.defaultScope = defaultScope;
            stream = null;
            closed = false;
            autoReconnect = true;
            _ = ListenAsync();
        }

        public void SetAutoReconnect(bool autoReconnect)
        {
            this.autoReconnect = autoReconnect;
        }

        public bool IsAutoReconnect() { return autoReconnect; }



        public async Task Connect(string token)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }
            this.token = token;


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

                pkg = new(Package.Type.ReqAuth, data);
            }

        }

        public void Close()
        {
            if (!closed && stream != null)
            {
                client.Close();
                closed = true;

            }
        }

        private async Task Write(byte[] data)
        {
            if (stream != null)
            {
                stream.Write(data, 0, data.Length);
            }
        }

        private async Task Connect()
        {
            await Task.Delay(1);
        }

        private async Task ListenAsync()
        {
            int headerSize = Marshal.SizeOf<Package.Header>();
            var buffer = new byte[512];
            int offset = 0;
            Package package = null;

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
                        if (n - offset < headerSize)
                        {
                            offset = n;
                            break;
                        }
                        package = new(buffer, offset);
                        offset += headerSize;
                        continue;
                    }

                    offset += package.CopyData(buffer, offset, n - offset);
                    if (package.IsComplete())
                    {
                        package = null;
                    }
                }

            }
        }

    }
}