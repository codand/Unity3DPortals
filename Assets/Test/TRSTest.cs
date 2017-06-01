using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class TRSTest : MonoBehaviour {
    public Vector3 translation;
    public Vector3 rotation;
    public Vector3 scale = Vector3.one;

    public Transform origin;
    public Transform target;

	void Update () {
        Matrix4x4 trs = Matrix4x4.TRS(translation, Quaternion.Euler(rotation), scale);
        this.transform.position = trs.MultiplyPoint (target.position);
	}
}
