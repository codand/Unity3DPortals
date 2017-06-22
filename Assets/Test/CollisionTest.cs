using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CollisionTest : MonoBehaviour {
	void OnCollisionEnter(Collision collision) {
        if (collision.impulse.magnitude > 5f) {
            Debug.Log(collision.collider.name + collision.impulse);
            UnityEditor.EditorApplication.isPaused = true;
        }
    }
}
