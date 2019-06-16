using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Portals {
    public class VelocityBasedFieldOfView : MonoBehaviour {
        [SerializeField]
        private Rigidbody _rigidbody;

        [SerializeField]
        private float _triggerSpeed = 3.0f;

        [SerializeField]
        private float _maxSpeedModified = 10.0f;

        [SerializeField]
        private float _fovAmplification = 1.5f;

        [SerializeField]
        private float _smoothness = 10.0f;

        private float _defaultFoV;
        private Camera _camera;

        private void Start() {
            _camera = GetComponent<Camera>();
            _defaultFoV = _camera.fieldOfView;
        }

        private void Update() {
            if (_camera.stereoEnabled) {
                Debug.LogError("Setting field of view not supported in VR. Disabling script.");
                this.enabled = false;
                return;
            }
            float speed = Vector3.Dot(_rigidbody.velocity, _camera.transform.forward);

            float ratio = Mathf.Clamp01((speed - _triggerSpeed) / (_maxSpeedModified - _triggerSpeed));

            float minFoV = _defaultFoV;
            float maxFoV = _defaultFoV * _fovAmplification;

            float srcFoV = _camera.fieldOfView;
            float dstFoV = Mathf.Lerp(minFoV, maxFoV, ratio);

            _camera.fieldOfView = Mathf.Lerp(srcFoV, dstFoV, _smoothness * Time.deltaTime);
        }
    }
}
