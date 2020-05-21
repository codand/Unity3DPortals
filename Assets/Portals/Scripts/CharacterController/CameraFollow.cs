using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform _target;

    void LateUpdate()
    {
        transform.position = _target.position;
        Debug.Log(transform.position.x);
    }
}
