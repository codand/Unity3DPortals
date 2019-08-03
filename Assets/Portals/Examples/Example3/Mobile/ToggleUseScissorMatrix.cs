using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ToggleUseScissorMatrix : MonoBehaviour
{
    void Start() {
        GetComponent<UnityEngine.UI.Toggle>().onValueChanged.AddListener(OnToggle);
    }

    private void OnToggle(bool toggled) {
        foreach (var portal in GameObject.FindObjectsOfType<Portals.Portal>()) {
            portal.UseScissorRect = toggled;
        }
    }
}
