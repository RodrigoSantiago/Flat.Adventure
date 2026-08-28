using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Code.Data;

namespace Code.Worlds.Storage {

    public class RegionSerialize {

        private static readonly int EntryOffset = sizeof(int) * 7;
        private static readonly int HeaderOffset = sizeof(int) + EntryOffset * Region.TotalChunks;
        private static readonly float ExtraPadding = 1.10f;
        private static readonly float MaxSpaceWaste = 1.50f;

        private readonly byte[] headerBuffer = new byte[HeaderOffset];
        
        private class RegionHeader {
            public int magic;
            public ChunkEntry[] entries;

            public RegionHeader Clone() {
                return new RegionHeader {
                    magic = magic,
                    entries = (ChunkEntry[])entries.Clone()
                };
            }
        }

        private struct ChunkEntry {
            public int offset;
            public int length;
            public int version;
            public int soilDenLength;
            public int soilMatLength;
            public int meshLength;
            public int listLength;
        }

        private readonly Func<string, Stream> createInput;
        private readonly Func<string, Stream> createOutput;

        public RegionSerialize(Func<string, Stream> createInput, Func<string, Stream> createOutput) {
            this.createInput = createInput;
            this.createOutput = createOutput;
        }

        public RegionSerialize() : this(InputStream, OutputStream) {
            
        }

        private static Stream InputStream(string path) {
            return new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
        }
        
        private static Stream OutputStream(string path) {
            return new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 64 * 1024,
                options: FileOptions.WriteThrough);
        }

        public ChunkCacheUpdate[] Load(string filePath, int requiredLods) {
            using var stream = createInput(filePath);
            var header = ReadHeader(stream);
            if (header == null) {
                throw new InvalidDataException("Invalid chunk file");
            }

            return ReadChunkUpdate(stream, header, requiredLods);
        }

