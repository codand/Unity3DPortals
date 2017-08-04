using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Portals;

public class Teleport : MonoBehaviour {
    public Camera target;
    public Portal portal;

    public Matrix4x4 mat = Matrix4x4.identity;

	void Update () {
        //target.transform.position = portal.TeleportPoint(this.transform.position);
        //target.transform.rotation = portal.TeleportRotation(this.transform.rotation);

        Matrix4x4 worldToLocalMatrix = mat * this.transform.worldToLocalMatrix;

        worldToLocalMatrix *= portal.PortalMatrix().inverse;

        target.worldToCameraMatrix = worldToLocalMatrix;
	}
}
