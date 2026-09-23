using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Game.DebugUtils {
    public class CustomCommandBufferFeature : ScriptableRendererFeature {
        public delegate void RenderListener(RasterCommandBuffer commandBuffer, bool shadowPass);
        public static event RenderListener OnRender;
            
        class CustomCommandBufferPass : ScriptableRenderPass {

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {

                /*using var builder = renderGraph.AddRasterRenderPass<PassData>("Custom Command Buffer", out _);

                var resourceData = frameData.Get<UniversalResourceData>();

                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);

                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);

                builder.SetRenderFunc((PassData _, RasterGraphContext context) => {
                    OnRender?.Invoke(context.cmd);
                });*/
                
                var resourceData = frameData.Get<UniversalResourceData>();
                
                // -------------------------------------------------------------
                // A) CASO 1: EXECUÇÃO NA FASE DE SOMBRA (BeforeRenderingShadows)
                // -------------------------------------------------------------
                if (renderPassEvent == RenderPassEvent.BeforeRenderingShadows) {
                    
                    // Se o mapa de sombras da luz principal não for válido neste frame, pula
                    if (!resourceData.mainShadowsTexture.IsValid()) return;

                    using var shadowBuilder = renderGraph.AddRasterRenderPass<PassData>("Custom CB - ShadowCaster", out _);
                    
                    // IMPORTANTE: O destino do desenho DEVE SER a textura de sombras da luz
                    shadowBuilder.SetRenderAttachmentDepth(resourceData.mainShadowsTexture);
                    shadowBuilder.AllowGlobalStateModification(true);

                    shadowBuilder.SetRenderFunc((PassData _, RasterGraphContext context) => {
                        OnRender?.Invoke(context.cmd, true); // true = Pass de Sombra
                    });

                    return;
                }

                // -------------------------------------------------------------
                // B) CASO 2: EXECUÇÃO NA FASE DE COR (AfterRenderingOpaques)
                // -------------------------------------------------------------
                using var colorBuilder = renderGraph.AddRasterRenderPass<PassData>("Custom CB - Color Pass", out _);

                colorBuilder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                colorBuilder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);
                colorBuilder.AllowGlobalStateModification(true);

                colorBuilder.SetRenderFunc((PassData _, RasterGraphContext context) => {
                    OnRender?.Invoke(context.cmd, false); // false = Pass de Cor
                });
            }

            private class PassData {
            }
        }

        private CustomCommandBufferPass pass;
        private CustomCommandBufferPass shadowPass;
        private CustomCommandBufferPass colorPass;

        public override void Create() {
            shadowPass = new CustomCommandBufferPass();
            shadowPass.renderPassEvent = RenderPassEvent.BeforeRenderingShadows;

            colorPass = new CustomCommandBufferPass();
            colorPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
            if (!Application.isPlaying) return;
            
            renderer.EnqueuePass(shadowPass);
            renderer.EnqueuePass(colorPass);
        }
    }
}