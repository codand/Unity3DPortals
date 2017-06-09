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

        private void Awake() {
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void FixedUpdate() {
            // Apply horizontal drag
            Vector3 verticalVelocity = Vector3.up * Vector3.Dot(Vector3.up, _rigidbody.velocity);
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
                Vector3 nMoveDir = (nInputDir - Vector3.up * Vector3.Dot(Vector3.up, nInputDir)).normalized;
                _rigidbody.AddForce(nMoveDir * _moveForce * scaleFactor, ForceMode.Acceleration);
            }
        }

        public void Rotate(float angle) {
            if (disable_stuff) return;
            Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
            this.transform.localRotation = this.transform.localRotation * rotation;
        }

        public void RotateHead(float angle) {
            if (!_head) {
                return;
            }
            //_headAngle -= angle;

            if (disable_stuff) return;

            float currentAngle = normalizeAngle(_head.localEulerAngles.x, -180, 180);
            float newAngle = currentAngle - angle;

            if (newAngle > _maxLookAngle) {
                newAngle = _maxLookAngle;
            } else if (newAngle < -_maxLookAngle) {
                newAngle = -_maxLookAngle;
            }

            _head.localRotation = Quaternion.Euler(newAngle, 0, 0);
            //Quaternion rotation = Quaternion.AngleAxis(angle, -Vector3.right);
            //Quaternion desiredRotation = _head.localRotation * rotation;


            //_head.localEulerAngles = eulers;
        }

        public void Jump() {
            float scaleFactor = this.transform.localScale.x;
            _rigidbody.AddForce(Vector3.up * _jumpForce * scaleFactor, ForceMode.Acceleration);
        }

        Coroutine correctRotation = null;
        void OnPortalExit(Portal portal) {
            if (correctRotation != null) {
                StopCoroutine(correctRotation);
            }
            correctRotation = StartCoroutine(CorrectionRotation(1.0f));
        }

        public Transform axis1, axis2;
        bool disable_stuff = false;
        IEnumerator CorrectionRotation(float duration) {
            disable_stuff = true;

            float timeScale = 0.25f;
            Time.timeScale = timeScale;
            Time.fixedDeltaTime *= timeScale;

            float elapsed = 0;
            while (elapsed < duration) {
                float ratio = elapsed / duration;

                // TODO: Make this independent of input
                float xRot = Input.GetAxis("Mouse X") * 3;
                float yRot = Input.GetAxis("Mouse Y") * 3;

                //
                // Head rotation
                //
                Quaternion srcHeadRotation = _head.rotation;
                Quaternion dstHeadRotation = Quaternion.LookRotation(_head.forward, Vector3.up);

                //float a = SignedAngle(_head.forward, Vector3.up, Vector3.Cross(_head.forward, Vector3.up));
                //Debug.Log(a);
                //if (a < 35) {
                //    dstHeadRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(-_head.forward, Vector3.up), Vector3.up) * Quaternion.Euler(-89.5f, 0, 0);
                //}

                //axis1.transform.position = _head.transform.position;
                //axis2.transform.position = _head.transform.position;
                //axis1.transform.rotation = srcHeadRotation;
                //axis2.transform.rotation = dstHeadRotation;

                Quaternion frameBodyRotation = Quaternion.Euler(0, xRot, 0);
                Quaternion frameHeadRotation = Quaternion.Euler(-yRot, 0, 0);
                Quaternion correctedHeadRotation = Quaternion.Lerp(srcHeadRotation, dstHeadRotation, ratio);
                _head.rotation = correctedHeadRotation * frameHeadRotation * frameBodyRotation;

                //
                // Body rotation (dependent on head's rotation)
                //
                Vector3 projected = Vector3.ProjectOnPlane(_head.forward, Vector3.up);
                float angle = SignedAngle(projected, Vector3.forward, Vector3.up);

                Quaternion srcBodyRotation = transform.rotation;
                Quaternion dstBodyRotation = Quaternion.Euler(0, angle, 0);

                // Save head rotation because modifying this transform also modifies the head's
                Quaternion headRotation = _head.rotation;

                // Set body rotation
                transform.rotation = Quaternion.Lerp(srcBodyRotation, dstBodyRotation, ratio);

                // Restore head rotation
                _head.rotation = headRotation;

                // TODO: this doesn't account for body
                if (Quaternion.Angle(correctedHeadRotation, dstHeadRotation) < 0.1f) {
                    break;
                }

                yield return null;
                elapsed += Time.deltaTime;
            }

            Time.timeScale = 1.0f;
            Time.fixedDeltaTime /= timeScale;
            disable_stuff = false;
        }

        float normalizeAngle(float angle, float start, float end) {
            float width = end - start;
            float offsetValue = angle - start;

            return (offsetValue - (Mathf.Floor(offsetValue / width) * width)) + start;
        }

        float SignedAngle(Vector3 a, Vector3 b, Vector3 normal) {
            float angle = Vector3.Angle(a, b);
            Vector3 cross = Vector3.Cross(a, b);
            if (Vector3.Dot(normal, cross) > 0) {
                angle *= -1;
            }
            return angle;
        }
    }
}
