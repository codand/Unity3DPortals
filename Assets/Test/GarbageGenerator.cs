using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GarbageGenerator : MonoBehaviour {

	void OnWillRenderObject() {
        for(int i = 0; i < 10000; i++) {
            new List<int>();
        }
    }
}
