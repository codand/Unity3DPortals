using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

public class TogglePostProcessing : MonoBehaviour
{
    public PostProcessVolume _volume;
    public PostProcessLayer _layer;

    bool _enabled = true;

    private void Start() {
        _volume.enabled = _enabled;
        _layer.enabled = _enabled;
    }


    private void Update() {
        if (Input.GetKeyDown(KeyCode.T)) {
            _enabled = !_enabled;
            _volume.enabled = _enabled;
            _layer.enabled = _enabled;
        }
    }
}
