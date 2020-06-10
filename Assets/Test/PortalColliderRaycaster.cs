using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Portals;
using System.Linq;

[RequireComponent(typeof(Portal))]
public class PortalColliderRaycaster : MonoBehaviour {
    public enum UpdateMode {
        Start,
        FixedUpdate,
    }
    [SerializeField] private UpdateMode _updateMode = UpdateMode.Start;
    [SerializeField] private LayerMask _layerMask;
    [SerializeField] private float _boxcastDistance = 5.0f;
    [SerializeField] private float _boxcastScaleMultiplier = 0.99f;

    Portal _portal;
    Collider _collider;

    private void Awake() {
        _portal = GetComponent<Portal>();
        _collider = GetComponent<Collider>();
    }

    private void Start() {
        if (_updateMode == UpdateMode.Start) {
            UpdateColliders();
        }
    }

    private void FixedUpdate() {
        if (_updateMode == UpdateMode.FixedUpdate) {
            UpdateColliders();
        }
    }

    private void UpdateColliders() {
        Vector3 position = transform.position - transform.forward * _collider.bounds.extents.z;
        Vector3 extents = _collider.bounds.extents * _boxcastScaleMultiplier;
        Quaternion rotation = transform.rotation;
        Vector3 direction = transform.forward;
        float distance = _boxcastDistance;

        Debug.DrawLine(position, position + direction * distance);
        RaycastHit[] hits = Physics.BoxCastAll(position, extents, direction, rotation, distance, _layerMask, QueryTriggerInteraction.Ignore);
        _portal.IgnoredColliders = hits.Select(hit => hit.collider).ToArray();
    }
}
