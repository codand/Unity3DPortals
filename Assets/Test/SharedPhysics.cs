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

    RigidbodyInfo _rigidbodyInfo;
    public float forceMultiplier = 1.0f;

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


    //void OnCollisionEnter() {
    //    if (isMaster) {
    //        UnityEditor.EditorApplication.isPaused = true;
    //    }
    //}

    void LateUpdate() {
        if (isMaster) {
            foreach (SharedPhysics slave in others) {
                Vector3 offset = initialOffsets[slave];

                // Lock clone to me
                slave.transform.position = transform.position + offset;
                slave.transform.rotation = transform.rotation;
            }
        }
    }


    void FixedUpdate() {
        if (isMaster) {
            //_rigidbody.velocity += Vector3.back * 10;
            //_rigidbody.AddForce(Vector3.back * forceMultiplier, ForceMode.VelocityChange);

            foreach (SharedPhysics slave in others) {
                Vector3 offset = initialOffsets[slave];

                // Lock clone to master
                slave._rigidbody.position = _rigidbody.position + offset;
                slave._rigidbody.rotation = _rigidbody.rotation;
                slave._rigidbody.velocity = _rigidbody.velocity;
                slave._rigidbody.angularVelocity = _rigidbody.angularVelocity;

                // Save clone's modified state
                slave.SaveRigidbodyInfo();
            }

            // Save master modified state
            SaveRigidbodyInfo();
        }
    }

    void LateFixedUpdate() {
        if (isMaster) {
            foreach (SharedPhysics slave in others) {
                Vector3 offset = initialOffsets[slave];

                // Apply velocity restrictions to master
                Vector3 slaveDeltaVelocity = slave._rigidbody.velocity - slave._rigidbodyInfo.velocity;
                Vector3 masterDeltaVelocity = _rigidbody.velocity - _rigidbodyInfo.velocity;

                Vector3 slaveDeltaPosition = slave._rigidbody.position - slave._rigidbodyInfo.position;
                Vector3 masterDeltaPosition = _rigidbody.position - _rigidbodyInfo.position;

                Vector3 slaveDeltaAngularVelocity = slave._rigidbody.angularVelocity - slave._rigidbodyInfo.angularVelocity;
                Vector3 masterDeltaAngularVelocity = _rigidbody.angularVelocity - _rigidbodyInfo.angularVelocity;

                Quaternion slaveDeltaRotation = slave._rigidbody.rotation * Quaternion.Inverse(slave._rigidbodyInfo.rotation);
                Quaternion masterDeltaRotation = _rigidbody.rotation * Quaternion.Inverse(_rigidbodyInfo.rotation);

                Vector3 velocityTransfer = CalculateImpulseTransfer(slaveDeltaVelocity, masterDeltaVelocity);
                Vector3 positionTransfer = CalculateImpulseTransfer(slaveDeltaPosition, masterDeltaPosition);
                Vector3 angularVelocityTransfer = CalculateImpulseTransfer(slaveDeltaAngularVelocity, masterDeltaAngularVelocity);
                Quaternion rotationTransfer = slaveDeltaRotation * Quaternion.Inverse(masterDeltaRotation);

                _rigidbody.velocity += velocityTransfer;
                _rigidbody.position += positionTransfer;
                _rigidbody.angularVelocity += angularVelocityTransfer;
                _rigidbody.rotation *= rotationTransfer;
            }
        }
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

        return impTransfer;
    }
    private void SaveRigidbodyInfo() {
        _rigidbodyInfo.position = _rigidbody.position;
        _rigidbodyInfo.rotation = _rigidbody.rotation;
        _rigidbodyInfo.velocity = _rigidbody.velocity;
        _rigidbodyInfo.angularVelocity = _rigidbody.angularVelocity;
    }

    private struct RigidbodyInfo {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public Vector3 angularVelocity;
        public Vector3 translationalImpulse;
        public Vector3 angularImpulse;
    }

    //void FixedUpdate() {
    //    impulseSumTranslational = Vector3.zero;
    //    impulseSumRotational = Vector3.zero;

    //    if (_rigidbody.useGravity) {
    //        impulseSumTranslational += Physics.gravity * Time.deltaTime;
    //    }

    //    if (isMaster) {
    //        foreach (SharedPhysics slave in others) {
    //            if (slave.isActiveAndEnabled) {
    //                Vector3 newPosition = this._rigidbody.position + initialOffsets[slave];
    //                Quaternion newRotation = this._rigidbody.rotation;
    //                slave._rigidbody.MovePosition(newPosition);
    //                slave._rigidbody.MoveRotation(newRotation);
    //                slave._rigidbody.velocity = _rigidbody.velocity;
    //                slave._rigidbody.angularVelocity = _rigidbody.angularVelocity;
    //            }
    //        }
    //    }
    //    //Debug.Log(name + " velocity: " + _rigidbody.velocity);
    //}

    //void LateUpdate() {

    //    if (isMaster) {
    //        foreach (SharedPhysics slave in others) {
    //            if (slave.isActiveAndEnabled) {
    //                Vector3 newPosition = this._rigidbody.position + initialOffsets[slave];
    //                Quaternion newRotation = this._rigidbody.rotation;

    //                slave.transform.position = newPosition;
    //                slave.transform.rotation = newRotation;
    //            }
    //        }
    //    }
    //}

    //void OnCollisionEnter(Collision collision) {
    //    if (pauseOnCollision) {
    //        UnityEditor.EditorApplication.isPaused = true;
    //    }
    //    HandleCollision(collision);
    //}

    //void OnCollisionStay(Collision collision) {
    //    HandleCollision(collision);
    //}

    //void HandleCollision(Collision collision) {
    //    Vector3 positionSum = Vector3.zero;
    //    Vector3 normalSum = Vector3.zero;
    //    foreach (ContactPoint contact in collision.contacts) {
    //        positionSum += contact.point;
    //        normalSum += contact.normal;
    //    }
    //    Vector3 averageContactPoint = positionSum / collision.contacts.Length;
    //    Vector3 averageNormal = normalSum / collision.contacts.Length;

    //    Vector3 impulse = collision.impulse;
    //    if (Vector3.Dot(impulse, averageNormal) < 0) {
    //        impulse *= -1;
    //    }

    //    Vector3 rotationImpulse = Vector3.Cross(averageContactPoint - _rigidbody.worldCenterOfMass, impulse);
    //    impulseSumTranslational += impulse;
    //    impulseSumRotational += rotationImpulse;
    //}


    //void LateFixedUpdate() {
    //    if (!isMaster) {
    //        foreach (SharedPhysics other in others) {
    //            other._rigidbody.velocity += CalculateImpulseTransfer(impulseSumTranslational, other.impulseSumTranslational);
    //            other._rigidbody.angularVelocity += CalculateImpulseTransfer(impulseSumRotational, other.impulseSumRotational);
    //        }
    //    }
    //}
}
