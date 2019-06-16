using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
[ExecuteInEditMode]
public class TextureScaler : MonoBehaviour {
    public Vector4 _MainTex_ST;

    void LateUpdate() {
        Renderer renderer = GetComponent<Renderer>();

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);

        block.SetVector("_MainTex_ST", transform.lossyScale);

        renderer.SetPropertyBlock(block);
    }
}
