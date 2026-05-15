using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Ledsna.SSR
{
    public class SSRPrepassFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public LayerMask waterLayer = -1;
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingSkybox;

            [Header("Resolution")]
            [Range(0.1f, 2.0f)] public float resolutionScale = 0.5f;

            [Header("Raymarching Settings")]
            [Range(0.01f, 1f)] public float stepSize = 0.2f;
            [Range(1.0f, 2.0f)] public float stepGrowth = 1.05f;
            [Range(16, 256)] public int maxSteps = 50;
            [Range(0.01f, 5.0f)] public float thickness = 1.5f;

            [Header("Blending")]
            [Range(0f, 0.5f)] public float edgeFade = 0.1f;
            [Range(0.0f, 0.1f)] public float distanceFade = 0.005f;
            [Range(0.0f, 2.0f)] public float reflectionIntensity = 1.0f;
            [Range(1.0f, 10.0f)] public float resolveBlurStrength = 5.0f;

            public Shader ssrTraceShader;
            public Shader ssrResolveShader;
        }

        public Settings settings = new Settings();
        SSRPrepassPass m_ScriptablePass;
        CopyHistoryPass m_HistoryPass;
        Material m_TraceMaterial;
        Material m_ResolveMaterial;
        RTHandle m_ColorHistory;

        public override void Create()
        {
            m_ScriptablePass = new SSRPrepassPass(settings);
            m_HistoryPass = new CopyHistoryPass();

            if (settings.ssrTraceShader != null)
            {
                m_TraceMaterial = CoreUtils.CreateEngineMaterial(settings.ssrTraceShader);
            }
            if (settings.ssrResolveShader != null)
            {
                m_ResolveMaterial = CoreUtils.CreateEngineMaterial(settings.ssrResolveShader);
            }
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(m_TraceMaterial);
            CoreUtils.Destroy(m_ResolveMaterial);
            m_ColorHistory?.Release();
            m_ColorHistory = null;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            if (settings.ssrTraceShader == null)
            {
                var shader = Shader.Find("Hidden/Ledsna/SSRTrace");
                if (shader != null)
                {
                    settings.ssrTraceShader = shader;
                    if (m_TraceMaterial == null) m_TraceMaterial = CoreUtils.CreateEngineMaterial(shader);
                }
            }
            if (settings.ssrResolveShader == null)
            {
                var shader = Shader.Find("Hidden/Ledsna/SSRResolve");
                if (shader != null)
                {
                    settings.ssrResolveShader = shader;
                    if (m_ResolveMaterial == null) m_ResolveMaterial = CoreUtils.CreateEngineMaterial(shader);
                }
            }

            var desc = renderingData.cameraData.cameraTargetDescriptor;
            if (m_ColorHistory == null || m_ColorHistory.rt == null || m_ColorHistory.rt.width != desc.width || m_ColorHistory.rt.height != desc.height)
            {
                if (m_ColorHistory != null) m_ColorHistory.Release();
                m_ColorHistory = RTHandles.Alloc(desc.width, desc.height,
                    colorFormat: desc.graphicsFormat,
                    name: "_SSR_ColorHistory",
                    useMipMap: true,
                    autoGenerateMips: false,
                    enableRandomWrite: false);
            }

            m_ScriptablePass.Setup(settings, m_TraceMaterial, m_ResolveMaterial, m_ColorHistory);
            renderer.EnqueuePass(m_ScriptablePass);

            m_HistoryPass.Setup(m_ColorHistory);
            renderer.EnqueuePass(m_HistoryPass);
        }

        class CopyHistoryPass : ScriptableRenderPass
        {
            RTHandle m_HistoryHandle;

            public void Setup(RTHandle historyHandle)
            {
                m_HistoryHandle = historyHandle;
                this.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            }

            class PassData
            {
                public TextureHandle src;
                public TextureHandle dstHistory;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                TextureHandle history = renderGraph.ImportTexture(m_HistoryHandle);

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (!resourceData.activeColorTexture.IsValid()) return;

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("SSR Update History", out var data))
                {
                    data.src = resourceData.activeColorTexture;
                    data.dstHistory = history;

                    builder.UseTexture(data.src, AccessFlags.Read);
                    builder.SetRenderAttachment(data.dstHistory, 0, AccessFlags.Write);

                    builder.SetRenderFunc((PassData passData, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(context.cmd, passData.src, new Vector4(1, 1, 0, 0), 0.0f, false);
                    });
                }

                using (var mipBuilder = renderGraph.AddUnsafePass<PassData>("SSR History Mips", out var mipData))
                {
                    mipData.dstHistory = history;
                    mipBuilder.UseTexture(mipData.dstHistory, AccessFlags.ReadWrite);

                    mipBuilder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        cmd.GenerateMips(data.dstHistory);
                    });
                }
            }
        }

        class SSRPrepassPass : ScriptableRenderPass
        {
            Settings m_Settings;
            Material m_TraceMaterial;
            Material m_ResolveMaterial;
            RTHandle m_ColorHistoryHandle;
            static readonly ShaderTagId k_SSRPrepassTag = new ShaderTagId("SSRPrepass");

            private static readonly int k_SSRStep = Shader.PropertyToID("_SSRStep");
            private static readonly int k_SSRStepGrowth = Shader.PropertyToID("_SSRStepGrowth");
            private static readonly int k_SSRMaxSteps = Shader.PropertyToID("_SSRMaxSteps");
            private static readonly int k_SSRThickness = Shader.PropertyToID("_SSRThickness");
            private static readonly int k_SSREdgeFade = Shader.PropertyToID("_SSREdgeFade");
            private static readonly int k_SSRDistanceFade = Shader.PropertyToID("_SSRDistanceFade");
            private static readonly int k_SSRIntensity = Shader.PropertyToID("_SSRIntensity");
            private static readonly int k_SSRBlurStrength = Shader.PropertyToID("_SSRBlurStrength");

            private static readonly int k_SSRDepthTextureID = Shader.PropertyToID("_SSRDepthTexture");
            private static readonly int k_SceneDepthTextureID = Shader.PropertyToID("_SceneDepthTexture");
            private static readonly int k_SSRNormalsTextureID = Shader.PropertyToID("_SSRNormalsTexture");
            private static readonly int k_SSRReflectionTextureID = Shader.PropertyToID("_SSRReflectionTexture");

            public SSRPrepassPass(Settings settings)
            {
                m_Settings = settings;
            }

            public void Setup(Settings settings, Material traceMaterial, Material resolveMaterial, RTHandle colorHistoryHandle)
            {
                m_Settings = settings;
                m_TraceMaterial = traceMaterial;
                m_ResolveMaterial = resolveMaterial;
                m_ColorHistoryHandle = colorHistoryHandle;
                renderPassEvent = settings.renderPassEvent;
            }

            class SSRPassData
            {
                public TextureHandle srcDepth;
                public TextureHandle srcNormals;
                public TextureHandle sceneDepth;
                public TextureHandle dstDepth;
                public TextureHandle dstNormals;
                public TextureHandle dstTrace;
                public TextureHandle dstReflection;
                public TextureHandle srcHistoryColor;
                public RendererListHandle rendererList;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                var desc = cameraData.cameraTargetDescriptor;
                int ssrWidth = (int)(desc.width * m_Settings.resolutionScale);
                int ssrHeight = (int)(desc.height * m_Settings.resolutionScale);
                ssrWidth = Mathf.Max(1, ssrWidth);
                ssrHeight = Mathf.Max(1, ssrHeight);

                TextureDesc depthDesc = new TextureDesc(ssrWidth, ssrHeight);
                depthDesc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.None;
                depthDesc.depthBufferBits = (DepthBits)desc.depthBufferBits;
                depthDesc.msaaSamples = MSAASamples.None;
                depthDesc.name = "SSR Depth";
                TextureHandle ssrDepth = renderGraph.CreateTexture(depthDesc);

                TextureDesc normalDesc = new TextureDesc(ssrWidth, ssrHeight);
                normalDesc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
                normalDesc.depthBufferBits = DepthBits.None;
                normalDesc.msaaSamples = MSAASamples.None;
                normalDesc.name = "SSR Normals";
                TextureHandle ssrNormals = renderGraph.CreateTexture(normalDesc);

                TextureDesc traceDesc = new TextureDesc(ssrWidth, ssrHeight);
                traceDesc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
                traceDesc.depthBufferBits = DepthBits.None;
                traceDesc.msaaSamples = MSAASamples.None;
                traceDesc.name = "SSR Trace Result";
                traceDesc.filterMode = FilterMode.Bilinear;
                TextureHandle ssrTrace = renderGraph.CreateTexture(traceDesc);

                TextureDesc reflectDesc = new TextureDesc(ssrWidth, ssrHeight);
                reflectDesc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
                reflectDesc.depthBufferBits = DepthBits.None;
                reflectDesc.msaaSamples = MSAASamples.None;
                reflectDesc.useMipMap = true;
                reflectDesc.autoGenerateMips = false;
                reflectDesc.enableRandomWrite = true;
                reflectDesc.name = "SSR Reflection Color";
                reflectDesc.filterMode = FilterMode.Trilinear;
                TextureHandle ssrReflection = renderGraph.CreateTexture(reflectDesc);

                TextureHandle historyColor = renderGraph.ImportTexture(m_ColorHistoryHandle);

                TextureHandle srcCameraDepth = resourceData.cameraDepthTexture;
                TextureHandle srcCameraNormals = resourceData.cameraNormalsTexture;

                SortingSettings sortingSettings = new SortingSettings(cameraData.camera);
                sortingSettings.criteria = SortingCriteria.CommonTransparent;

                DrawingSettings drawingSettings = new DrawingSettings(k_SSRPrepassTag, sortingSettings)
                {
                    enableDynamicBatching = true,
                    enableInstancing = true,
                    perObjectData = PerObjectData.None
                };

                FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.all, m_Settings.waterLayer);

                RendererListParams listParams = new RendererListParams(frameData.Get<UniversalRenderingData>().cullResults, drawingSettings, filteringSettings);
                RendererListHandle rendererList = renderGraph.CreateRendererList(listParams);

                if (m_TraceMaterial != null)
                {
                    using (var copyBuilder = renderGraph.AddRasterRenderPass<SSRPassData>("SSR Copy Depth (Downsample)", out var copyData))
                    {
                        copyData.srcDepth = srcCameraDepth;
                        copyData.dstDepth = ssrDepth;
                        copyBuilder.UseTexture(copyData.srcDepth, AccessFlags.Read);
                        copyBuilder.SetRenderAttachmentDepth(copyData.dstDepth, AccessFlags.Write);
                        copyBuilder.AllowGlobalStateModification(true);
                        copyBuilder.SetRenderFunc((SSRPassData data, RasterGraphContext context) =>
                        {
                            context.cmd.SetGlobalTexture("_CameraDepthTexture", data.srcDepth);
                            Blitter.BlitTexture(context.cmd, data.srcDepth, new Vector4(1, 1, 0, 0), m_TraceMaterial, 2);
                        });
                    }
                }

                if (m_TraceMaterial != null)
                {
                    using (var blitBuilder = renderGraph.AddRasterRenderPass<SSRPassData>("SSR Copy Normals", out var blitData))
                    {
                        blitData.srcNormals = srcCameraNormals;
                        blitData.dstNormals = ssrNormals;
                        blitBuilder.AllowGlobalStateModification(true);

                        blitBuilder.UseTexture(blitData.srcNormals, AccessFlags.Read);
                        blitBuilder.SetRenderAttachment(blitData.dstNormals, 0, AccessFlags.Write);

                        blitBuilder.SetRenderFunc((SSRPassData data, RasterGraphContext context) =>
                        {
                            context.cmd.SetGlobalTexture("_CameraNormalsTexture", data.srcNormals);

                            Blitter.BlitTexture(context.cmd, data.srcNormals, new Vector4(1, 1, 0, 0), m_TraceMaterial, 1);
                        });
                    }
                }

                using (var drawBuilder = renderGraph.AddRasterRenderPass<SSRPassData>("SSR Draw Water", out var drawData))
                {
                    drawData.rendererList = rendererList;
                    drawData.dstDepth = ssrDepth;
                    drawData.dstNormals = ssrNormals;

                    drawBuilder.UseRendererList(drawData.rendererList);
                    drawBuilder.SetRenderAttachment(drawData.dstNormals, 0, AccessFlags.ReadWrite);
                    drawBuilder.SetRenderAttachmentDepth(drawData.dstDepth, AccessFlags.ReadWrite);

                    drawBuilder.SetGlobalTextureAfterPass(drawData.dstDepth, k_SSRDepthTextureID);
                    drawBuilder.SetGlobalTextureAfterPass(drawData.dstNormals, k_SSRNormalsTextureID);

                    drawBuilder.SetRenderFunc((SSRPassData data, RasterGraphContext context) =>
                    {
                        context.cmd.DrawRendererList(data.rendererList);
                    });
                }

                if (m_TraceMaterial != null)
                {
                    using (var traceBuilder = renderGraph.AddRasterRenderPass<SSRPassData>("SSR Trace", out var traceData))
                    {
                        traceData.srcDepth = ssrDepth;
                        traceData.sceneDepth = srcCameraDepth;
                        traceData.srcNormals = ssrNormals;
                        traceData.srcHistoryColor = historyColor;
                        traceData.dstTrace = ssrTrace;

                        traceBuilder.UseTexture(traceData.srcDepth, AccessFlags.Read);
                        traceBuilder.UseTexture(traceData.sceneDepth, AccessFlags.Read);
                        traceBuilder.UseTexture(traceData.srcNormals, AccessFlags.Read);
                        traceBuilder.UseTexture(traceData.srcHistoryColor, AccessFlags.Read);
                        traceBuilder.AllowGlobalStateModification(true);

                        traceBuilder.SetRenderAttachment(traceData.dstTrace, 0, AccessFlags.Write);

                        traceBuilder.SetRenderFunc((SSRPassData data, RasterGraphContext context) =>
                        {
                            if (m_TraceMaterial == null) return;

                            m_TraceMaterial.SetFloat(k_SSRStep, m_Settings.stepSize);
                            m_TraceMaterial.SetFloat(k_SSRStepGrowth, m_Settings.stepGrowth);
                            m_TraceMaterial.SetFloat(k_SSRMaxSteps, (float)m_Settings.maxSteps);
                            m_TraceMaterial.SetFloat(k_SSRThickness, m_Settings.thickness);
                            m_TraceMaterial.SetFloat(k_SSREdgeFade, m_Settings.edgeFade);
                            m_TraceMaterial.SetFloat(k_SSRDistanceFade, m_Settings.distanceFade);
                            m_TraceMaterial.SetFloat(k_SSRIntensity, m_Settings.reflectionIntensity);

                            context.cmd.SetGlobalTexture(k_SSRDepthTextureID, data.srcDepth);
                            context.cmd.SetGlobalTexture(k_SceneDepthTextureID, data.sceneDepth);

                            context.cmd.SetGlobalTexture(k_SSRNormalsTextureID, data.srcNormals);

                            Blitter.BlitTexture(context.cmd, data.srcHistoryColor, new Vector4(1, 1, 0, 0), m_TraceMaterial, 0);
                        });
                    }
                }

                if (m_ResolveMaterial != null)
                {
                    using (var resolveBuilder = renderGraph.AddRasterRenderPass<SSRPassData>("SSR Resolve", out var resolveData))
                    {
                        resolveData.dstTrace = ssrTrace;
                        resolveData.srcNormals = ssrNormals;
                        resolveData.dstReflection = ssrReflection;

                        resolveBuilder.UseTexture(resolveData.dstTrace, AccessFlags.Read);
                        resolveBuilder.UseTexture(resolveData.srcNormals, AccessFlags.Read);
                        resolveBuilder.AllowGlobalStateModification(true);

                        resolveBuilder.SetRenderAttachment(resolveData.dstReflection, 0, AccessFlags.Write);

                        resolveBuilder.SetGlobalTextureAfterPass(resolveData.dstReflection, k_SSRReflectionTextureID);

                        resolveBuilder.SetRenderFunc((SSRPassData data, RasterGraphContext context) =>
                        {
                            if (m_ResolveMaterial == null) return;

                            m_ResolveMaterial.SetFloat(k_SSRBlurStrength, m_Settings.resolveBlurStrength);

                            context.cmd.SetGlobalTexture(k_SSRNormalsTextureID, data.srcNormals);

                            Blitter.BlitTexture(context.cmd, data.dstTrace, new Vector4(1, 1, 0, 0), m_ResolveMaterial, 0);
                        });
                    }
                }

                if (m_TraceMaterial != null)
                {
                    using (var mipBuilder = renderGraph.AddUnsafePass<SSRPassData>("SSR Generate Mips", out var mipData))
                    {
                        mipData.dstReflection = ssrReflection;
                        mipBuilder.UseTexture(mipData.dstReflection, AccessFlags.ReadWrite);

                        mipBuilder.SetRenderFunc((SSRPassData data, UnsafeGraphContext context) =>
                        {
                            CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                            cmd.GenerateMips(data.dstReflection);
                        });
                    }
                }
            }
        }
    }
}
