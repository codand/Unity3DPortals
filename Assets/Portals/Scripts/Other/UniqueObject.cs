using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UniqueObject : MonoBehaviour {
    static UniqueObject _instance;
    public static UniqueObject instance {
        get {
            if (!_instance) {
                _instance = FindObjectOfType<UniqueObject>();
            }
            return _instance;
        }
        set {
            _instance = value;
        }
    }
    void Awake() {
        if (UniqueObject.instance != this) {
            Destroy(this.gameObject);
        }
    }

    void OnDestroy() {
        if (UniqueObject.instance == this) {
            UniqueObject.instance = null;
        }
    }
}
