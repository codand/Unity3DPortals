using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Portals {
    [RequireComponent(typeof(Rigidbody))]
    public class RigidbodyCharacterController : MonoBehaviour {
        [SerializeField]
        private Transform _head;
        [SerializeField]
        private bool _noClipEnabled = false;
        [SerializeField]
        private float _flySpeed = 10.0f;
        [SerializeField]
        private float _moveForce = 40.0f;
        [SerializeField]
        private float _horizontalDrag = 10.0f;
        [SerializeField]
        private float _jumpForce = 250.0f;
        [SerializeField]
        private float _maxLookAngle = 89.5f;

        private Rigidbody _rigidbody;
        private GravityManipulator _gravityManipulator;
        private float _headAngle = 0.0f;

        private Vector3 _upVector {
            get {
                return _gravityManipulator ? _gravityManipulator.upVector : Vector3.up;
            }
        }

        private void Awake() {
            _rigidbody = GetComponent<Rigidbody>();
            _gravityManipulator = GetComponent<GravityManipulator>();
        }

        private void FixedUpdate() {
            // Apply horizontal drag
            Vector3 verticalVelocity = _upVector * Vector3.Dot(_upVector, _rigidbody.velocity);
            Vector3 horizontalVelocity = _rigidbody.velocity - verticalVelocity;

            _rigidbody.AddForce(horizontalVelocity * -1 * _horizontalDrag, ForceMode.Acceleration);
        }

        public void ToggleNoClip() {
            _noClipEnabled = !_noClipEnabled;
            _rigidbody.isKinematic = _noClipEnabled;
        }

        public void Move(Vector3 direction) {
            float scaleFactor = this.transform.localScale.x;

            if (_noClipEnabled) {
                transform.position += direction * _flySpeed * scaleFactor * Time.deltaTime;
            } else {
                Vector3 nInputDir = direction.normalized;
                // Project input direction onto plane whose normal is our upVector
                Vector3 nMoveDir = (nInputDir - _upVector * Vector3.Dot(_upVector, nInputDir)).normalized;
                _rigidbody.AddForce(nMoveDir * _moveForce * scaleFactor, ForceMode.Acceleration);
            }
        }

        public void Rotate(float angle) {
            Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
            this.transform.localRotation = this.transform.localRotation * rotation;
        }

        public void RotateHead(float angle) {
            if (!_head) {
                return;
            }
            _headAngle -= angle;

            if (_headAngle > _maxLookAngle) {
                _headAngle = _maxLookAngle;
            } else if (_headAngle < -_maxLookAngle) {
                _headAngle = -_maxLookAngle;
            }

            _head.localRotation = Quaternion.Euler(_headAngle, 0, 0);
            //Quaternion rotation = Quaternion.AngleAxis(angle, -Vector3.right);
            //Quaternion desiredRotation = _head.localRotation * rotation;


            //_head.localEulerAngles = eulers;
        }

        public void Jump() {
            float scaleFactor = this.transform.localScale.x;
            _rigidbody.AddForce(_upVector * _jumpForce * scaleFactor, ForceMode.Acceleration);
        }
    }
}
