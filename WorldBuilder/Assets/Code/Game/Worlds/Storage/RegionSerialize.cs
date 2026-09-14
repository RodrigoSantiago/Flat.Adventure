using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Data;
using K4os.Compression.LZ4;

namespace Game.Worlds.Storage {

    public class RegionSerialize {

        private static readonly int EntryOffset = sizeof(long) * 5 + sizeof(int) * 4;
        private static readonly int HeaderOffset = (sizeof(int) + EntryOffset * Region.TotalChunks) * 2 + 4;
        private static readonly float MaxSpaceWaste = 2.50f;

        private readonly byte[] headerBuffer = new byte[HeaderOffset];
        
        private class RegionHeader {
            public ChunkEntry[] entries;
            public int version;

            public RegionHeader Clone() {
                return new RegionHeader {
                    entries = (ChunkEntry[])entries.Clone(),
                    version = version
                };
            }
        }

        private struct ChunkEntry : IEquatable<ChunkEntry> {
            public long offset;
            public long length;
            public long version;
            public long meshVersion;
            public long soilVersion;
            public int soilDenLength;
            public int soilMatLength;
            public int meshLength;
            public int listLength;

            public static bool operator ==(ChunkEntry a, ChunkEntry b) {
                return a.Equals(b);
            }

            public static bool operator !=(ChunkEntry a, ChunkEntry b) {
                return !a.Equals(b);
            }

            public bool Equals(ChunkEntry other) {
                return offset == other.offset && length == other.length && version == other.version 
                       && meshVersion == other.meshVersion && soilVersion == other.soilVersion 
                       && soilDenLength == other.soilDenLength && soilMatLength == other.soilMatLength 
                       && meshLength == other.meshLength && listLength == other.listLength;
            }

            public override bool Equals(object obj) {
                return obj is ChunkEntry other && Equals(other);
            }

            public override int GetHashCode() {
                var hashCode = new HashCode();
                hashCode.Add(offset);
                hashCode.Add(length);
                hashCode.Add(version);
                hashCode.Add(meshVersion);
                hashCode.Add(soilVersion);
                hashCode.Add(soilDenLength);
                hashCode.Add(soilMatLength);
                hashCode.Add(meshLength);
                hashCode.Add(listLength);
                return hashCode.ToHashCode();
            }
        }

        private readonly IStreamTransfer streamTransfer;

        public RegionSerialize(IStreamTransfer streamTransfer) {
            this.streamTransfer = streamTransfer;
        }

        public RegionSerialize() : this(new FileStreamTransfer()) { }

        public ChunkCacheUpdate[] Load(string filePath, int requiredLods) {
            using var stream = streamTransfer.CreateInput(filePath);
            var header = ReadHeader(stream);
            if (header == null) {
                throw new InvalidDataException("Invalid chunk file");
            }

            return ReadChunkUpdate(stream, header, requiredLods);
        }

        public void Save(string filePath, ChunkCacheUpdate[] updateChunks, bool update) {
            var compressedInputChunks = CompressChunkUpdates(updateChunks);

            using var iStream = update ? streamTransfer.CreateInput(filePath) : null;

            bool temp = false;
            
            var header = iStream == null ? null : ReadHeader(iStream);
            if (header == null) {
                if (update) {
                    throw new InvalidDataException("Failed to update the chunk");
                }
                header = new RegionHeader {
                    entries = new ChunkEntry[Region.TotalChunks],
                    version = 1,
                };
                PackHeader(header, compressedInputChunks);
                
            } else {
                var originalHeader = header?.Clone();
                float waste = AppendHeader(header, compressedInputChunks);
                
                if (waste >= MaxSpaceWaste) {
                    var allChunks = ReadChunkUpdateRaw(iStream, originalHeader, -1);
                    
                    foreach (var chunk in compressedInputChunks) {
                        var chunkUpdate = allChunks[chunk.chunkEntryId];
                        chunkUpdate.version = chunk.version;
                        chunkUpdate.meshVersion = chunk.meshVersion;
                        chunkUpdate.soilVersion = chunk.soilVersion;
                        chunkUpdate.soilDenData = chunk.soilDenData;
                        chunkUpdate.soilMatData = chunk.soilMatData;
                        chunkUpdate.meshData = chunk.meshData;
                        chunkUpdate.listData = chunk.listData;
                    }

                    compressedInputChunks = allChunks;
                    header = originalHeader;
                    
                    PackHeader(header, compressedInputChunks);
                    temp = true;
                }
            }
            
            iStream?.Close();
            
            using var oStream = streamTransfer.CreateOutput(filePath, temp);
                
            foreach (var chunk in compressedInputChunks) {
                var entry = header.entries[chunk.chunkEntryId];
                if (chunk.TotalLength > 0) {
                    oStream.Seek(entry.offset, SeekOrigin.Begin);
                    if (chunk.soilDenData != null) WriteData(oStream, chunk.soilDenData);
                    if (chunk.soilMatData != null) WriteData(oStream, chunk.soilMatData);
                    if (chunk.meshData != null) WriteData(oStream, chunk.meshData);
                    if (chunk.listData != null) WriteData(oStream, chunk.listData);
                }
            }
                
            header.version = temp ? 1 : header.version + 1;
            WriteHeader(oStream, header);
            streamTransfer.Flush(oStream);
            
            if (temp) {
                streamTransfer.UpgradeTempOutput(filePath);
            }
        }

