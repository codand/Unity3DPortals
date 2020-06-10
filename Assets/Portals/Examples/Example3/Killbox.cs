using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Killbox : MonoBehaviour
{
    private void OnTriggerEnter(Collider other) {
        other.transform.position = Vector3.zero;
        other.transform.rotation = Quaternion.identity;
        other.transform.localScale = Vector3.one;
        var rb = other.GetComponent<Rigidbody>();
        if (rb) {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.mass = 1;
        }
    }
}
