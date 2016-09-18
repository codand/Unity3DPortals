using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisableTargetWhenInvisible : MonoBehaviour {
    [SerializeField] GameObject _target;

    void OnBecameInvisible() {
        _target.SetActive(false);
    }

    void OnBecameVisible() {
        _target.SetActive(true);
    }
}