        private float AppendHeader(RegionHeader header, ChunkCacheUpdate[] chunks) {
            var occupied = header.entries
                .Where(e => e.offset > 0 && e.length > 0)
                .OrderBy(e => e.offset).ToList();
            
            foreach (var chunk in chunks) {
                var entry = header.entries[chunk.chunkEntryId];
                
                if (chunk.TotalLength == 0) {
                    entry = new ChunkEntry();
                } else {
                    entry.length = chunk.TotalLength;
                    entry.offset = FindFreeSpace(occupied, chunk.TotalLength);
                    entry.version = chunk.version;
                    entry.meshVersion = chunk.meshVersion;
                    entry.soilVersion = chunk.soilVersion;
                    entry.soilDenLength = chunk.soilDenData?.Length ?? 0;
                    entry.soilMatLength = chunk.soilMatData?.Length ?? 0;
                    entry.meshLength = chunk.meshData?.Length ?? 0;
                    entry.listLength = chunk.listData?.Length ?? 0;
                    occupied.Add(entry);
                    occupied.Sort((a, b) => a.offset.CompareTo(b.offset));
                }

                header.entries[chunk.chunkEntryId] = entry;
            }

            long totalRequired = HeaderOffset;
            long totalUsed = HeaderOffset;
            foreach (var entry in header.entries) {
                totalUsed = Math.Max(totalUsed, entry.offset + entry.length);
                totalRequired += entry.length;
            }

            return totalUsed / (float)totalRequired;
        }

        private void PackHeader(RegionHeader header, ChunkCacheUpdate[] chunks) {
            long lastOffset = HeaderOffset;
            foreach (var chunk in chunks) {
                var entry = header.entries[chunk.chunkEntryId];
                if (chunk.TotalLength == 0) {
                    entry = new ChunkEntry();
                } else {
                    entry.length = chunk.TotalLength;
                    entry.offset = lastOffset;
                    entry.version = chunk.version;
                    entry.meshVersion = chunk.meshVersion;
                    entry.soilVersion = chunk.soilVersion;
                    entry.soilDenLength = chunk.soilDenData?.Length ?? 0;
                    entry.soilMatLength = chunk.soilMatData?.Length ?? 0;
                    entry.meshLength = chunk.meshData?.Length ?? 0;
                    entry.listLength = chunk.listData?.Length ?? 0;

                    lastOffset += entry.length;
                }

                header.entries[chunk.chunkEntryId] = entry;
            }
        }

        private long FindFreeSpace(List<ChunkEntry> occupied, long minLength) {
            long candidateOffset = HeaderOffset;

            foreach (var entry in occupied) {
                long gapLength = entry.offset - candidateOffset;

                if (gapLength >= minLength) {
                    return candidateOffset;
                }

                candidateOffset = entry.offset + entry.length;
            }
            
            return candidateOffset;
        }
        
        private RegionHeader ReadHeader(Stream stream) {
            try {
                if (stream.Length < HeaderOffset) return null;

                stream.Seek(0, SeekOrigin.Begin);
                ReadData(stream, headerBuffer, 0, HeaderOffset);
                
                int cursor = 0;
                int magic = ReadInt(headerBuffer, ref cursor);
                if (magic != 0xC0FE) {
                    return null;
                }

                int cursorA = 4;
                int cursorB = (HeaderOffset - 4) / 2 + 4;

                var headerA = ReadHeaderVersion(stream, ref cursorA);
                var headerB = ReadHeaderVersion(stream, ref cursorB);

                if (headerA == null && headerB == null) return null;
                
                if (headerA == null) return headerB;
                if (headerB == null) return headerA;

                if (!AreHeadersEqual(headerA, headerB) && headerA.version == headerB.version) {
                    return headerB;
                }

                return headerA;
            } catch {
                return null;
            }
        }

        private bool AreHeadersEqual(RegionHeader a, RegionHeader b) {
            if (a.version != b.version) return false;

            for (int i = 0; i < a.entries.Length; i++) {
                if (a.entries[i] == b.entries[i]) {
                    return false;
                }
            }
            return true;
        }

