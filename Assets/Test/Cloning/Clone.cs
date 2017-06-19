using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

/**

    Step 1: Clone game object
    Step 2: Resolve references

    A reference contains a target Transform + Component

**/

public class Clone : MonoBehaviour {
    public GameObject target;

    private static List<Type> _validBehaviours = new List<Type>(){
            typeof(Animator),
            typeof(MeshFilter),
            typeof(MeshRenderer),
            typeof(SkinnedMeshRenderer),
        };

    void OnEnable() {
        CloneObject(target);
    }

    private static GameObject CloneObject(GameObject obj) {
        GameObject clone = Instantiate(obj);
        ScrubCloneRecursively(clone);
        return clone;
    }

    private static void ScrubCloneRecursively(GameObject obj) {
        Behaviour[] allBehaviours = obj.GetComponents<Behaviour>();
        foreach (Behaviour behaviour in allBehaviours) {
            if (!_validBehaviours.Contains(behaviour.GetType())) {
                behaviour.enabled = false;
            }
        }
        foreach (Transform child in obj.transform) {
            ScrubCloneRecursively(child.gameObject);
        }
    }
}
