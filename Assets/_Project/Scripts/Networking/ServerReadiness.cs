using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace PersistentWorld.Networking
{
    // Only loopback is exposed here. Caddy exposes /health on Railway's HTTP port.
    public sealed class ServerReadiness : IDisposable
    {
        private readonly TcpListener listener;
        private readonly Thread thread;
        private volatile bool stopped, ready;
        private volatile int players;
        private long heartbeat;
        public ServerReadiness(int port)
        {
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start(8);
            thread = new Thread(Serve) { IsBackground = true, Name = "PWA readiness" };
            thread.Start();
        }
        public void Heartbeat(bool listening, int count)
        {
            players = count; ready = listening;
            Interlocked.Exchange(ref heartbeat, Stopwatch.GetTimestamp());
        }
        private void Serve()
        {
            while (!stopped)
            {
                try
                {
                    using TcpClient client = listener.AcceptTcpClient();
                    client.ReceiveTimeout = client.SendTimeout = 2000;
                    using NetworkStream stream = client.GetStream();
                    using StreamReader reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                    string line = reader.ReadLine();
                    bool healthy = ready && (Stopwatch.GetTimestamp() - Interlocked.Read(ref heartbeat)) / (double)Stopwatch.Frequency < 10;
                    bool path = line != null && line.StartsWith("GET /health HTTP/", StringComparison.Ordinal);
                    string body = path ? "{\"ready\":" + (healthy ? "true" : "false") + ",\"players\":" + players + ",\"protocol\":2}" : "{}";
                    string code = !path ? "404 Not Found" : healthy ? "200 OK" : "503 Service Unavailable";
                    byte[] response = Encoding.UTF8.GetBytes("HTTP/1.1 " + code + "\r\nContent-Type: application/json\r\nCache-Control: no-store\r\nConnection: close\r\nContent-Length: " + Encoding.UTF8.GetByteCount(body) + "\r\n\r\n" + body);
                    stream.Write(response, 0, response.Length);
                }
                catch (IOException) { }
                catch (SocketException) { if (stopped) return; }
                catch (ObjectDisposedException) { return; }
            }
        }
        public void Dispose() { stopped = true; ready = false; listener.Stop(); thread.Join(2500); }
    }
}
