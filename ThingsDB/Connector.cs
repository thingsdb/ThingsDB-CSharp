using System.Net.Security;
using System.Net.Sockets;

namespace ThingsDB
{
    public class Connector
    {
        private readonly string _host;
        private readonly int _port;
        private Stream? _stream;

        public Connector(string host, int port)
        {
            _host = host;
            _port = port;
            _stream = null;
        }

        public async Task ConnectAsync()
        {
            TcpClient client = new();
            
            await client.ConnectAsync(_host, _port);

            _stream = new SslStream(client.GetStream());
            ListenAsync()
        } 
        
        private async Task ListenAsync()
        {
            var buffer = new byte[8];

            while (true) {
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {

                }
            }
        }

    }
}