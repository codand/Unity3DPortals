using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Portals {
    public class VelocityBasedFieldOfView : MonoBehaviour {
        [SerializeField]
        private Rigidbody _rigidbody;

        [SerializeField]
        private float _minSpeed = 10.0f;

        [SerializeField]
        private float _maxSpeed = 40.0f;

        [SerializeField]
        private float _fovAmplification = 1.5f;

        [SerializeField]
        private float _smoothness = 10.0f;

        private float _defaultFoV;
        private Camera _camera;

        private float ScaleMultiplier {
            get => transform.lossyScale.x;
        }

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
            float speedMin = _minSpeed * ScaleMultiplier;
            float speedMax = _maxSpeed * ScaleMultiplier;
            float ratio = Mathf.Clamp01((speed - speedMin) / (speedMax - speedMin));

            float minFoV = _defaultFoV;
            float maxFoV = _defaultFoV * _fovAmplification;

            float srcFoV = _camera.fieldOfView;
            float dstFoV = Mathf.Lerp(minFoV, maxFoV, ratio);

            _camera.fieldOfView = Mathf.Lerp(srcFoV, dstFoV, _smoothness * Time.deltaTime);
        }
    }
}
