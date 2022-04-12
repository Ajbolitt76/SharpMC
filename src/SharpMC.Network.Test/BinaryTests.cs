using SharpMC.Data;
using SharpMC.Network.Binary;
using SharpMC.Network.Packets.Play.ToBoth;
using SharpMC.Network.Packets.Play.ToClient;
using Xunit;
using static SharpMC.Network.Test.TestHelper;

namespace SharpMC.Network.Test
{
    public class BinaryTests
    {
        [Fact]
        public void ShouldReadCustomPayload()
        {
            var input = DataBunch.PlayCustomPayload;
            Assert.Equal(25, input.Length);

            var packet = Read<CustomPayload>(input, out var packetId);
            Assert.Equal(0x0a, packetId);

            Assert.Equal(0x0a, packet.ServerId);
            Assert.Equal("minecraft:brand", packet.Channel);
            Assert.Equal(new byte[] {7, 118, 97, 110, 105, 108, 108, 97}, packet.Data);
        }

        [Fact]
        public void ShouldReadPlayLogin()
        {
            var input = DataBunch.PlayLogin;
            Assert.Equal(23992, input.Length);

            var packet = Read<Login>(input, out var packetId);
            WriteTexts(nameof(ShouldReadPlayLogin), ToJson(packet));
            Assert.Equal(0x26, packetId);

            Assert.Equal(0x26, packet.ClientId);
            Assert.Equal(167, packet.EntityId);
            Assert.False(packet.IsHardcore);
            Assert.Equal(0, packet.GameMode);
            Assert.Equal(-1, packet.PreviousGameMode);
            Assert.Equal(new[]
            {
                "minecraft:overworld", "minecraft:the_nether", "minecraft:the_end"
            }, packet.WorldNames);
            Assert.Equal("minecraft:overworld", packet.WorldName);
            Assert.Equal(-2837111331551244922, packet.HashedSeed);
            Assert.Equal(new[] { -660566458, -1901654650 }, packet.HashedSeeds);
            Assert.Equal(0, packet.Dimension.PiglinSafe);
            Assert.Equal(384, packet.Dimension.Height);
            Assert.Equal(0, packet.Dimension.UltraWarm);
            Assert.Equal(0, packet.Dimension.HasCeiling);
            Assert.Equal(-64, packet.Dimension.MinY);
            Assert.Equal(1, packet.Dimension.CoordinateScale);
            Assert.Equal(384, packet.Dimension.LogicalHeight);
            Assert.Equal(1, packet.Dimension.HasRaids);
            Assert.Equal("minecraft:overworld", packet.Dimension.Effects);
            Assert.Equal(1, packet.Dimension.BedWorks);
            Assert.Equal(1, packet.Dimension.HasSkylight);
            Assert.Equal(0, packet.Dimension.RespawnWorks);
            Assert.Equal("#minecraft:infiniburn_overworld", packet.Dimension.InfiniBurn);
            Assert.Equal(0, packet.Dimension.AmbientLight);
            Assert.Equal(1, packet.Dimension.Natural);
            Assert.Equal(20, packet.MaxPlayers);
            Assert.Equal(10, packet.ViewDistance);
            Assert.Equal(10, packet.SimulationDistance);
            Assert.False(packet.ReducedDebugInfo);
            Assert.True(packet.EnableRespawnScreen);
            Assert.False(packet.IsDebug);
            Assert.False(packet.IsFlat);
            Assert.Equal(-1, packet.PacketId);
        }

        [Fact]
        public void ShouldWriteCustomPayload()
        {
            var expected = DataBunch.PlayCustomPayload;
            Assert.Equal(25, expected.Length);

            var packet = new CustomPayload
            {
                Channel = "minecraft:brand",
                Data = new byte[] { 7, 118, 97, 110, 105, 108, 108, 97 }
            };

            var actual = Write(packet, 0x0a);
            WriteBytes(nameof(ShouldWriteCustomPayload), expected, actual);
            Assert.Equal(ToJson(expected), ToJson(actual));
        }

        [Fact]
        public void ShouldWritePlayLogin()
        {
            var expected = DataBunch.PlayLogin;
            Assert.Equal(23992, expected.Length);

            var packet = new Login
            {
                EntityId = 167,
                IsHardcore = false,
                GameMode = 0,
                PreviousGameMode = -1,
                WorldNames = Defaults.WorldNames,
                WorldName = Defaults.WorldName,
                HashedSeeds = new[] { -660566458, -1901654650 },
                MaxPlayers = 20,
                ViewDistance = 10,
                SimulationDistance = 10,
                ReducedDebugInfo = false,
                EnableRespawnScreen = true,
                IsDebug = false,
                IsFlat = false,
                DimensionCodec = new LoginDimCodec
                {
                    Realms = Defaults.Realms, Biomes = Defaults.Biomes
                },
                Dimension = Defaults.CurrentDim
            };

            var actual = Write(packet, 0x26);
            WriteBytes(nameof(ShouldWritePlayLogin), expected, actual);
            Assert.Equal(ToJson(expected), ToJson(actual));
        }
    }
}