using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Game.Data;
using UnityEngine;

namespace Game.GraphicGenerator {
    public class MeshRenderBuffer : IDisposable {
        private sealed class Group {
            public GraphicsBuffer sourceBuffer;
            public readonly List<Command> commands = new();
            public readonly MaterialPropertyBlock props = new();

            public void Reset(GraphicsBuffer sourceBuffer) {
                this.sourceBuffer = sourceBuffer;
                commands.Clear();
            }
            
        }

        private struct Command {
            public ChunkPos posLod;
            public int elementsSize;

            public Command(ChunkPos posLod, int elementsSize) {
                this.posLod = posLod;
                this.elementsSize = elementsSize;
            }
        }
        
        [StructLayout(LayoutKind.Sequential)]
        struct ChunkPos {
            public uint xy;
            public uint z;
            public uint lod;
            public uint offset;
        }
        
        private readonly Stack<Group> pool = new(256);
        
        private readonly Dictionary<GraphicsBuffer, Group> groups = new();
        private int totalCommands;

        // CPU data.
        private ChunkPos[] posLodData = Array.Empty<ChunkPos>();
        private GraphicsBuffer.IndirectDrawArgs[] argsData = Array.Empty<GraphicsBuffer.IndirectDrawArgs>();

        // GPU data.
        private GraphicsBuffer posLodBuffer;
        private GraphicsBuffer argsBuffer;

        private int posLodCapacity;
        private int argsCapacity;

        private RenderParams renderParams;
        private static readonly int ChunkPosBuffer = Shader.PropertyToID("ChunkPosBuffer");
        private static readonly int MeshVertexBuffer = Shader.PropertyToID("MeshVertexBuffer");
        
        public RenderParams Params => renderParams;

        public void Begin(RenderParams renderParams) {
            this.renderParams = renderParams;
            foreach (var group in groups.Values) {
                pool.Push(group);
            }
            groups.Clear();
            totalCommands = 0;
        }

        public void Add(IndexPos pos, int lod, GraphicsBuffer buffer, int elementsOffset, int elementsSize) {
            if (!groups.TryGetValue(buffer, out var group)) {
                if (!pool.TryPop(out group)) {
                    group = new Group(); 
                }
                group.Reset(buffer);
                groups.Add(buffer, group);
            }

            var packed = new ChunkPos {
               xy = (uint)(pos.x | (pos.y << 16)),
               z = (uint)pos.z,
               lod = (uint)(1 << lod),
               offset = (uint)elementsOffset
            };
            group.commands.Add(new Command(packed, elementsSize));
            totalCommands++;
        }
        
        public void Commit() {
            if (totalCommands == 0) return;
            
            EnsureArgsCapacity(totalCommands);
            EnsurePosLodCapacity(totalCommands);

            int index = 0;
            foreach (Group group in groups.Values) {
                foreach (var command in group.commands) {
                    argsData[index] = new GraphicsBuffer.IndirectDrawArgs {
                        startVertex = 0,
                        vertexCountPerInstance = (uint)(command.elementsSize),
                        instanceCount = 1,
                        startInstance = 0
                    };
                    posLodData[index] = command.posLod;
                    index++;
                }
            }

            posLodBuffer.SetData(posLodData, 0, 0, totalCommands);
            argsBuffer.SetData(argsData, 0, 0, totalCommands);
        }

        public void Render() {
            int startCommand = 0;
            foreach (Group group in groups.Values) {
                group.props.SetBuffer(ChunkPosBuffer, posLodBuffer);
                group.props.SetBuffer(MeshVertexBuffer, group.sourceBuffer);

                renderParams.matProps = group.props;
                
                Graphics.RenderPrimitivesIndirect(
                    renderParams,
                    MeshTopology.Triangles,
                    argsBuffer,
                    group.commands.Count,
                    startCommand);
                startCommand += group.commands.Count;
            }
        }

        private void EnsurePosLodCapacity(int required) {
            if (required <= posLodCapacity)
                return;

            int capacity = GrowCapacity(posLodCapacity, required);

            Array.Resize(ref posLodData, capacity);
            Release(ref posLodBuffer);

            posLodBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                capacity,
                sizeof(float) * 4);

            posLodCapacity = capacity;
        }

        private void EnsureArgsCapacity(int required) {
            if (required <= argsCapacity)
                return;

            int capacity = GrowCapacity(argsCapacity, required);

            Array.Resize(ref argsData, capacity);
            Release(ref argsBuffer);

            argsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                capacity,
                GraphicsBuffer.IndirectDrawArgs.size);

            argsCapacity = capacity;
        }

        private static int GrowCapacity(int current, int required) {
            int capacity = current > 0 ? current : 64;

            while (capacity < required)
                capacity *= 2;

            return capacity;
        }

        public void Clear() {
            Release(ref posLodBuffer);
            Release(ref argsBuffer);

            groups.Clear();

            posLodData = Array.Empty<ChunkPos>();
            argsData = Array.Empty<GraphicsBuffer.IndirectDrawArgs>();

            posLodCapacity = 0;
            argsCapacity = 0;
        }

        public void Dispose() {
            Clear();
        }

        private static void Release(ref GraphicsBuffer buffer) {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }
    }
}