        public void Save(string filePath, ChunkCacheUpdate[] updateChunks, bool update) {
            using var stream = createOutput(filePath);
            
            var header = ReadHeader(stream);
            if (header == null) {
                if (update) {
                    throw new InvalidDataException("Failed to update the chunk");
                }
                header = new RegionHeader {
                    magic = 0xC0FE,
                    entries = new ChunkEntry[Region.TotalChunks]
                };
            }

            var originalHeader = header.Clone();

            foreach (var chunk in updateChunks) {
                var entry = header.entries[chunk.chunkEntryId];
                if (chunk.TotalLength > entry.length) {
                    entry.offset = 0;
                }
                header.entries[chunk.chunkEntryId] = entry;
            }

            foreach (var chunk in updateChunks) {
                var entry = header.entries[chunk.chunkEntryId];
                entry.version = chunk.version;
                
                if (chunk.TotalLength == 0) {
                    entry.offset = 0;
                    entry.length = 0;
                    entry.soilDenLength = 0;
                    entry.soilMatLength = 0;
                    entry.meshLength = 0;
                    entry.listLength = 0;
                } else {
                    if (chunk.TotalLength > entry.length) {
                        int extraLength = (int)(chunk.TotalLength * ExtraPadding);
                        entry.length = FindFreeSpace(header, chunk.TotalLength, extraLength, out entry.offset);
                    }
                    entry.soilDenLength = chunk.soilDenData?.Length ?? 0;
                    entry.soilMatLength = chunk.soilMatData?.Length ?? 0;
                    entry.meshLength = chunk.meshData?.Length ?? 0;
                    entry.listLength = chunk.listData?.Length ?? 0;
                }

                header.entries[chunk.chunkEntryId] = entry;
            }

            long totalRequired = 0;
            long totalUsed = 0;
            foreach (var entry in header.entries) {
                totalRequired += entry.soilDenLength + entry.soilMatLength + entry.meshLength + entry.listLength;
                totalUsed += entry.length;
            }

            float waste = totalUsed / (float)totalRequired;
            if (waste > MaxSpaceWaste || (totalRequired == 0 && totalUsed > 4096)) {
                var allChunks = ReadChunkUpdate(stream, originalHeader, -1);
                foreach (var chunk in updateChunks) {
                    allChunks[chunk.chunkEntryId].version = chunk.version;
                    allChunks[chunk.chunkEntryId].soilDenData = chunk.soilDenData;
                    allChunks[chunk.chunkEntryId].soilMatData = chunk.soilMatData;
                    allChunks[chunk.chunkEntryId].meshData = chunk.meshData;
                    allChunks[chunk.chunkEntryId].listData = chunk.listData;
                }

                int lastOffset = HeaderOffset;
                foreach (var chunk in allChunks) {
                    var entry = originalHeader.entries[chunk.chunkEntryId];
                    entry.version = chunk.version;
                    entry.length = (int)(chunk.TotalLength * ExtraPadding);
                    if (entry.length == 0) {
                        entry.offset = 0;
                        entry.soilDenLength = 0;
                        entry.soilMatLength = 0;
                        entry.meshLength = 0;
                        entry.listLength = 0;
                    } else {
                        entry.offset = lastOffset;
                        entry.soilDenLength = chunk.soilDenData?.Length ?? 0;
                        entry.soilMatLength = chunk.soilMatData?.Length ?? 0;
                        entry.meshLength = chunk.meshData?.Length ?? 0;
                        entry.listLength = chunk.listData?.Length ?? 0;

                        lastOffset += entry.length;
                    }

                    header.entries[chunk.chunkEntryId] = entry;
                }
            
                WriteDead(stream);
                foreach (var chunk in allChunks) {
                    var entry = header.entries[chunk.chunkEntryId];
                    if (chunk.TotalLength > 0) {
                        stream.Seek(entry.offset, SeekOrigin.Begin);
                        if (chunk.soilDenData != null) WriteData(stream, chunk.soilDenData, 0, chunk.soilDenData.Length);
                        if (chunk.soilMatData != null) WriteData(stream, chunk.soilMatData, 0, chunk.soilMatData.Length);
                        if (chunk.meshData != null) WriteData(stream, chunk.meshData, 0, chunk.meshData.Length);
                        if (chunk.listData != null) WriteData(stream, chunk.listData, 0, chunk.listData.Length);
                    }
                }

            } else {
            
                WriteDead(stream);
                foreach (var chunk in updateChunks) {
                    var entry = header.entries[chunk.chunkEntryId];
                    if (chunk.TotalLength > 0) {
                        stream.Seek(entry.offset, SeekOrigin.Begin);
                        if (chunk.soilDenData != null) WriteData(stream, chunk.soilDenData, 0, chunk.soilDenData.Length);
                        if (chunk.soilMatData != null) WriteData(stream, chunk.soilMatData, 0, chunk.soilMatData.Length);
                        if (chunk.meshData != null) WriteData(stream, chunk.meshData, 0, chunk.meshData.Length);
                        if (chunk.listData != null) WriteData(stream, chunk.listData, 0, chunk.listData.Length);
                    }
                }
            }
            
            WriteHeader(stream, header);

            if (stream is FileStream fileStream) {
                fileStream.Flush(true);
            } else {
                stream.Flush();
            }
        }

        private int FindFreeSpace(RegionHeader header, int minLength, int maxLength, out int offset) {
            var occupied = header.entries
                .Where(e => e.offset > 0 && e.length > 0)
                .OrderBy(e => e.offset);

            int candidateOffset = HeaderOffset;

            foreach (var entry in occupied) {
                int gapLength = entry.offset - candidateOffset;

                if (gapLength >= minLength) {
                    offset = candidateOffset;
                    return Math.Min(maxLength, gapLength);
                }

                candidateOffset = entry.offset + entry.length;
            }
            
            offset = candidateOffset;
            return maxLength;
        }
        
