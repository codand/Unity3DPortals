using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Portals;

public class GetSide : MonoBehaviour {
    public Portal portal;

    void Update() {
        Debug.Log(portal.Plane.GetSide(this.transform.position));
    }
}
