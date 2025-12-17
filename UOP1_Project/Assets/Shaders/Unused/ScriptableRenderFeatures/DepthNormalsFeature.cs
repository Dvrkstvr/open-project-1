// This code is an adaptation of the open-source work by Alexander Ameye
// From a tutorial originally posted here:
// https://alexanderameye.github.io/outlineshader
// Code also available on his Gist account
// https://gist.github.com/AlexanderAmeye

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Profiling;

public class DepthNormalsFeature : ScriptableRendererFeature
{
	class DepthNormalsPass : ScriptableRenderPass
	{
		int kDepthBufferBits = 32;
		private RTHandle depthAttachmentHandle;
		internal RenderTextureDescriptor descriptor { get; private set; }

		private Material depthNormalsMaterial = null;
		private FilteringSettings m_FilteringSettings;
		string m_ProfilerTag = "DepthNormals Prepass";
		ShaderTagId m_ShaderTagId = new ShaderTagId("DepthOnly");

		public DepthNormalsPass(RenderQueueRange renderQueueRange, LayerMask layerMask, Material material)
		{
			m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
			depthNormalsMaterial = material;
		}

		public void Setup(RenderTextureDescriptor baseDescriptor, RTHandle depthAttachmentHandle)
		{
			this.depthAttachmentHandle = depthAttachmentHandle;
			baseDescriptor.colorFormat = RenderTextureFormat.ARGB32;
			baseDescriptor.depthBufferBits = kDepthBufferBits;
			descriptor = baseDescriptor;
		}

		public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
		{
			// RTHandles are managed, so no need to call GetTemporaryRT
			ConfigureTarget(depthAttachmentHandle);
			ConfigureClear(ClearFlag.All, Color.black);
		}

		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);

			using (new ProfilingScope(cmd, new ProfilingSampler(m_ProfilerTag)))
			{
				context.ExecuteCommandBuffer(cmd);
				cmd.Clear();

				var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
				var drawSettings = CreateDrawingSettings(m_ShaderTagId, ref renderingData, sortFlags);
				drawSettings.perObjectData = PerObjectData.None;

				drawSettings.overrideMaterial = depthNormalsMaterial;

				context.DrawRenderers(renderingData.cullResults, ref drawSettings,
					ref m_FilteringSettings);

				cmd.SetGlobalTexture("_CameraDepthNormalsTexture", depthAttachmentHandle.nameID);
			}

			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
		}

		public override void FrameCleanup(CommandBuffer cmd)
		{
			// RTHandles are released by the system, so no need to manually release
			depthAttachmentHandle = null;
		}
	}

	DepthNormalsPass depthNormalsPass;
	RTHandle depthNormalsTexture;
	Material depthNormalsMaterial;

	public override void Create()
	{
		depthNormalsMaterial = CoreUtils.CreateEngineMaterial("Hidden/Internal-DepthNormalsTexture");
		depthNormalsPass = new DepthNormalsPass(RenderQueueRange.opaque, -1, depthNormalsMaterial);
		depthNormalsPass.renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;
		depthNormalsTexture = RTHandles.Alloc("_CameraDepthNormalsTexture", name: "_CameraDepthNormalsTexture");
	}

	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		depthNormalsPass.Setup(renderingData.cameraData.cameraTargetDescriptor, depthNormalsTexture);
		renderer.EnqueuePass(depthNormalsPass);
	}
}

