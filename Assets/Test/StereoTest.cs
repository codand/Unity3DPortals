using UnityEngine;
using UnityEngine.VR;

[ExecuteInEditMode]
public class StereoTest : MonoBehaviour {
    private Camera m_Camera;


    void OnEnable() {
        Camera.onPreRender += Foo;
    }
    void OnDisable() {
        Camera.onPreRender -= Foo;
    }

    void Foo(Camera cam) {
        // Globally set the current eye for Multi-Pass stereo rendering.
        // We also run this code in Single-Pass rendering because Unity doesn't have a runtime
        // check for single/multi-pass stereo, but the value gets ignored.
        Shader.SetGlobalFloat("_PortalMultiPassCurrentEye", (int) cam.stereoActiveEye);
    }

    //void Awake() {
    //    m_Camera = GetComponent<Camera>();

    //    RenderTextureDescriptor desc = new RenderTextureDescriptor(
    //        VRSettings.eyeTextureWidth * 2,
    //        VRSettings.eyeTextureHeight,
    //        RenderTextureFormat.Default,
    //        24);
    //    desc.vrUsage = VRTextureUsage.TwoEyes;
    //    RenderTexture texture = new RenderTexture(desc);
    //    texture.name = "Stereo RenderTexture";
    //    texture.Create();
    //    m_Camera.targetTexture = texture;
    //}

    //void LateUpdate() {
    //    //Shader.EnableKeyword("UNITY_SINGLE_PASS_STEREO");
    //    m_Camera.Render();
    //    //Shader.DisableKeyword("UNITY_SINGLE_PASS_STEREO");
    //}
}

//    void DrawToScreen(Texture texture) {
//        Material mat = new Material(Shader.Find("Hidden/BlitCopy"));
//        mat.mainTexture = texture;

//        DrawQuad(Display.main.colorBuffer, Display.main.depthBuffer, mat);
//    }

//    void DrawQuad(RenderBuffer colorBuffer, RenderBuffer depthBuffer, Material mat, int pass = 0) {
//        Graphics.SetRenderTarget(colorBuffer, depthBuffer);

//        mat.SetPass(pass);
//        GL.PushMatrix();
//        GL.LoadOrtho();
//        GL.Begin(GL.QUADS);

//        GL.Vertex(new Vector3(0, 0, 0));
//        GL.Vertex(new Vector3(0, 1, 0));
//        GL.Vertex(new Vector3(1, 1, 0));
//        GL.Vertex(new Vector3(1, 0, 0));

//        GL.End();
//        GL.PopMatrix();
//    }
//}
