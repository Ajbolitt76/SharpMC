using System;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Pipelines.Sockets.Unofficial;
using SharpMC.Network.API;
using SharpMC.Network.Events;

namespace SharpMC.Network
{
    public class NetConnectionFactory
    {
        public EventHandler<ConnectionCreatedArgs>? OnConnectionCreated;

        private readonly ILoggerFactory _factory;

        public NetConnectionFactory(ILoggerFactory factory)
        {
            _factory = factory;
        }

        internal NetConnection CreateConnection(
            Direction direction,
            SocketConnection socket,
            EventHandler<ConnectionConfirmedArgs>? confirmedAction = null)
        {
            ArgumentNullException.ThrowIfNull(socket);

            var connection = Create(direction, socket, confirmedAction);
            OnConnectionCreated?.Invoke(null, new ConnectionCreatedArgs(connection));
            return connection;
        }

        protected virtual NetConnection Create(
            Direction direction,
            SocketConnection socket,
            EventHandler<ConnectionConfirmedArgs>? confirmedAction = null)
        {
            var log = _factory.CreateLogger<NetConnection>();
            var conn = new NetConnection(log, direction, socket, confirmedAction);
            return conn;
        }
    }
}