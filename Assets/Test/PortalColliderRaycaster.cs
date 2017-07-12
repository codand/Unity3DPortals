using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Portals;
using System.Linq;

[RequireComponent(typeof(Portal))]
public class PortalColliderRaycaster : MonoBehaviour {
    [SerializeField] private LayerMask _layerMask;
    [SerializeField] private float _boxcastDistance = 5.0f;

    Portal _portal;
    Collider _collider;

    void Awake() {
        _portal = GetComponent<Portal>();
        _collider = GetComponent<Collider>();
    }

    void FixedUpdate() {
        Vector3 position = transform.position - transform.forward * _collider.bounds.extents.z;
        Vector3 extents = _collider.bounds.extents;
        Quaternion rotation = transform.rotation;
        Vector3 direction = transform.forward;
        float distance = _boxcastDistance;

        Debug.DrawLine(position, position + direction * distance);
        RaycastHit[] hits = Physics.BoxCastAll(position, extents, direction, rotation, distance, _layerMask, QueryTriggerInteraction.Ignore);
        _portal.IgnoredColliders = hits.Select(hit => hit.collider).ToArray();
    }
}
