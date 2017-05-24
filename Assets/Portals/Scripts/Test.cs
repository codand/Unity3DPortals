using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VR;

public class Test : MonoBehaviour {
    [SerializeField] Renderer _renderer;

    void OnPreRender() {
        Debug.Log(Camera.current.stereoActiveEye);
    }
}
