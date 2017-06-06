using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CopyPortalMesh : MonoBehaviour {
    void Start() {
        GetComponent<MeshFilter>().sharedMesh = Portals.Portal._mesh;
    }
}
