using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Portals;

public class ResetIfAtRest : MonoBehaviour
{
    [SerializeField] private Vector3 _launchForce;

    private Vector3 _startPosition;
    private Vector3 _startScale;
    private Quaternion _startRotation;
    private float _startMass;

    private Rigidbody _rigidbody;
    private Vector3 _prevVelocity;

    private const float SleepThreshold = 0.0001f;
    
    private void Awake() {
        _rigidbody = GetComponent<Rigidbody>();
    }
    void Start()
    {
        _startPosition = _rigidbody.position;
        _startRotation = _rigidbody.rotation;
        _startScale = transform.localScale;
        _startMass = _rigidbody.mass;
        Reset();
    }

    void FixedUpdate()
    {
        // Rigidbody.IsSleeping doesn't work because we are applying our own gravity, so just check when
        // then change in velocity is very low.
        Vector3 dv = _rigidbody.velocity - _prevVelocity;
        if (dv.magnitude < SleepThreshold) {
            Reset();
        }
        _prevVelocity = _rigidbody.velocity;
    }

    private void Reset() {
        transform.localScale = _startScale;
        _rigidbody.position = _startPosition;
        _rigidbody.rotation = _startRotation;
        _rigidbody.mass = _startMass;
        _rigidbody.AddForce(_launchForce);

        var gravityManipulator = GetComponent<GravityManipulator>();
        if (gravityManipulator) {
            gravityManipulator.upVector = Vector3.up;
        }
    }
}
