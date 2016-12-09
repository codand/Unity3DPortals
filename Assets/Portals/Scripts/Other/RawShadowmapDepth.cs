using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
public class RawShadowmapDepth : MonoBehaviour {

    public Light m_Light;
    RenderTexture m_ShadowmapCopy;

    void Start() {
        RenderTargetIdentifier shadowmap = BuiltinRenderTextureType.CurrentActive;
        m_ShadowmapCopy = new RenderTexture(1024, 1024, 0);
        CommandBuffer cb = new CommandBuffer();
        cb.name = "Copy Shadowmap";

        // Change shadow sampling mode for m_Light's shadowmap.
        cb.SetShadowSamplingMode(shadowmap, ShadowSamplingMode.RawDepth);

        // The shadowmap values can now be sampled normally - copy it to a different render texture.
        cb.Blit(shadowmap, new RenderTargetIdentifier(m_ShadowmapCopy));

        // Execute after the shadowmap has been filled.
        m_Light.AddCommandBuffer(LightEvent.AfterShadowMap, cb);

        // Sampling mode is restored automatically after this command buffer completes, so shadows will render normally.
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest) {
        Graphics.Blit(src, dest);
        // Display the shadowmap in the corner.
        Camera.main.rect = new Rect(0, 0, 0.5f, 0.5f);
        Graphics.Blit(m_ShadowmapCopy, dest);
        Camera.main.rect = new Rect(0, 0, 1, 1);
    }
}