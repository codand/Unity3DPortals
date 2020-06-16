using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Portals.Example {
    [RequireComponent(typeof(Camera))]
    public class ShootBall : MonoBehaviour {
        [SerializeField] GameObject _ballPrefab;
        [SerializeField] float _fireSpeed = 10.0f;
        [SerializeField] KeyCode _fireKey = KeyCode.LeftShift;
        [SerializeField] float _destroyAfter = 15.0f;

        private Camera _camera;

        private void Awake() {
            _camera = GetComponent<Camera>();
        }

        private void Update() {
            if (Input.GetKeyDown(_fireKey)) {
                GameObject ball = Instantiate(_ballPrefab);
                ball.transform.position = _camera.transform.position + _camera.transform.forward;

                Rigidbody rigidbody = ball.GetComponent<Rigidbody>();
                rigidbody.velocity = _camera.transform.forward * _fireSpeed;

                Destroy(ball, _destroyAfter);
            }
        }
    }
}
