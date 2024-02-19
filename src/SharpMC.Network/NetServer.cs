using System;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pipelines.Sockets.Unofficial;
using SharpMC.Network.API;
using SharpMC.Network.Events;
using ProtocolType = SharpMC.Network.API.ProtocolType;

namespace SharpMC.Network
{
    public class NetServer
    {
        private readonly ILogger<NetServer> Log;

        public NetConnectionFactory NetConnectionFactory { get; }
        internal INetConfiguration Configuration { get; }

        private CancellationTokenSource CancellationToken { get; set; }
        private ConcurrentDictionary<EndPoint, NetConnection> Connections { get; set; }
        private Socket ListenerSocket { get; set; }

        public NetServer(ILogger<NetServer> log, INetConfiguration config,
            NetConnectionFactory factory)
        {
            Log = log;
            Configuration = config;
            NetConnectionFactory = factory;
            SetDefaults();
            Configure();
        }

        private void SetDefaults()
        {
            CancellationToken = new CancellationTokenSource();
            Connections = new ConcurrentDictionary<EndPoint, NetConnection>();
        }

        private void Configure()
        {
            if (Configuration.Protocol == ProtocolType.Tcp)
            {
                ListenerSocket = new Socket(
                    AddressFamily.InterNetwork,
                    SocketType.Stream,
                    System.Net.Sockets.ProtocolType.Tcp);
            }
            else
            {
                throw new NotSupportedException("This protocol is currently not supported!");
            }
        }

        public void Start()
        {
            if (Configuration.Protocol == ProtocolType.Tcp)
            {
                var endpoint = new IPEndPoint(Configuration.Host, Configuration.Port);
                ListenerSocket.Bind(endpoint);
                ListenerSocket.Listen(10);
                _ = AcceptConnections(CancellationToken.Token);
            }
        }

        public void Stop()
        {
            CancellationToken.Cancel();
            foreach (var i in Connections)
            {
                i.Value.Stop();
            }
        }

        private async Task AcceptConnections(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var socket = await ListenerSocket.AcceptAsync(cancellationToken);
                var conn = NetConnectionFactory.CreateConnection(
                    Direction.Client, 
                    SocketConnection.Create(socket));

                if (Connections.TryAdd(socket.RemoteEndPoint!, conn))
                {
                    conn.OnConnectionClosed += (sender, args) =>
                    {
                        if (Connections.TryRemove(args.Connection.RemoteEndPoint, out var nc))
                        {
                            Log.LogInformation($"Client disconnected: {nc.RemoteEndPoint}");
                        }
                    };
                    conn.Initialize();
                }
                else
                {
                    Log.LogWarning("Could not create new active connection!");
                }
            }
        }
        
    }
}