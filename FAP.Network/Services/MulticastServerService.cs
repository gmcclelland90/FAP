using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;

namespace FAP.Network.Services
{
    public class MulticastServerService : MulticastCommon
    {
        private readonly object sync = new object();
        private Socket broadcastSocket;
        private readonly ILogger<MulticastServerService> logService;

        public MulticastServerService(ILogger<MulticastServerService> logger)
        {
            logService = logger;
        }

        private void ConnectBroadcast()
        {
            broadcastSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            broadcastSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                                            new MulticastOption(broadcastAddress, IPAddress.Any));
            broadcastSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, 1);
            broadcastSocket.Connect(broadcastAddress, broadcastPort);
        }

        public void SendMessage(string msg)
        {
            lock (sync)
            {
                if (null == broadcastSocket)
                    ConnectBroadcast();

                int maxByteCount = Encoding.UTF8.GetMaxByteCount(msg.Length);
                byte[] buffer = ArrayPool<byte>.Shared.Rent(maxByteCount);
                try
                {
                    int bytesWritten = Encoding.UTF8.GetBytes(msg, 0, msg.Length, buffer, 0);
                    broadcastSocket.SendTo(buffer, 0, bytesWritten, SocketFlags.None, broadcastSocket.RemoteEndPoint);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }

        public void Stop()
        {
            if (null != broadcastSocket)
            {
                broadcastSocket.Close();
                broadcastSocket = null;
            }
        }
    }
}