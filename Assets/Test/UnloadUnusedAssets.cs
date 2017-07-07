using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class UnloadUnusedAssets : MonoBehaviour {
    void OnEnable() {
        Debug.Log("Collecting");
        System.GC.Collect();
    }
}
