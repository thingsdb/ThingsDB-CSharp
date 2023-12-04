using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace ThingsDB
{
    public class Connector
    {
        private readonly string host;
        private readonly int port;
        private readonly string username;
        private readonly string password;
        private readonly string defaultScope;
        private bool autoReconnect;
        private bool closed;
        private Stream? stream;
        private readonly TcpClient client = new();

        public Connector(string host, string username, string password) : this(host, 9000, username, password, "/thingsdb") { }
        public Connector(string host, int port, string username, string password) : this(host, port, username, password, "/thingsdb") { }
        public Connector(string host, string username, string password, string defaultScope) : this(host, 9000, username, password, defaultScope) { }
        public Connector(string host, int port, string username, string password, string defaultScope)
        {
            this.host = host;
            this.port = port;
            this.username = username;
            this.password = password;
            this.defaultScope = defaultScope;
            stream = null;
            closed = false;
            autoReconnect = false;
            _ = ListenAsync();
        }

        public async Task Connect(bool autoReconnect)
        {
            this.autoReconnect = autoReconnect;

            if (stream == null)
            {
                await client.ConnectAsync(host, port);
                stream = new SslStream(client.GetStream());

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
                }
                else if (package == null)
                {
                    if (n < headerSize)
                    {
                        offset = n;
                        continue;
                    }
                    package = new(buffer);
                    package.CopyData(buffer[headerSize: n - headerSize]);
                }
            }
        }

    }
}