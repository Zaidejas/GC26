using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace GC26.Spinv
{
    // CRT-эффект только на камере игры: полноэкранный проход с материалом CRT после всей отрисовки кадра.
    // Общий рендерер проекта не меняется (правило «не трогать проект целиком»): проход добавляется
    // к этой камере перед каждым её кадром
    [RequireComponent(typeof(Camera))]
    public class CrtCamera : MonoBehaviour
    {
        public Material material;

        Camera cam;
        CrtPass pass;

        void OnEnable()
        {
            cam = GetComponent<Camera>();
            if (pass == null) pass = new CrtPass();
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        }

        void OnBeginCamera(ScriptableRenderContext context, Camera rendering)
        {
            if (rendering != cam || material == null) return;
            var data = rendering.GetUniversalAdditionalCameraData();
            if (data == null || data.scriptableRenderer == null) return;
            pass.material = material;
            data.scriptableRenderer.EnqueuePass(pass);
        }

        class CrtPass : ScriptableRenderPass
        {
            public Material material;

            public CrtPass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                TextureHandle source = resources.activeColorTexture;
                TextureDesc desc = renderGraph.GetTextureDesc(source);
                desc.name = "_SpinvCrtColor";
                desc.clearBuffer = false;
                TextureHandle target = renderGraph.CreateTexture(desc);

                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, target, material, 0), "Spinv CRT");
                resources.cameraColor = target;
            }
        }
    }
}
