using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum DebugRenderView {
    None,
    Depth,
    Stencil
}

[ExecuteInEditMode]
public class DebugRender : MonoBehaviour {
    public DebugRenderView DebugView;
    public int StencilMin = 0;
    public int StencilMax = 4;

    private Material _depthDebugMaterial;
    private Material _stencilDebugMaterial;

    void Start() {
        GetComponent<Camera>().depthTextureMode = DepthTextureMode.Depth;
        _depthDebugMaterial = new Material(Shader.Find("Portals/Debug/ViewDepth"));
        _stencilDebugMaterial = new Material(Shader.Find("Portals/Debug/ViewStencil"));
    }

    void DrawDepth(RenderTexture src, RenderTexture dst) {
        Graphics.Blit(src, dst, _depthDebugMaterial);
    }

    void DrawStencil(RenderTexture src, RenderTexture dst) {
        RenderTexture tmp = RenderTexture.GetTemporary(src.width, src.height, 24);
        Graphics.Blit(src, tmp);

        for (int i = StencilMin; i <= StencilMax; i++) {
            int bitMask = i;
            _stencilDebugMaterial.SetFloat("_StencilRef", bitMask);
            float f = (float)i / (StencilMax - StencilMin);
            Color color = new Color(f, f, f, 1);
            _stencilDebugMaterial.SetColor("_Color", color);
            DrawQuad(tmp.colorBuffer, src.depthBuffer, _stencilDebugMaterial);
        }
        
        Graphics.Blit(tmp, dst);
        RenderTexture.ReleaseTemporary(tmp);
    }

    void DrawNothing(RenderTexture src, RenderTexture dst) {
        Graphics.Blit(src, dst);
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst) {
        switch (DebugView) {
            case DebugRenderView.Depth:
                DrawDepth(src, dst);
                break;
            case DebugRenderView.Stencil:
                DrawStencil(src, dst);
                break;
            case DebugRenderView.None:
            default:
                DrawNothing(src, dst);
                break;
        }

    }

    void DrawQuad(RenderBuffer colorBuffer, RenderBuffer depthBuffer, Material mat, int pass = 0) {
        Graphics.SetRenderTarget(colorBuffer, depthBuffer);

        mat.SetPass(pass);
        GL.PushMatrix();
        GL.LoadOrtho();
        GL.Begin(GL.QUADS);

        GL.Vertex(new Vector3(0, 0, 0));
        GL.Vertex(new Vector3(0, 1, 0));
        GL.Vertex(new Vector3(1, 1, 0));
        GL.Vertex(new Vector3(1, 0, 0));

        GL.End();
        GL.PopMatrix();
    }
}
