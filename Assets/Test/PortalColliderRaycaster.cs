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
        RaycastHit[] hits = Physics.BoxCastAll(transform.position, _collider.bounds.extents / 2, transform.forward, transform.rotation, _boxcastDistance, _layerMask, QueryTriggerInteraction.Ignore);
        _portal.IgnoredColliders = hits.Select(hit => hit.collider).ToArray();
    }
}
