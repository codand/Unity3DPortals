using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class InvertCulling : MonoBehaviour {
    private bool oldCulling;
    public void OnPreRender() {
        oldCulling = GL.invertCulling;
        GL.invertCulling = true;
    }

    public void OnPostRender() {
        GL.invertCulling = oldCulling;
    }
}
