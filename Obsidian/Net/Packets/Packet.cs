using Obsidian.Util;
using Obsidian.Util.Extensions;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Obsidian.Net.Packets
{
    /// <summary>
    /// https://wiki.vg/Protocol#Packet_format
    /// </summary>
    public class Packet
    {
        public bool Empty => this.data == null || this.data.Length == 0;

        internal byte[] data;

        internal int id;

        public Packet(int packetid) => this.id = packetid;

        public Packet(int packetId, byte[] data)
        {
            this.id = packetId;
            this.data = data;
        }

        public virtual async Task WriteAsync(MinecraftStream stream)
        {
            await stream.Lock.WaitAsync();

            await using var dataStream = new MinecraftStream();
            await this.ComposeAsync(dataStream);

            var packetLength = this.id.GetVarIntLength() + (int)dataStream.Length;

            await stream.WriteVarIntAsync(packetLength);
            await stream.WriteVarIntAsync(id);

            dataStream.Position = 0;
            await dataStream.CopyToAsync(stream);

            stream.Lock.Release();
        }

        public virtual async Task WriteCompressedAsync(MinecraftStream stream, int threshold = 0)
        {
            await stream.Lock.WaitAsync();

            await using var dataStream = new MinecraftStream();
            await this.ComposeAsync(dataStream);

            var dataLength = this.id.GetVarIntLength() + (int)dataStream.Length;
            var useCompression = threshold > 0 && dataLength >= threshold;

            dataStream.Position = 0;

            if (useCompression)
            {
                Console.WriteLine("compressing");
                await using var memoryStream = new MemoryStream();
                await ZLibUtils.CompressAsync(dataStream, memoryStream);

                var packetLength = dataLength + (int)memoryStream.Length;

                await stream.WriteVarIntAsync(packetLength);
                await stream.WriteVarIntAsync(dataLength);
                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(stream);
            }
            else
            {
                Console.WriteLine("Not compressing");
                await stream.WriteVarIntAsync(dataLength);
                await stream.WriteVarIntAsync(0);
                await stream.WriteVarIntAsync(this.id);
                await dataStream.CopyToAsync(stream);
            }

            stream.Lock.Release();
        }

        public virtual async Task ReadAsync(byte[] data = null)
        {
            using var stream = new MinecraftStream(data ?? this.data);
            await PopulateAsync(stream);
        }

        protected virtual Task ComposeAsync(MinecraftStream stream) => Task.CompletedTask;

        protected virtual Task PopulateAsync(MinecraftStream stream) => Task.CompletedTask;
    }
}