        private RegionHeader ReadHeaderVersion(Stream stream, ref int cursor) {
            var header = new RegionHeader {
                entries = new ChunkEntry[Region.TotalChunks]
            };

            for (int i = 0; i < header.entries.Length; i++) {
                var entry = new ChunkEntry {
                    offset = ReadLong(headerBuffer, ref cursor),
                    length = ReadLong(headerBuffer, ref cursor),
                    version = ReadLong(headerBuffer, ref cursor),
                    meshVersion = ReadLong(headerBuffer, ref cursor),
                    soilVersion = ReadLong(headerBuffer, ref cursor),
                    soilDenLength = ReadInt(headerBuffer, ref cursor),
                    soilMatLength = ReadInt(headerBuffer, ref cursor),
                    meshLength = ReadInt(headerBuffer, ref cursor),
                    listLength = ReadInt(headerBuffer, ref cursor)
                };

                long dataLength = (long)entry.soilDenLength + entry.soilMatLength + 
                                        entry.meshLength + entry.listLength;

                if (entry.offset < 0
                    || entry.length < 0
                    || entry.soilDenLength < 0
                    || entry.soilMatLength < 0
                    || entry.meshLength < 0
                    || entry.listLength < 0
                    || (entry.offset > 0 && entry.offset < HeaderOffset)
                    || (entry.offset == 0 && entry.length > 0)
                    || dataLength > entry.length
                    || (entry.offset > 0 && entry.offset + entry.length > stream.Length)) {
                    return null;
                }

                header.entries[i] = entry;
            }

            header.version = ReadInt(headerBuffer, ref cursor);
            
            return header;
        }

        private void WriteHeader(Stream stream, RegionHeader header) {
            int cursor = 0;
            WriteInt(headerBuffer, ref cursor, 0xC0FE);

            foreach (var entry in header.entries) {
                WriteLong(headerBuffer, ref cursor, entry.offset);
                WriteLong(headerBuffer, ref cursor, entry.length);
                WriteLong(headerBuffer, ref cursor, entry.version);
                WriteLong(headerBuffer, ref cursor, entry.meshVersion);
                WriteLong(headerBuffer, ref cursor, entry.soilVersion);
                WriteInt(headerBuffer, ref cursor, entry.soilDenLength);
                WriteInt(headerBuffer, ref cursor, entry.soilMatLength);
                WriteInt(headerBuffer, ref cursor, entry.meshLength);
                WriteInt(headerBuffer, ref cursor, entry.listLength);
            }

            WriteInt(headerBuffer, ref cursor, header.version);

            stream.Seek(0, SeekOrigin.Begin);
            stream.Write(headerBuffer, 0, 4);
            stream.Write(headerBuffer, 4, (HeaderOffset - 4) / 2);
            streamTransfer.Flush(stream);
            
            stream.Write(headerBuffer, 4, (HeaderOffset - 4) / 2);
            streamTransfer.Flush(stream);
        }

        private int ReadInt(byte[] buffer, ref int offset) {
            int value = buffer[offset]
                        | (buffer[offset + 1] << 8)
                        | (buffer[offset + 2] << 16)
                        | (buffer[offset + 3] << 24);

            offset += sizeof(int);
            return value;
        }

        private void WriteInt(byte[] buffer, ref int offset, int value) {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);

            offset += sizeof(int);
        }
        
        private long ReadLong(byte[] buffer, ref int offset) {
            long value = buffer[offset]
                         | ((long)buffer[offset + 1] << 8)
                         | ((long)buffer[offset + 2] << 16)
                         | ((long)buffer[offset + 3] << 24)
                         | ((long)buffer[offset + 4] << 32)
                         | ((long)buffer[offset + 5] << 40)
                         | ((long)buffer[offset + 6] << 48)
                         | ((long)buffer[offset + 7] << 56);

            offset += sizeof(long);
            return value;
        }

        private void WriteLong(byte[] buffer, ref int offset, long value) {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
            buffer[offset + 4] = (byte)(value >> 32);
            buffer[offset + 5] = (byte)(value >> 40);
            buffer[offset + 6] = (byte)(value >> 48);
            buffer[offset + 7] = (byte)(value >> 56);

            offset += sizeof(long);
        }

        private ChunkCacheUpdate[] ReadChunkUpdate(Stream stream, RegionHeader header, int requiredLods) {
            var caches = new List<ChunkCacheUpdate>(header.entries.Length);
            for (int i = 0; i < header.entries.Length; i++) {
                var entry = header.entries[i];
                int lod = Region.GetLod(i);
                if (!Region.IsRequired(lod, requiredLods) || 
                    entry.version == 0 || 
                    entry.soilDenLength <= 0 || 
                    entry.soilMatLength <= 0) continue;
                
                var cache = new ChunkCacheUpdate(i) {
                    version = entry.version,
                    meshVersion = entry.meshVersion,
                    soilVersion = entry.soilVersion
                };
                
                stream.Seek(entry.offset, SeekOrigin.Begin);

                cache.soilDenData = ReadAndDecompressBuffer(stream, entry.soilDenLength);
                cache.soilMatData = ReadAndDecompressBuffer(stream, entry.soilMatLength);

                if (entry.meshLength > 0) {
                    cache.meshData = ReadAndDecompressBuffer(stream, entry.meshLength);
                }
                
                if (entry.listLength > 0) {
                    cache.listData = ReadAndDecompressBuffer(stream, entry.listLength);
                }

                caches.Add(cache);
            }
            return caches.ToArray();
        }