        private RegionHeader ReadHeader(Stream stream) {
            try {
                if (stream.Length < HeaderOffset) {
                    return null;
                }

                stream.Seek(0, SeekOrigin.Begin);
                ReadData(stream, headerBuffer, 0, headerBuffer.Length);

                int cursor = 0;

                int magic = ReadInt(headerBuffer, ref cursor);
                if (magic != 0xC0FE) {
                    return null;
                }

                var header = new RegionHeader {
                    magic = magic,
                    entries = new ChunkEntry[Region.TotalChunks]
                };

                for (int i = 0; i < header.entries.Length; i++) {
                    var entry = new ChunkEntry {
                        offset = ReadInt(headerBuffer, ref cursor),
                        length = ReadInt(headerBuffer, ref cursor),
                        version = ReadInt(headerBuffer, ref cursor),
                        soilDenLength = ReadInt(headerBuffer, ref cursor),
                        soilMatLength = ReadInt(headerBuffer, ref cursor),
                        meshLength = ReadInt(headerBuffer, ref cursor),
                        listLength = ReadInt(headerBuffer, ref cursor)
                    };

                    long dataLength = (long)entry.soilDenLength + entry.soilMatLength + entry.meshLength + entry.listLength;

                    if (entry.offset < 0
                        || entry.length < 0
                        || entry.soilDenLength < 0
                        || entry.soilMatLength < 0
                        || entry.meshLength < 0
                        || entry.listLength < 0
                        || (entry.offset > 0 && entry.offset < HeaderOffset)
                        || (entry.offset == 0 && entry.length > 0)
                        || dataLength > entry.length
                        || (entry.offset > 0 && (long)entry.offset + entry.length > stream.Length)) {
                        return null;
                    }

                    header.entries[i] = entry;
                }

                return header;
            } catch {
                return null;
            }
        }

        private void WriteDead(Stream stream) {
            stream.Seek(0, SeekOrigin.Begin);
            int cursor = 0;
            WriteInt(headerBuffer, ref cursor, 0xDEAD);
        }

        private void WriteHeader(Stream stream, RegionHeader header) {
            int cursor = 0;

            WriteInt(headerBuffer, ref cursor, header.magic);

            foreach (var entry in header.entries) {
                WriteInt(headerBuffer, ref cursor, entry.offset);
                WriteInt(headerBuffer, ref cursor, entry.length);
                WriteInt(headerBuffer, ref cursor, entry.version);
                WriteInt(headerBuffer, ref cursor, entry.soilDenLength);
                WriteInt(headerBuffer, ref cursor, entry.soilMatLength);
                WriteInt(headerBuffer, ref cursor, entry.meshLength);
                WriteInt(headerBuffer, ref cursor, entry.listLength);
            }

            stream.Seek(0, SeekOrigin.Begin);
            stream.Write(headerBuffer, 0, headerBuffer.Length);
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
        
        private ChunkCacheUpdate[] ReadChunkUpdate(Stream stream, RegionHeader header, int requiredLods) {
            var caches = new List<ChunkCacheUpdate>(header.entries.Length);
            for (int i = 0; i < header.entries.Length; i++) {
                var entry = header.entries[i];
                int lod = Region.GetLod(i);
                if (!Region.IsRequired(lod, requiredLods)) continue;
                
                var cache = new ChunkCacheUpdate {
                    chunkEntryId = i,
                    version = entry.version
                };
                
                if (entry.soilDenLength > 0 || 
                    entry.soilMatLength > 0 || 
                    entry.meshLength > 0 ||
                    entry.listLength > 0) {
                    stream.Seek(entry.offset, SeekOrigin.Begin);
                }

                if (entry.soilDenLength > 0) {
                    cache.soilDenData = new byte[entry.soilDenLength];
                    ReadData(stream, cache.soilDenData, 0, cache.soilDenData.Length);
                }

                if (entry.soilMatLength > 0) {
                    cache.soilMatData = new byte[entry.soilMatLength];
                    ReadData(stream, cache.soilMatData, 0, cache.soilMatData.Length);
                }

                if (entry.meshLength > 0) {
                    cache.meshData = new byte[entry.meshLength];
                    ReadData(stream, cache.meshData, 0, cache.meshData.Length);
                }
                
                if (entry.listLength > 0) {
                    cache.listData = new byte[entry.listLength];
                    ReadData(stream, cache.listData, 0, cache.listData.Length);
                }

                caches.Add(cache);
            }
            return caches.ToArray();
        }

        private void WriteData(Stream stream, byte[] data, int offset, int length) {
            stream.Write(data, offset, length);
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
    }
}