using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ToggleNewOrOldRenderer : MonoBehaviour
{
    void Start() {
        GetComponent<UnityEngine.UI.Toggle>().onValueChanged.AddListener(OnToggle);
    }

    private void OnToggle(bool toggled) {
        foreach (var renderer in GameObject.FindObjectsOfType<Portals.PortalRenderer>()) {
            renderer.useOldRenderer = toggled;
        }
    }
}
