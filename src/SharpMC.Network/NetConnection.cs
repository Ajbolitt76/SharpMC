using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Compression;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.IO;
using Pipelines.Sockets.Unofficial;
using Pipelines.Sockets.Unofficial.Arenas;
using SharpMC.Network.API;
using SharpMC.Network.Core;
using SharpMC.Network.Events;
using SharpMC.Network.Packets;
using SharpMC.Network.Packets.API;
using SharpMC.Network.Util;

namespace SharpMC.Network
{
    public class NetConnection
    {
        private readonly ILogger<NetConnection> Log;
        private CancellationTokenSource CancellationToken { get; }
        private EventHandler<ConnectionConfirmedArgs>? ConnectionConfirmed { get; }
        private Direction Direction { get; }
        private SocketConnection ConnectionPipe { get; }

        private MinecraftStream _readerStream;
        private MinecraftStream _sendStream;

        private object _disconnectSync = false;

        private Channel<Packet> _packetQueue = Channel.CreateBounded<Packet>(
            new BoundedChannelOptions(256)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true
            });

        private readonly RecyclableMemoryStreamManager _manager = new();

        public NetConnection(
            ILogger<NetConnection> log,
            Direction direction,
            SocketConnection connectionPipe,
            EventHandler<ConnectionConfirmedArgs>? confirmedAction = null)
        {
            Log = log;
            Direction = direction;
            ConnectionPipe = connectionPipe;
            RemoteEndPoint = ConnectionPipe.Socket.RemoteEndPoint!;
            ConnectionConfirmed = confirmedAction;
            CancellationToken = new CancellationTokenSource();
            ConnectionState = ConnectionState.Handshake;
            IsConnected = true;
        }


        public EventHandler<PacketReceivedArgs>? OnPacketReceived;
        public EventHandler<ConnectionClosedArgs>? OnConnectionClosed;

        public EndPoint RemoteEndPoint { get; private set; }
        public ConnectionState ConnectionState { get; protected set; }
        public bool CompressionEnabled { get; protected set; }
        protected int CompressionThreshold = 256;

        public bool EncryptionInitiated { get; private set; }
        protected byte[] SharedSecret { get; private set; }

        public bool IsConnected { get; private set; }

        private Task NetworkProcessing { get; set; }
        private Task NetworkWriting { get; set; }

        public void Initialize()
        {
            NetworkProcessing = ProcessNetwork();
            NetworkWriting = ProcessQueue();
        }

        public void Stop()
        {
            if (CancellationToken.IsCancellationRequested)
                return;

            CancellationToken.Cancel();

            if (SocketConnected(ConnectionPipe.Socket))
            {
                Disconnected(true);
            }
            else
            {
                Disconnected(false);
            }
        }

        private void Disconnected(bool notified)
        {
            lock (_disconnectSync)
            {
                if ((bool)_disconnectSync)
                    return;
                _disconnectSync = true;
            }

            if (!CancellationToken.IsCancellationRequested)
            {
                CancellationToken.Cancel();
            }

            ConnectionPipe.Dispose();
            OnConnectionClosed?.Invoke(this, new ConnectionClosedArgs(this, notified));
            IsConnected = false;
        }

        protected void InitEncryption(byte[] sharedKey)
        {
            SharedSecret = sharedKey;
            _readerStream.InitEncryption(SharedSecret, false);
            _sendStream.InitEncryption(SharedSecret, true);
            EncryptionInitiated = true;
        }

