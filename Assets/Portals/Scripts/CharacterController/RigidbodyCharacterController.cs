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
        private float _maxLookAngle = 89.5f;
        [SerializeField]
        private float _portalLookSnapAngle = 15.0f;
        [SerializeField]
        private float _groundCheckDistance = 0.01f;
        [SerializeField]
        private LayerMask _collisionMask = -1;
        [SerializeField]
        private bool _correctRotationOnTeleport = true;
        [SerializeField]
        private float _correctRotationDuration = 0.5f;
        [SerializeField]
        private AnimationCurve _correctRotationEasing = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private bool _grounded = false;

        private Rigidbody _rigidbody;
        private CapsuleCollider _capsuleCollider;
        private GravityManipulator _gravityManipulator;

        private Vector3 UpVector {
            get => _gravityManipulator ? _gravityManipulator.upVector : Vector3.up;
        }

        private void Awake() {
            _rigidbody = GetComponent<Rigidbody>();
            _capsuleCollider = GetComponent<CapsuleCollider>();
            _gravityManipulator = GetComponent<GravityManipulator>();
        }

        private void FixedUpdate() {
            RaycastHit hitInfo;
            _grounded = Physics.SphereCast(
                transform.position,
                _capsuleCollider.radius,
                -1 * this.UpVector,
                out hitInfo,
                ((_capsuleCollider.height / 2f) - _capsuleCollider.radius) + _groundCheckDistance,
                _collisionMask.value,
                QueryTriggerInteraction.Ignore);

            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(_rigidbody.velocity, this.UpVector);
            Vector3 verticalVelocity = _rigidbody.velocity - horizontalVelocity;
            _rigidbody.AddForce(-1 * horizontalVelocity * (_grounded ? _movementInfo.dragGrounded : _movementInfo.dragAerial), ForceMode.VelocityChange);

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
                Vector3 moveDir = Vector3.ProjectOnPlane(direction, this.UpVector).normalized;

                Vector3 verticalVelocity = this.UpVector * Vector3.Dot(this.UpVector, _rigidbody.velocity);
                Vector3 horizontalVelocity = _rigidbody.velocity - verticalVelocity;

                Vector3 movement = moveDir * scaleFactor * (_grounded ? _movementInfo.accellerationGrounded : _movementInfo.accellerationAerial);

                horizontalVelocity = Vector3.ClampMagnitude(horizontalVelocity + movement, Mathf.Max(horizontalVelocity.magnitude, _movementInfo.maxSpeedHorizontal));
                _rigidbody.velocity = horizontalVelocity + verticalVelocity;
            }
        }

        public void Rotate(float angle) {
            if (_disableControls) return;

            Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);

            // TODO: This causes jittering
            this.transform.localRotation = this.transform.localRotation * rotation;

            // And this causes lethargic movement
            //_rigidbody.MoveRotation(_rigidbody.rotation * rotation);
        }

        public void RotateHead(float angle) {
            if (!_head) {
                return;
            }

            if (_disableControls) return;

            //float currentAngle = normalizeAngle(_head.localEulerAngles.x, -180, 180);
            //float newAngle = currentAngle - angle;

            //if (newAngle > _maxLookAngle) {
            //    newAngle = _maxLookAngle;
            //} else if (newAngle < -_maxLookAngle) {
            //    newAngle = -_maxLookAngle;
            //}

            _head.localRotation = Quaternion.Euler(_head.localRotation.eulerAngles.x - angle, 0, 0);
            //_head.localRotation *= Quaternion.Euler(-angle, 0, 0);
            _head.localRotation = ClampRotationAroundXAxis(_head.localRotation, -_maxLookAngle, _maxLookAngle);
            //Quaternion rotation = Quaternion.AngleAxis(angle, -Vector3.right);
            //Quaternion desiredRotation = _head.localRotation * rotation;


            //_head.localEulerAngles = eulers;
        }

        public void Jump() {
            float scaleFactor = this.transform.localScale.x;
            _rigidbody.AddForce(this.UpVector * _movementInfo.jumpForce * scaleFactor, ForceMode.Acceleration);
        }

        Coroutine correctRotation = null;
        void OnPortalTeleport(Portal portal) {
            if (_correctRotationOnTeleport) {
                if (correctRotation != null) {
                    StopCoroutine(correctRotation);
                }
                correctRotation = StartCoroutine(CorrectionRotationRoutine(_correctRotationDuration));
            }
        }
        
        bool _disableControls = false;
        IEnumerator CorrectionRotationRoutine(float duration) {
            _disableControls = true;

            float elapsed = 0;
            while (elapsed < duration) {
                float ratio = elapsed / duration;
                float easing = _correctRotationEasing.Evaluate(ratio);
                float angle = CorrectRotation(easing);

                yield return null;
                elapsed += Time.deltaTime;
            }

            CorrectRotation(1.0f);

            _disableControls = false;
        }

        float CorrectRotation(float ratio) {
            // TODO: Make this independent of input
            float xRot = Input.GetAxis("Mouse X") * 3;
            float yRot = Input.GetAxis("Mouse Y") * 3;

            Quaternion frameXRotation = Quaternion.Euler(0, xRot, 0);
            Quaternion frameYRotation = Quaternion.Euler(-yRot, 0, 0);

            // Calculate head rotation
            Quaternion srcHeadRotation = _head.rotation;
            Quaternion dstHeadRotation = Quaternion.LookRotation(_head.forward, Vector3.up);
            float angle = Quaternion.Angle(srcHeadRotation, dstHeadRotation);

            //// Check if our current up angle overshoots our max angle.
            //// If it does, and it's within our defined snap angle, then try to look straight up
            //// instead of flipping the camera.
            //float angleFromTrueUp = Util.SignedPlanarAngle(_head.up, Vector3.up, _head.right);
            //if ((-90 - _portalLookSnapAngle < angleFromTrueUp && angleFromTrueUp < -90) ||
            //    (90 < angleFromTrueUp && angleFromTrueUp < 90 + _portalLookSnapAngle)) {
            //    dstHeadRotation = srcHeadRotation * Quaternion.Euler(-angleFromTrueUp + 0.5f, 0, 0);
            //}

            // Calculate desired body rotation by projecting the desired camera rotation onto the upward plane
            // and using that rotation.
            Vector3 dstForwardVector = Vector3.ProjectOnPlane(dstHeadRotation * Vector3.forward, Vector3.up);
            float dstBodyAngle = MathUtil.SignedPlanarAngle(dstForwardVector, Vector3.forward, Vector3.up);
            Quaternion srcBodyRotation = transform.rotation;
            Quaternion dstBodyRotation = Quaternion.Euler(0, dstBodyAngle, 0);

            transform.rotation = Quaternion.Slerp(srcBodyRotation, dstBodyRotation, ratio) * frameXRotation;
            _head.rotation = Quaternion.Slerp(srcHeadRotation, dstHeadRotation, ratio) * frameYRotation * frameXRotation;

            //// TODO: this doesn't account for body
            //if (Quaternion.Angle(correctedHeadRotation, dstHeadRotation) < 0.1f) {
            //    break;
            //}
            return angle;
        }

        //Quaternion AlignAxis(Quaternion rotation, Vector3 axis) {
        //    Quaternion q;
        //    Vector3 a = Vector3.Cross(3dobject.up, varnormal);
        //    float d = Vector3.Dot(3dobject.up, varnormal);
        //    if (d < 0.0 && a.sqrMagnitude == 0.0) /* Replace with a real epsilon test */
        //    {
        //        q.x = 3dobject.left.x;
        //        q.y = 3dobject.left.y;
        //        q.z = 3dobject.left.z;
        //        q.w = 0.0;
        //    } else {
        //        q.x = a.x;
        //        q.y = a.y;
        //        q.z = a.z;
        //        q.w = Mathf.Sqrt((3dobject.up.sqrMagnitude) * (varnormal.sqrMagnitude)) +d;
        //    }
        //    return q;
        //}

        Quaternion ClampRotationAroundXAxis(Quaternion rotation, float minX, float maxX) {
            rotation.x /= rotation.w;
            rotation.y /= rotation.w;
            rotation.z /= rotation.w;
            rotation.w = 1.0f;

            float angleX = 2.0f * Mathf.Rad2Deg * Mathf.Atan(rotation.x);

            angleX = Mathf.Clamp(angleX, minX, maxX);

            rotation.x = Mathf.Tan(0.5f * Mathf.Deg2Rad * angleX);

            return rotation;
        }
    }
}
