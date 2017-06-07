using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Portals;

[ExecuteInEditMode]
public class PortalTele : MonoBehaviour {
    public Portal portal;
    public Transform target;

    void Update() {
        ////target.position = portal.WorldToPortalMatrix().MultiplyPoint3x4(this.transform.position);
        //target.position = portal.ExitPortal.transform.TransformPoint(Quaternion.Euler(0, 180, 0) * portal.transform.InverseTransformPoint(this.transform.position));
        //target.forward = portal.ExitPortal.transform.TransformDirection(Quaternion.Euler(0, 180, 0) * portal.transform.InverseTransformDirection(this.transform.forward));

        portal.ApplyWorldToPortalTransform(target, this.transform);
    }
}
