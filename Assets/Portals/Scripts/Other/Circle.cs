using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Circle : MonoBehaviour {
    [SerializeField] private float _spinSpeed = 2;
    [SerializeField] private Transform pivot;

    void Update() {
        transform.RotateAround(pivot.position, pivot.forward, _spinSpeed * Time.deltaTime);
    }
}
