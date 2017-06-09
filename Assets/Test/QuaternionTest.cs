using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class QuaternionTest : MonoBehaviour {
    public float angle;

    Vector3 savedForward = Vector3.forward;

    void OnEnable() {
        savedForward = transform.forward;
    }

    void Update() {
        //Quaternion rotation = Quaternion.Euler(0, angle, 0);
        Quaternion rotation = Quaternion.AngleAxis(angle, transform.up);

        //this.transform.rotation = rotation * Quaternion.LookRotation(savedForward);
        transform.Rotate(transform.up, 1.0f, Space.Self);
    }
}
