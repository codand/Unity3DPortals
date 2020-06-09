using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class SweepTestTest : MonoBehaviour
{
    // Update is called once per frame
    void FixedUpdate()
    {
        var rb = GetComponent<Rigidbody>();
        //RaycastHit[] hits = rb.SweepTestAll(rb.velocity, rb.velocity.magnitude * Time.fixedDeltaTime, QueryTriggerInteraction.Collide);
        RaycastHit[] hits = rb.SweepTestAll(Vector3.down, 1, QueryTriggerInteraction.Collide);
        foreach(var hit in hits) {
            Debug.Log(hit.collider.gameObject.name);
        }
    }
}
