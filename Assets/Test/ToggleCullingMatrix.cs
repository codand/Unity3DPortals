using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Portals;

using UnityEngine.UI;

public class ToggleCullingMatrix : MonoBehaviour {
    public Portal portal;
    public Toggle toggleCullingMatrix;
    public Toggle toggleProjectionMatrix;

    public void Update() {
        portal.UseCullingMatrix = toggleCullingMatrix.isOn;
        portal.UseProjectionMatrix = toggleProjectionMatrix.isOn;
    }
}
