using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RecursivelyReplaceMaterial : MonoBehaviour {
    public Shader shader;

    private Dictionary<Renderer, Shader> _rendererToShader = new Dictionary<Renderer, Shader>();

    void OnEnable() {
        ReplaceMaterials();
    }

    void OnDisable() {
        RestoreMaterials();
    }

    void ReplaceMaterials() {
        ReplaceMaterialRecursively(this.gameObject);
    }

    void ReplaceMaterialRecursively(GameObject obj) {
        Renderer renderer = obj.GetComponent<Renderer>();
        if(renderer) {
            _rendererToShader[renderer] = renderer.material.shader;
            renderer.material.shader = shader;
        }

        foreach (Transform child in obj.transform) {
            ReplaceMaterialRecursively(child.gameObject);
        }
    }

    void RestoreMaterials() {
        foreach(KeyValuePair<Renderer, Shader> kvp in _rendererToShader) {
            Renderer renderer = kvp.Key;
            Shader shader = kvp.Value;

            renderer.material.shader = shader;
        }
    }
}
