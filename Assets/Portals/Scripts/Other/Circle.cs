using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Circle : MonoBehaviour {
    [SerializeField] float _spinSpeed = 2;
    [SerializeField] float _offset = 0;

    Vector3 _savedPosition;

	void Start () {
        _savedPosition = transform.position;
	}
	
	void Update () {
        float dz = Mathf.Cos(Time.time * _spinSpeed + Mathf.Deg2Rad * _offset);
        float dy = Mathf.Sin(Time.time * _spinSpeed + Mathf.Deg2Rad * _offset);
        Vector3 newPos = _savedPosition + new Vector3(0, dy, dz);
        transform.position = new Vector3(transform.position.x, newPos.y, newPos.z);
	}
}
