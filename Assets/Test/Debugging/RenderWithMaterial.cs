using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RenderWithMaterial : MonoBehaviour {
    [SerializeField] private Material _material;

    void Start() {
        GetComponent<Camera>().depthTextureMode = DepthTextureMode.Depth;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest) {
        Graphics.Blit(src, dest, _material);
    }
}
