using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RenderColor : MonoBehaviour {
    public Camera c1;
    public Camera c2;
    public Camera c3;

    public Renderer c1View;
    public Renderer c2View;
    public Renderer c3View;

    void OnEnable() {
        c1.targetTexture = RenderTexture.GetTemporary(Camera.main.pixelWidth, Camera.main.pixelHeight, 24, RenderTextureFormat.Default);
        c2.targetTexture = RenderTexture.GetTemporary(Camera.main.pixelWidth, Camera.main.pixelHeight, 24, RenderTextureFormat.Default);
        c3.targetTexture = RenderTexture.GetTemporary(Camera.main.pixelWidth, Camera.main.pixelHeight, 24, RenderTextureFormat.Default);
    }

    void OnDisable() {
        RenderTexture.ReleaseTemporary(c1.targetTexture);
        RenderTexture.ReleaseTemporary(c2.targetTexture);
        RenderTexture.ReleaseTemporary(c3.targetTexture);

        c1.targetTexture = null;
        c1.targetTexture = null;
        c1.targetTexture = null;
    }

	void OnWillRenderObject() {
        if (Camera.current == c1) {
            GetComponent<Renderer>().material.SetTexture("_MainTex", null);
            GetComponent<Renderer>().material.color = Color.red;
        }

        if (Camera.current == c2) {
            c3.Render();

            GetComponent<Renderer>().material.SetTexture("_MainTex", c3.targetTexture);
            GetComponent<Renderer>().material.color = Color.green;
        }

        if (Camera.current == c3) {
            GetComponent<Renderer>().material.SetTexture("_MainTex", null);
            GetComponent<Renderer>().material.color = Color.blue;
        }
    }

    void Update() {
        c1.Render();
        c2.Render();

        c1View.material.SetTexture("_MainTex", c1.targetTexture);
        c2View.material.SetTexture("_MainTex", c2.targetTexture);
        c3View.material.SetTexture("_MainTex", c3.targetTexture);
    }
}
