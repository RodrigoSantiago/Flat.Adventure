using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.GameTest {
    public class TestCreateMap : MonoBehaviour {

        public ComputeShader shader;
        private ComputeBuffer heightBuffer;

        private int kernel;
        private static readonly int HeightBuffer = Shader.PropertyToID("HeightBuffer");

        private void Awake() {
            kernel = shader.FindKernel("Generate");
            heightBuffer = new ComputeBuffer(1024 / sizeof(uint), sizeof(uint), ComputeBufferType.Raw);
        }

        public void BuildMap() {
            shader.SetBuffer(kernel, HeightBuffer, heightBuffer);
            shader.Dispatch(kernel, 8, 8, 8);
            AsyncGPUReadback.Request(heightBuffer, result => {
                if (result.hasError) {
                    Debug.LogError("Failed to generate");
                }
                
            });
        }
    }
}