using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerTest : MonoBehaviour {
    void OnTriggerEnter(Collider collider) {
        Debug.Log("Entered: " + collider.name);
    }
}
