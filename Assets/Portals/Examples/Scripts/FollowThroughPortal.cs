using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Portals;

public class FollowThroughPortal : MonoBehaviour {
    [SerializeField] Teleportable _target;
    [SerializeField] Transform _cameraRoot;
    [SerializeField] float _followSpeed = 5.0f;

    Portal _portal;
    MonoBehaviour[] _rootBehaviours;

    Vector3 _defaultPosition;
    Quaternion _defaultRotation;

    void Awake() {
        _defaultPosition = transform.localPosition;
        _defaultRotation = transform.localRotation;

        _rootBehaviours = _cameraRoot.GetComponents<MonoBehaviour>();
    }

    void OnEnable() {
        _target.OnTeleport += OnTargetTeleported;
    }

    void OnDisable() {
        _target.OnTeleport -= OnTargetTeleported;
    }

    void OnTargetTeleported(Teleportable sender, Portal portal) {
        if (portal == portal.ExitPortal) {
            // Player walked back out of the portal
            portal = null;
            SetScriptsEnabled(true);
        } else {
            // Player walked into a new portal
            _portal = portal;
            SetScriptsEnabled(false);
        }
    }

    void SetScriptsEnabled(bool enable) {
        foreach (MonoBehaviour script in _rootBehaviours) {
            script.enabled = enable;
        }
    }

    void FixedUpdate() {
        if (_portal) {
            Vector3 srcPosition = transform.position;
            Vector3 dstPosition = _portal.transform.position + _portal.transform.forward;
            transform.position = Vector3.Lerp(srcPosition, dstPosition, Time.deltaTime * _followSpeed);

            Vector3 lookAt = _portal.InverseTeleportPoint(_target.transform.position);
            Quaternion srcRotation = transform.rotation;
            Quaternion dstRotation = Quaternion.LookRotation(lookAt - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, dstRotation, Time.deltaTime * _followSpeed);

            if (_portal.Plane.GetSide(transform.position)) {
                _cameraRoot.position = _portal.TeleportPoint(_cameraRoot.position);
                _cameraRoot.rotation = _portal.TeleportRotation(_cameraRoot.rotation);
                _portal = null;
                SetScriptsEnabled(true);
            }
        } else {
            transform.localPosition = Vector3.Lerp(transform.localPosition, _defaultPosition, Time.deltaTime * _followSpeed);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, _defaultRotation, Time.deltaTime * _followSpeed);
        }
    }
}
