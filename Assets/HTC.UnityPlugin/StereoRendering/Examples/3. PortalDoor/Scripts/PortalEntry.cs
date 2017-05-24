//========= Copyright 2016, HTC Corporation. All rights reserved. ===========

using UnityEngine;
using HTC.UnityPlugin.StereoRendering;

public class PortalEntry : MonoBehaviour
{
    public Collider playerCollider;

    public GameObject hmdRig;
    public GameObject hmdEye;
    public StereoRenderer stereoRenderer;

    /////////////////////////////////////////////////////////////////////////////////

    void OnTriggerEnter(Collider other)
    {
        // if hmd has collided with portal door
        if (other == playerCollider)
        {
            stereoRenderer.shouldRender = false;

            // adjust rotation
            Quaternion rotEntryToExit = stereoRenderer.anchorRot * Quaternion.Inverse(stereoRenderer.canvasOriginRot);
            hmdRig.transform.rotation = rotEntryToExit * hmdRig.transform.rotation;

            // adjust position
            Vector3 posDiff = new Vector3(stereoRenderer.stereoCameraHead.transform.position.x - hmdEye.transform.position.x,
                                          stereoRenderer.stereoCameraHead.transform.position.y - hmdEye.transform.position.y,
                                          stereoRenderer.stereoCameraHead.transform.position.z - hmdEye.transform.position.z);
            Vector3 camRigTargetPos = hmdRig.transform.position + posDiff;

            // assign the target position to camera rig
            hmdRig.transform.position = camRigTargetPos;

            stereoRenderer.shouldRender = true;
        }
    }
}
