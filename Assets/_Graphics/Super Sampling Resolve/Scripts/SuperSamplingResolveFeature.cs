using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;

namespace SuperSamplingResolve
{
    public class SuperSamplingResolveFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader resolveShader;

        private Material resolveMaterial;
        private SuperSamplingResolvePass resolvePass;

        public override void Create()
        {
            resolvePass = new SuperSamplingResolvePass();
            resolvePass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents + 1;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (resolvePass == null)
                return;

            var cameraData = renderingData.cameraData;

            if (cameraData.renderScale <= 1.0f)
                return;

            if (cameraData.cameraType != CameraType.Game)
                return;

            if (Mathf.RoundToInt(cameraData.renderScale) <= 1)
                return;

            if (resolveShader == null)
                return;

            if (resolveMaterial == null)
                resolveMaterial = new Material(resolveShader);

            resolvePass.Setup(resolveMaterial, cameraData.renderScale);
            renderer.EnqueuePass(resolvePass);
        }

        protected override void Dispose(bool disposing)
        {
            if (Application.isPlaying)
                Destroy(resolveMaterial);
            else
                DestroyImmediate(resolveMaterial);
        }
    }

    public class SuperSamplingResolvePass : ScriptableRenderPass
    {
        private Material material;

        private static readonly int SuperSamplingScaleID = Shader.PropertyToID("_SuperSamplingScale");
        private static readonly int CameraDepthTextureID = Shader.PropertyToID("_CameraDepthTexture");
        private static readonly int CameraNormalsTextureID = Shader.PropertyToID("_CameraNormalsTexture");
        private static readonly int CameraObjectIDTextureID = Shader.PropertyToID("_CameraObjectIDTexture");
        private static readonly int PixelPerfectDetailTextureID = Shader.PropertyToID("_PixelPerfectDetailTexture");

        private class PassData
        {
            internal Material material;
            internal TextureHandle srcColorTex;
            internal TextureHandle srcDepthTex;
            internal TextureHandle srcNormalsTex;
            internal TextureHandle srcObjectIDTex;
            internal TextureHandle srcPixelPerfectDetailTex;
        }

        public SuperSamplingResolvePass()
        {
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        public void Setup(Material material, float renderScale)
        {
            this.material = material;
            material.SetInt(SuperSamplingScaleID, Mathf.RoundToInt(renderScale));
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null)
                return;

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();

            if (resourceData.isActiveTargetBackBuffer)
                return;

            var srcColor = resourceData.activeColorTexture;
            var srcDepth = resourceData.cameraDepthTexture;
            var srcNormals = resourceData.cameraNormalsTexture;
            var srcObjectID = resourceData.cameraObjectIDTexture;
            var srcPixelPerfectDetail = resourceData.pixelPerfectDetailTexture;

            if (!srcColor.IsValid()
                || !srcDepth.IsValid()
                || !srcNormals.IsValid()
                || !srcObjectID.IsValid()
                || !srcPixelPerfectDetail.IsValid())
                return;

            var cam = cameraData.camera;
            int outW = cam.pixelWidth;
            int outH = cam.pixelHeight;

            var srcDesc = srcColor.GetDescriptor(renderGraph);
            var colorDesc = new TextureDesc(outW, outH, false, false)
            {
                format = srcDesc.format,
                depthBufferBits = 0,
                clearBuffer = false,
                msaaSamples = MSAASamples.None,
                name = "_ResolvedColor"
            };
            var resolvedColor = renderGraph.CreateTexture(colorDesc);

            var depthDesc = new TextureDesc(outW, outH, false, false)
            {
                format = GraphicsFormat.R32_SFloat,
                depthBufferBits = 0,
                clearBuffer = false,
                msaaSamples = MSAASamples.None,
                filterMode = FilterMode.Point,
                name = "_ResolvedCameraDepthTexture"
            };
            var resolvedDepth = renderGraph.CreateTexture(depthDesc);

            var srcNormalsDesc = srcNormals.GetDescriptor(renderGraph);
            var normalsDesc = new TextureDesc(outW, outH, false, false)
            {
                format = srcNormalsDesc.format,
                depthBufferBits = 0,
                clearBuffer = false,
                msaaSamples = MSAASamples.None,
                filterMode = FilterMode.Point,
                name = "_ResolvedCameraNormalsTexture"
            };
            var resolvedNormals = renderGraph.CreateTexture(normalsDesc);

            var srcObjectIDDesc = srcObjectID.GetDescriptor(renderGraph);
            var objectIDDesc = new TextureDesc(outW, outH, false, false)
            {
                format = srcObjectIDDesc.format,
                depthBufferBits = 0,
                clearBuffer = false,
                msaaSamples = MSAASamples.None,
                filterMode = FilterMode.Point,
                name = "_ResolvedCameraObjectIDTexture"
            };
            var resolvedObjectID = renderGraph.CreateTexture(objectIDDesc);

            var srcPixelPerfectDetailDesc = srcPixelPerfectDetail.GetDescriptor(renderGraph);
            var pixelPerfectDetailDesc = new TextureDesc(outW, outH, false, false)
            {
                format = srcPixelPerfectDetailDesc.format,
                depthBufferBits = 0,
                clearBuffer = false,
                msaaSamples = MSAASamples.None,
                filterMode = FilterMode.Point,
                name = "_ResolvedPixelPerfectDetailTexture"
            };
            var resolvedPixelPerfectDetail = renderGraph.CreateTexture(pixelPerfectDetailDesc);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Super Sampling Resolve", out var passData))
            {
                passData.material = material;
                passData.srcColorTex = srcColor;
                passData.srcDepthTex = srcDepth;
                passData.srcNormalsTex = srcNormals;
                passData.srcObjectIDTex = srcObjectID;
                passData.srcPixelPerfectDetailTex = srcPixelPerfectDetail;

                builder.UseTexture(srcColor, AccessFlags.Read);
                builder.UseTexture(srcDepth, AccessFlags.Read);
                builder.UseTexture(srcNormals, AccessFlags.Read);
                builder.UseTexture(srcObjectID, AccessFlags.Read);
                builder.UseTexture(srcPixelPerfectDetail, AccessFlags.Read);

                builder.SetRenderAttachment(resolvedColor, 0, AccessFlags.Write);
                builder.SetRenderAttachment(resolvedDepth, 1, AccessFlags.Write);
                builder.SetRenderAttachment(resolvedNormals, 2, AccessFlags.Write);
                builder.SetRenderAttachment(resolvedObjectID, 3, AccessFlags.Write);
                builder.SetRenderAttachment(resolvedPixelPerfectDetail, 4, AccessFlags.Write);

                builder.SetGlobalTextureAfterPass(resolvedDepth, CameraDepthTextureID);
                builder.SetGlobalTextureAfterPass(resolvedNormals, CameraNormalsTextureID);
                builder.SetGlobalTextureAfterPass(resolvedObjectID, CameraObjectIDTextureID);
                builder.SetGlobalTextureAfterPass(resolvedPixelPerfectDetail, PixelPerfectDetailTextureID);

                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc<PassData>(static (data, context) =>
                {
                    context.cmd.SetGlobalTexture(CameraDepthTextureID, data.srcDepthTex);
                    context.cmd.SetGlobalTexture(CameraNormalsTextureID, data.srcNormalsTex);
                    context.cmd.SetGlobalTexture(CameraObjectIDTextureID, data.srcObjectIDTex);
                    context.cmd.SetGlobalTexture(PixelPerfectDetailTextureID, data.srcPixelPerfectDetailTex);

                    Blitter.BlitTexture(context.cmd, data.srcColorTex, Vector2.one, data.material, 0);
                });
            }

            resourceData.cameraColor = resolvedColor;
        }
    }
}
