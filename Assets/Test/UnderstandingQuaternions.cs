using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnderstandingQuaternions : MonoBehaviour {
    public Quaternion rotation = Quaternion.identity;
    public Vector3 clampAxis = Vector3.up;

    Quaternion Clamped(Quaternion q, Vector3 axis, float maxAngle) {
        // Normalize input rotation
        Quaternion o = new Quaternion(q.x / q.w, q.y / q.w, q.z / q.w, 1.0f);
        return o;
    }

	void Update () {
        gameObject.transform.rotation = rotation;// Clamped(rotation, clampAxis, 5.0f);
	}
}
