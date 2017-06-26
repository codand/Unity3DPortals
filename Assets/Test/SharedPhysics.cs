using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SharedPhysics : MonoBehaviour {
    public bool isMaster;
    public List<SharedPhysics> others;

    public Vector3 impulseSumTranslational;
    public Vector3 impulseSumRotational;
    public bool pauseOnCollision = false;
    public float lerpRate = 15;

    Rigidbody _rigidbody;

    Dictionary<SharedPhysics, Vector3> initialOffsets;
    SharedPhysics _master;

    void Awake() {
        _rigidbody = GetComponent<Rigidbody>();
        StartCoroutine(LateFixedUpdateRoutine());

        initialOffsets = new Dictionary<SharedPhysics, Vector3>();
        foreach (SharedPhysics other in others) {
            initialOffsets[other] = other.transform.position - this.transform.position;
            if(other.isMaster) {
                _master = other;
            }
        }
        if (isMaster) {
            _master = this;
        }
    }

    IEnumerator LateFixedUpdateRoutine() {
        while (true) {
            yield return new WaitForFixedUpdate();
            LateFixedUpdate();
        }
    }

    void FixedUpdate() {
        impulseSumTranslational = Vector3.zero;
        impulseSumRotational = Vector3.zero;

        if (_rigidbody.useGravity) {
            impulseSumTranslational += Physics.gravity * Time.deltaTime;
        }

        if (isMaster) {
            foreach (SharedPhysics slave in others) {
                if (slave.isActiveAndEnabled) {
                    Vector3 newPosition = this._rigidbody.position + initialOffsets[slave];
                    Quaternion newRotation = this._rigidbody.rotation;
                    slave._rigidbody.MovePosition(newPosition);
                    slave._rigidbody.MoveRotation(newRotation);
                    slave._rigidbody.velocity = _rigidbody.velocity;
                    slave._rigidbody.angularVelocity = _rigidbody.angularVelocity;
                }
            }
        }
        //Debug.Log(name + " velocity: " + _rigidbody.velocity);
    }

    void LateUpdate() {

        if (isMaster) {
            foreach (SharedPhysics slave in others) {
                if (slave.isActiveAndEnabled) {
                    Vector3 newPosition = this._rigidbody.position + initialOffsets[slave];
                    Quaternion newRotation = this._rigidbody.rotation;

                    slave.transform.position = newPosition;
                    slave.transform.rotation = newRotation;
                }
            }
        }
    }

    void OnCollisionEnter(Collision collision) {
        if (pauseOnCollision) {
            UnityEditor.EditorApplication.isPaused = true;
        }
        HandleCollision(collision);
    }

    void OnCollisionStay(Collision collision) {
        HandleCollision(collision);
    }

    void HandleCollision(Collision collision) {
        Vector3 positionSum = Vector3.zero;
        Vector3 normalSum = Vector3.zero;
        foreach (ContactPoint contact in collision.contacts) {
            positionSum += contact.point;
            normalSum += contact.normal;
        }
        Vector3 averageContactPoint = positionSum / collision.contacts.Length;
        Vector3 averageNormal = normalSum / collision.contacts.Length;

        Vector3 impulse = collision.impulse;
        if (Vector3.Dot(impulse, averageNormal) < 0) {
            impulse *= -1;
        }
        
        Vector3 rotationImpulse = Vector3.Cross(averageContactPoint - _rigidbody.worldCenterOfMass, impulse);
        impulseSumTranslational += impulse;
        impulseSumRotational += rotationImpulse;
    }

    Vector3 CalculateImpulseTransfer(Vector3 imp1, Vector3 imp2) {
        Vector3 impParallel = Vector3.Project(imp1, imp2);
        Vector3 impPerpendicular = imp1 - impParallel;
        Vector3 impTransfer = impPerpendicular;
        float magnitude = Vector3.Dot(imp1, imp2.normalized);
        if (magnitude < 0) {
            impTransfer += impParallel;
        } else if (magnitude > imp2.magnitude) {
            impTransfer += (impParallel - imp2);
        }


        //Vector3 impTransfer = imp1 - imp2;

        Debug.DrawLine(transform.position, transform.position + impParallel, Color.yellow);
        Debug.DrawLine(transform.position, transform.position + impPerpendicular, Color.blue);
        Debug.DrawLine(transform.position, transform.position + imp1, Color.cyan);
        Debug.DrawLine(transform.position, transform.position + imp2, Color.white);
        Debug.DrawLine(transform.position, transform.position + impTransfer, Color.black);

        //Debug.Log("Transfering: " + impTransfer);
        return impTransfer;
    }

    void LateFixedUpdate() {
        if (!isMaster) {
            foreach (SharedPhysics other in others) {
                other._rigidbody.velocity += CalculateImpulseTransfer(impulseSumTranslational, other.impulseSumTranslational);
                other._rigidbody.angularVelocity += CalculateImpulseTransfer(impulseSumRotational, other.impulseSumRotational);
            }
        }
    }
}
