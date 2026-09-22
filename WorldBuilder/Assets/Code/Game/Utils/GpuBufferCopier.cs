using System; using UnityEngine;
using UnityEngine.Rendering;

public sealed class GpuBufferCopier {
    
    private const uint UInt4PerThread = 4;
    private const uint ThreadsPerGroup = 256;
    private const uint MaxDispatchGroups = 65535;
    
    private readonly ComputeShader shader;
    private readonly int copyKernel;
    private readonly int tailKernel;
    
    private static readonly int Src = Shader.PropertyToID("Src");
    private static readonly int Dst = Shader.PropertyToID("Dst");
    private static readonly int SrcOffset = Shader.PropertyToID("srcOffset");
    private static readonly int DstOffset = Shader.PropertyToID("dstOffset");
    private static readonly int GroupsX = Shader.PropertyToID("groupsX");
    private static readonly int Length = Shader.PropertyToID("length");

    public GpuBufferCopier() {
        var copyShader = Resources.Load<ComputeShader>("GpuBufferCopy");
        shader = copyShader
            ? copyShader
            : throw new InvalidOperationException("Failed to load 'GpuBufferCopy' from Resources.");
        copyKernel = shader.FindKernel("CopyBuffer");
        tailKernel = shader.FindKernel("CopyTail");
    }

    public void Copy(GraphicsBuffer src, GraphicsBuffer dst, long srcOffset, long dstOffset, long length, bool sync = false) {
        ValidateBuffer(src, nameof(src));
        ValidateBuffer(dst, nameof(dst));
        ValidateRange(src, srcOffset, length, nameof(src));
        ValidateRange(dst, dstOffset, length, nameof(dst));
        
        if (length == 0) return;
        
        uint srcIndex = checked((uint)srcOffset);
        uint dstIndex = checked((uint)dstOffset);
        uint count = checked((uint)length);
        uint fastCount = count & ~3u;
        uint tailCount = count & 3u;
        
        if (fastCount != 0) DispatchCopy(src, dst, srcIndex, dstIndex, fastCount);
        
        if (tailCount != 0) DispatchTail(src, dst, srcIndex + fastCount, dstIndex + fastCount, tailCount);

        if (sync) {
            AsyncGPUReadback.Request(dst, 0, 1, request => { }).WaitForCompletion();
        }
    }

    private void DispatchCopy(GraphicsBuffer src, GraphicsBuffer dst, uint srcOffset, uint dstOffset, uint count) {
        shader.SetBuffer(copyKernel, Src, src);
        shader.SetBuffer(copyKernel, Dst, dst);
        shader.SetInt(SrcOffset, checked((int)srcOffset));
        shader.SetInt(DstOffset, checked((int)dstOffset));
        
        uint threadCount = count / UInt4PerThread;
        uint groupCount = (threadCount + ThreadsPerGroup - 1) / ThreadsPerGroup;
        uint groupsX = Math.Min(groupCount, MaxDispatchGroups);
        uint groupsY = (groupCount + groupsX - 1) / groupsX;
        
        if (groupsY > MaxDispatchGroups)
            throw new InvalidOperationException($"The requested copy requires {groupsX} x {groupsY} thread groups, " +
                                                $"but the maximum supported dispatch size is {MaxDispatchGroups} x {MaxDispatchGroups}.");
        
        shader.SetInt(GroupsX, checked((int)groupsX));
        shader.Dispatch(copyKernel, checked((int)groupsX), checked((int)groupsY), 1);
    }

    private void DispatchTail(GraphicsBuffer src, GraphicsBuffer dst, uint srcOffset, uint dstOffset, uint count) {
        shader.SetBuffer(tailKernel, Src, src);
        shader.SetBuffer(tailKernel, Dst, dst);
        shader.SetInt(SrcOffset, checked((int)srcOffset));
        shader.SetInt(DstOffset, checked((int)dstOffset));
        shader.SetInt(Length, checked((int)count));
        shader.Dispatch(tailKernel, 1, 1, 1);
    }

    private static void ValidateBuffer(GraphicsBuffer buffer, string parameterName) {
        if (buffer == null) throw new ArgumentNullException(parameterName);
        
        if (!buffer.IsValid()) throw new InvalidOperationException($"The {parameterName} GraphicsBuffer is not valid.");
        
        if (buffer.stride != 16)
            throw new ArgumentException(
                $"The {parameterName} buffer must have a stride of exactly 16 bytes (uint4). " +
                $"Actual stride: {buffer.stride} bytes.", parameterName);
    }

    private static void ValidateRange(GraphicsBuffer buffer, long offset, long length, string parameterName) {
        if (offset < 0)
            throw new ArgumentOutOfRangeException(parameterName, offset,
                $"The {parameterName} offset must be greater than or equal to zero.");
        
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), length,
                "The copy length must be greater than or equal to zero.");
        
        long end;
        try {
            end = checked(offset + length);
        } catch (OverflowException) {
            throw new ArgumentOutOfRangeException(nameof(length),
                "The requested range exceeds the maximum supported range.");
        }

        if (end > buffer.count)
            throw new ArgumentOutOfRangeException(nameof(length),
                $"The requested {parameterName} range [{offset}, {end}) exceeds the " +
                $"{parameterName} buffer size of {buffer.count} uint4 elements.");
    }
}