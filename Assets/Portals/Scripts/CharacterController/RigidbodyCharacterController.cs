using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Portals {
    [RequireComponent(typeof(Rigidbody))]
    public class RigidbodyCharacterController : MonoBehaviour {
        [System.Serializable]
        public struct MovementInfo {
            public float maxSpeedHorizontal;
            public float maxSpeedVertical;
            public float accellerationGrounded;
            public float accellerationAerial;
            public float dragGrounded;
            public float dragAerial;
            public float jumpForce;
        }

        [SerializeField]
        private Transform _head;
        [SerializeField]
        private bool _noClipEnabled = false;
        [SerializeField]
        private float _flySpeed = 10.0f;
        [SerializeField]
        private MovementInfo _movementInfo;
        [SerializeField]
        private float _maxLookAngle = 89f;
        [SerializeField]
        private float _portalLookSnapAngle = 15.0f;
        [SerializeField]
        private float _groundCheckDistance = 0.01f;
        [SerializeField]
        private LayerMask _collisionMask = -1;
        [SerializeField]
        private float _rotationalReallignmentRate = 10.0f;

        private Rigidbody _rigidbody;
        private CapsuleCollider _capsuleCollider;
        private GravityManipulator _gravityManipulator;
        private bool _isGrounded = false;
        private Quaternion _previousHeadRotation;
        private float _xFrameRotation;
        private float _yFrameRotation;

        private Vector3 UpVector {
            get => _gravityManipulator ? _gravityManipulator.upVector : Vector3.up;
        }

        private void Awake() {
            _rigidbody = GetComponent<Rigidbody>();
            _capsuleCollider = GetComponent<CapsuleCollider>();
            _gravityManipulator = GetComponent<GravityManipulator>();
        }

        private void Start() {
            _previousHeadRotation = _head.rotation;
        }

        private void FixedUpdate() {
            DoGroundCheck();
            FixRotation();
            ApplyDrag();
        }

        private void LateUpdate() {
            DoHeadRotation(_xFrameRotation, _yFrameRotation);
            _xFrameRotation = 0;
            _yFrameRotation = 0;
        }

        private void OnPortalTeleport(Portal portal) {
            _previousHeadRotation = portal.TeleportRotation(_previousHeadRotation);
        }

        private void DoHeadRotation(float xRot, float yRot) {
            // Reset head rotation because the head rotates independent of the body
            _head.rotation = _previousHeadRotation;

            // Rotate horizontally
            _head.Rotate(UpVector, xRot, Space.World);

            // Rotate vertically, but clamp to avoid looking higher or lower than 90 degrees
            yRot = ClampRotation(yRot, _maxLookAngle);
            _head.Rotate(_head.right, -yRot, Space.World);

            // Fix head rotation in the case that we are upside down
            Quaternion srcHeadRotation = _head.rotation;
            Quaternion dstHeadRotation = Quaternion.LookRotation(_head.forward, UpVector);
            Quaternion headCorrection = Quaternion.Slerp(srcHeadRotation, dstHeadRotation, _rotationalReallignmentRate * Time.deltaTime);
            _head.rotation = headCorrection;

            // Save head rotation for next frame
            _previousHeadRotation = _head.rotation;
        }

        // TODO: This could work a lot better
        private float ClampRotation(float rotation, float maxRotation) {
            Vector3 centerForward = Vector3.ProjectOnPlane(_head.forward, UpVector);
            float currentAngle = Vector3.SignedAngle(_head.forward, centerForward, _head.right);
            float desiredAngle = currentAngle + rotation;

            if (Mathf.Abs(desiredAngle) > _maxLookAngle) {
                rotation = Mathf.Sign(desiredAngle) * _maxLookAngle - currentAngle;
            }

            return rotation;
        }

        private void DoGroundCheck() {
            Vector3 origin = transform.position;
            float radius = _capsuleCollider.radius;
            Vector3 direction = -1 * UpVector;
            float distance = ((_capsuleCollider.height / 2f) - _capsuleCollider.radius) + _groundCheckDistance;
            int layerMask = _collisionMask.value;
            _isGrounded = Physics.SphereCast(origin, radius, direction, out RaycastHit hit, distance, layerMask, QueryTriggerInteraction.Ignore);
        }

        private void FixRotation() {
            Vector3 dstForwardVector = Vector3.ProjectOnPlane(_head.forward, UpVector);
            Quaternion srcBodyRotation = _rigidbody.rotation;
            Quaternion dstBodyRotation = Quaternion.LookRotation(dstForwardVector, UpVector);

            Quaternion newBodyRotation = Quaternion.Slerp(srcBodyRotation, dstBodyRotation, _rotationalReallignmentRate * Time.deltaTime);
            
            // MoveRotation respects interpolation settings, so this will rotate smoothly
            _rigidbody.MoveRotation(newBodyRotation);
        }

        private void ApplyDrag() {
            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(_rigidbody.velocity, this.UpVector);
            Vector3 verticalVelocity = _rigidbody.velocity - horizontalVelocity;
            _rigidbody.AddForce(-1 * horizontalVelocity * (_isGrounded ? _movementInfo.dragGrounded : _movementInfo.dragAerial), ForceMode.VelocityChange);

            //horizontalVelocity = Vector3.ClampMagnitude(horizontalVelocity, _movementInfo.maxSpeedHorizontal);
            verticalVelocity = Vector3.ClampMagnitude(verticalVelocity, _movementInfo.maxSpeedVertical);

            _rigidbody.velocity = horizontalVelocity + verticalVelocity;
        }

        public void ToggleNoClip() {
            _noClipEnabled = !_noClipEnabled;

            if (_noClipEnabled) {
                _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                _rigidbody.isKinematic = true;
            } else {
                _rigidbody.isKinematic = false;
                _rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }
        }

        public void Move(Vector3 direction) {
            float scaleFactor = this.transform.localScale.x;

            if (_noClipEnabled) {
                transform.position += direction * _flySpeed * scaleFactor * Time.deltaTime;
            } else {
                Vector3 forward = Vector3.ProjectOnPlane(direction, UpVector).normalized;

                Vector3 verticalVelocity = UpVector * Vector3.Dot(UpVector, _rigidbody.velocity);
                Vector3 horizontalVelocity = _rigidbody.velocity - verticalVelocity;

                Vector3 movement = forward * scaleFactor * (_isGrounded ? _movementInfo.accellerationGrounded : _movementInfo.accellerationAerial);

                horizontalVelocity = Vector3.ClampMagnitude(horizontalVelocity + movement, Mathf.Max(horizontalVelocity.magnitude, _movementInfo.maxSpeedHorizontal));
                _rigidbody.velocity = horizontalVelocity + verticalVelocity;
            }
        }

        public void Rotate(float xAngle, float yAngle) {
            _xFrameRotation = xAngle;
            _yFrameRotation = yAngle;
        }

        public void Jump() {
            float scaleFactor = this.transform.localScale.x;
            _rigidbody.AddForce(this.UpVector * _movementInfo.jumpForce * scaleFactor, ForceMode.Acceleration);
        }
    }
}
