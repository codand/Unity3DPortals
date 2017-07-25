using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StereoTest : MonoBehaviour {
    public RenderTexture _texture;

    private Camera _camera;

    void Awake() {
        _camera = GetComponent<Camera>();
    }

	void LateUpdate () {
        RenderTexture temp = RenderTexture.GetTemporary(Camera.main.pixelWidth, Camera.main.pixelHeight, 24);
        temp.name = "My Temp Texture";
        _camera.targetTexture = temp;
        Shader.EnableKeyword("UNITY_SINGLE_PASS_STEREO");
        _camera.Render();
        Shader.DisableKeyword("UNITY_SINGLE_PASS_STEREO");
        RenderTexture.ReleaseTemporary(temp);
        //DrawToScreen(_texture);
	}

    void DrawToScreen(Texture texture) {
        Material mat = new Material(Shader.Find("Hidden/BlitCopy"));
        mat.mainTexture = texture;

        DrawQuad(Display.main.colorBuffer, Display.main.depthBuffer, mat);
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
