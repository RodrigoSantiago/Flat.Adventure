using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.GraphicGenerator {
    public class MeshRenderBuffer : IDisposable {
        private sealed class Group {
            public GraphicsBuffer SourceBuffer;
            public MaterialPropertyBlock props = new MaterialPropertyBlock();

            public int StartCommand;
            public int CommandCount;
        }

        private readonly Dictionary<GraphicsBuffer, Group> groups = new();

        // CPU data.
        private Vector4[] posLodData = Array.Empty<Vector4>();
        private GraphicsBuffer.IndirectDrawArgs[] argsData = Array.Empty<GraphicsBuffer.IndirectDrawArgs>();

        // GPU data.
        private GraphicsBuffer posLodBuffer;
        private GraphicsBuffer argsBuffer;

        private int posLodCapacity;
        private int posLodCount;

        private int argsCapacity;
        private int argsCount;

        private RenderParams renderParams;
        public RenderParams Params => renderParams;

        public void Begin(RenderParams renderParams) {
            this.renderParams = renderParams;

            posLodCount = 0;
            argsCount = 0;

            foreach (Group group in groups.Values) {
                group.StartCommand = 0;
                group.CommandCount = 0;
            }
        }

        public void Add(Vector4 posLod, GraphicsBuffer buffer, int elementsOffset, int elementsSize) {
            if (!groups.TryGetValue(buffer, out var group)) {
                group = new Group {
                    SourceBuffer = buffer,
                    StartCommand = argsCount
                };

                groups.Add(buffer, group);
            }

            int instanceId = posLodCount++;

            EnsurePosLodCapacity(posLodCount);
            EnsureArgsCapacity(argsCount + 1);

            posLodData[instanceId] = posLod;

            argsData[argsCount] =
                new GraphicsBuffer.IndirectDrawArgs {
                    vertexCountPerInstance = (uint)elementsSize,
                    instanceCount = 1,
                    startVertex = (uint)elementsOffset,
                    startInstance = (uint)instanceId
                };

            argsCount++;
            group.CommandCount++;
        }

        public void Commit() {
            if (argsCount == 0) return;

            posLodBuffer.SetData(posLodData, 0, 0, posLodCount);
            argsBuffer.SetData(argsData, 0, 0, argsCount);

            foreach (Group group in groups.Values) {
                if (group.CommandCount == 0)
                    continue;

                // group.props.SetBuffer("PosLodBuffer", posLodBuffer);
                // group.props.SetBuffer("SourceBuffer", group.SourceBuffer);

                renderParams.matProps = group.props;

                Graphics.RenderPrimitivesIndirect(
                    in renderParams,
                    MeshTopology.Triangles,
                    argsBuffer,
                    group.CommandCount,
                    group.StartCommand);
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

            posLodData = Array.Empty<Vector4>();
            argsData = Array.Empty<GraphicsBuffer.IndirectDrawArgs>();

            posLodCapacity = 0;
            posLodCount = 0;

            argsCapacity = 0;
            argsCount = 0;
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