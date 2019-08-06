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
        //Portals.PortalRenderer.useOldRenderer = toggled;
    }
}
