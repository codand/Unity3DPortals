using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VR;

public class Test : MonoBehaviour {
    [SerializeField] Renderer _renderer;

    void OnPreRender() {
        Camera cam = Camera.current;
        if (cam.stereoActiveEye == Camera.MonoOrStereoscopicEye.Left) {
            _renderer.sharedMaterial.color = Color.blue;
            Debug.Log(cam.name + " Left: Blue");
        } else if (cam.stereoActiveEye == Camera.MonoOrStereoscopicEye.Right) {
            _renderer.sharedMaterial.color = Color.red;
            Debug.Log(cam.name + " Right: Red");
        }
    }
}
