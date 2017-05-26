using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StencilTest : MonoBehaviour {
    public Camera cam;
    public Shader replacementShader;

    void Awake() {
        cam.SetReplacementShader(replacementShader, "");
    }
}
