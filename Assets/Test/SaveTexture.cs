using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SaveTexture : MonoBehaviour {
    public RenderTexture lastFrameTexture;

    Camera _camera;

    void Awake() {
        _camera = GetComponent<Camera>();
        lastFrameTexture = new RenderTexture(_camera.pixelWidth, _camera.pixelHeight, 24);
    }

	void OnRenderImage(RenderTexture src, RenderTexture dest) {
        Graphics.Blit(src, dest);
        Graphics.Blit(src, lastFrameTexture);
    }
}
