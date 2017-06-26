using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Portals;

[RequireComponent(typeof(Portal))]
public class PortalColliderRaycaster : MonoBehaviour {
    [SerializeField] private LayerMask _layerMask;

    Portal _portal;

    void Awake() {
        _portal = GetComponent<Portal>();
    }

	void FixedUpdate () {
        RaycastHit hit;
        if(Physics.Raycast(transform.position, transform.forward, out hit, 1.0f, _layerMask, QueryTriggerInteraction.Ignore)) { 
            _portal.AttachedCollider = hit.collider;
        } else {
            _portal.AttachedCollider = null;
        }
	}
}