        private async Task ProcessNetwork()
        {
            await Task.Yield();
            try
            {
                using var ms = new MinecraftStream(StreamConnection.GetReader(ConnectionPipe.Input));

                _readerStream = ms;
                while (!CancellationToken.IsCancellationRequested)
                {
                    int packetId;
                    byte[] packetData;
                    if (!CompressionEnabled)
                    {
                        var length = ms.ReadVarInt();
                        packetId = ms.ReadVarInt(out var packetIdLength);
                        if (length - packetIdLength > 0)
                        {
                            packetData = ms.Read(length - packetIdLength);
                        }
                        else
                        {
                            packetData = Array.Empty<byte>();
                        }
                    }
                    else
                    {
                        var packetLength = ms.ReadVarInt();
                        var dataLength = ms.ReadVarInt(out var br);
                        if (dataLength == 0)
                        {
                            packetId = ms.ReadVarInt(out var readMore);
                            packetData = ms.Read(packetLength - (br + readMore));
                        }
                        else
                        {
                            var data = ms.Read(packetLength - br);
                            DecompressData(data, out var decompressed);
                            using (var b = new MemoryStream(decompressed))
                            {
                                using (var a = new MinecraftStream(b))
                                {
                                    packetId = a.ReadVarInt(out var l);
                                    packetData = a.Read(dataLength - l);
                                }
                            }
                        }
                    }

                    var packet = MCPacketFactory.GetPacket(ConnectionState, packetId);
                    if (packet == null)
                    {
                        Log.LogWarning($"Unhandled package! 0x{packetId:x2}");
                        continue;
                    }

                    Log.LogInformation($" << Receiving packet 0x{packet.PacketId:x2} ({packet.GetType().Name})");
                    packet.Decode(new MinecraftStream(new MemoryStream(packetData)));
                    HandlePacket(packet);
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning(ex, "OH NO");
                if (ex is OperationCanceledException)
                    return;
                if (ex is EndOfStreamException)
                    return;
                if (ex is IOException)
                    return;
                Log.LogError(ex, "An unhandled exception occurred while processing network!");
            }
            finally
            {
                Disconnected(false);
            }
        }

        protected virtual void HandlePacket(Packet packet)
        {
            var args = new PacketReceivedArgs(packet);
            OnPacketReceived?.BeginInvoke(this, args, PacketReceivedCallback, args);
        }

        private void PacketReceivedCallback(IAsyncResult ar)
        {
            OnPacketReceived?.EndInvoke(ar);
            var args = (PacketReceivedArgs)ar.AsyncState!;
            if (args.IsInvalid)
            {
                Log.LogWarning("Packet reported as invalid!");
            }
        }

        public void SendPacket(Packet packet)
        {
            _packetQueue.Writer.TryWrite(packet);
        }

        private void WritePacketToStream(Packet packet, IMinecraftStream stream)
        {
            if (packet.PacketId == -1 && packet is IToClient toClient)
                packet.PacketId = toClient.ClientId;
            if (packet.PacketId == -1)
                throw new Exception();

            // Log.LogTrace($" >> Sending packet 0x{packet.PacketId:x2} ({packet.GetType().Name})");
            using var rawPacket = _manager.GetStream();
            using var mc = new MinecraftStream(rawPacket);

            mc.WriteVarInt(packet.PacketId);
            packet.Encode(mc);
            var encodedPacket = rawPacket.GetReadOnlySequence();

            using var ms = _manager.GetStream();
            if (CompressionEnabled)
            {
                if (encodedPacket.Length >= CompressionThreshold)
                {
                    WriteCompressPacketData(encodedPacket, stream);
                }
                else
                {
                    // Uncompressed
                    stream.WriteVarInt(0);
                    stream.Write(encodedPacket);
                }
            }
            else
            {
                stream.Write(encodedPacket);
            }
        }

        public void WriteCompressPacketData(ReadOnlySequence<byte> inData, IMinecraftStream stream)
        {
            using var outMemoryStream = _manager.GetStream();
            using (var outZStream = new ZLibStream(outMemoryStream, CompressionLevel.Optimal, true))
            {
                outZStream.WriteSequence(inData);
            }

            var outData = outMemoryStream.GetReadOnlySequence();
            stream.WriteVarInt((int)outData.Length);
            stream.Write(outData);
        }

        private async Task ProcessQueue()
        {
            await Task.Yield();
            await using var mc = new MinecraftStream(StreamConnection.GetWriter(ConnectionPipe.Output));
            _sendStream = mc;

            await using var cacheMemoryStream = new MemoryStream(1024 * 10);
            await using var cache = new MinecraftStream(cacheMemoryStream);

            await foreach (var packet in _packetQueue.Reader.ReadAllAsync())
            {
                try
                {
                    WritePacketToStream(packet, cache);
                    Log.LogInformation($" >> Sending packet 0x{packet.PacketId:x2} ({packet.GetType().Name})");
                    mc.WriteVarInt((int)cacheMemoryStream.Length);
                    mc.Write(cacheMemoryStream.ToArray());
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                cache.Position = 0;
                cache.SetLength(0);
            }
        }

        public static void DecompressData(byte[] inData, out byte[] outData)
        {
            using (var outMemoryStream = new MemoryStream())
            {
                using (var outZStream = new ZLibStream(outMemoryStream, CompressionMode.Decompress, true))
                {
                    outZStream.Write(inData, 0, inData.Length);
                }

                outData = outMemoryStream.ToArray();
            }
        }

        private bool SocketConnected(Socket s)
        {
            var part1 = s.Poll(1000, SelectMode.SelectRead);
            var part2 = s.Available == 0;
            if (part1 && part2)
                return false;
            return true;
        }
    }
}