        private ChunkCacheUpdate[] ReadChunkUpdateRaw(Stream stream, RegionHeader header, int requiredLods) {
            var caches = new List<ChunkCacheUpdate>(header.entries.Length);
            for (int i = 0; i < header.entries.Length; i++) {
                var entry = header.entries[i];
                int lod = Region.GetLod(i);
                if (!Region.IsRequired(lod, requiredLods) || 
                    entry.version == 0 || 
                    entry.soilDenLength <= 0 || 
                    entry.soilMatLength <= 0) continue;
                
                var cache = new ChunkCacheUpdate(i) {
                    version = entry.version,
                    meshVersion = entry.meshVersion,
                    soilVersion = entry.soilVersion
                };
                
                stream.Seek(entry.offset, SeekOrigin.Begin);

                cache.soilDenData = new byte[entry.soilDenLength];
                ReadData(stream, cache.soilDenData, 0, entry.soilDenLength);
                cache.soilMatData = new byte[entry.soilMatLength];
                ReadData(stream, cache.soilMatData, 0, entry.soilMatLength);

                if (entry.meshLength > 0) {
                    cache.meshData = new byte[entry.meshLength];
                    ReadData(stream, cache.meshData, 0, entry.meshLength);
                }
                
                if (entry.listLength > 0) {
                    cache.listData = new byte[entry.listLength];
                    ReadData(stream, cache.listData, 0, entry.listLength);
                }

                caches.Add(cache);
            }
            return caches.ToArray();
        }

        private void WriteData(Stream stream, byte[] data) {
            stream.Write(data, 0, data.Length);
        }

        private void ReadData(Stream stream, byte[] data, int offset, int length) {
            int totalRead = 0;
            while (totalRead < length) {
                int read = stream.Read(
                    data,
                    offset + totalRead,
                    length - totalRead
                );

                if (read == 0) throw new EndOfStreamException();
                totalRead += read;
            }
        }

        private ChunkCacheUpdate[] CompressChunkUpdates(ChunkCacheUpdate[] updates) {
            var compressedList = new ChunkCacheUpdate[updates.Length];

            for (int i = 0; i < updates.Length; i++) {
                var orig = updates[i];
                compressedList[i] = new ChunkCacheUpdate(orig.chunkEntryId) {
                    version = orig.version,
                    meshVersion = orig.meshVersion,
                    soilVersion = orig.soilVersion,
                    soilDenData = CompressBuffer(orig.soilDenData),
                    soilMatData = CompressBuffer(orig.soilMatData),
                    meshData = CompressBuffer(orig.meshData),
                    listData = CompressBuffer(orig.listData)
                };
            }

            return compressedList;
        }

        private byte[] CompressBuffer(byte[] rawData) {
            if (rawData == null || rawData.Length == 0) return null;

            int maxOutputSize = LZ4Codec.MaximumOutputSize(rawData.Length);
            byte[] compressedBuffer = new byte[sizeof(int) + maxOutputSize];

            compressedBuffer[0] = (byte)rawData.Length;
            compressedBuffer[1] = (byte)(rawData.Length >> 8);
            compressedBuffer[2] = (byte)(rawData.Length >> 16);
            compressedBuffer[3] = (byte)(rawData.Length >> 24);

            int encodedLength = LZ4Codec.Encode(
                rawData, 0, rawData.Length,
                compressedBuffer, sizeof(int), maxOutputSize,
                LZ4Level.L00_FAST
            );

            int totalCompressedSize = sizeof(int) + encodedLength;
            Array.Resize(ref compressedBuffer, totalCompressedSize);

            return compressedBuffer;
        }

        private byte[] ReadAndDecompressBuffer(Stream stream, int compressedLength) {
            byte[] compressedData = new byte[compressedLength];
            ReadData(stream, compressedData, 0, compressedLength);

            int originalSize = compressedData[0]
                             | (compressedData[1] << 8)
                             | (compressedData[2] << 16)
                             | (compressedData[3] << 24);

            byte[] decompressedBuffer = new byte[originalSize];

            LZ4Codec.Decode(
                compressedData, sizeof(int), compressedLength - sizeof(int),
                decompressedBuffer, 0, originalSize
            );

            return decompressedBuffer;
        }
    }
}