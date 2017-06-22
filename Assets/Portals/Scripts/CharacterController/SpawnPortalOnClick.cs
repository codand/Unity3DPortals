using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Portals;

public class SpawnPortalOnClick : MonoBehaviour {
    [SerializeField] GameObject _portalPrefab;
    [SerializeField] LayerMask _canHit = -1;

    Portal _leftPortal;
    Portal _rightPortal;

    void Awake() {
        if (!isActiveAndEnabled) {
            return;
        }
    }

    void Start() {
        if (!isActiveAndEnabled) {
            return;
        }

        _leftPortal = SpawnPortal(Vector3.zero, Quaternion.identity);
        _rightPortal = SpawnPortal(Vector3.zero, Quaternion.identity);

        _leftPortal.ExitPortal = _rightPortal;
        _rightPortal.ExitPortal = _leftPortal;

        _leftPortal.name = "Left Portal";
        _rightPortal.name = "Right Portal";

        _leftPortal.gameObject.SetActive(false);
        _rightPortal.gameObject.SetActive(false);

        ParticleSystem particles = _leftPortal.GetComponentInChildren<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.startColor = Color.red;
    }

    Portal SpawnPortal(Vector3 location, Quaternion rotation) {
        GameObject obj = Instantiate(_portalPrefab, location, rotation);
        Portal portal = obj.GetComponent<Portal>();

        return portal;
    }

    Quaternion CalculateRotation(Vector3 forward, Vector3 normal) {
        Vector3 forwardOnPlane = Vector3.Cross(-normal, Vector3.right);
        Vector3 projectedForward = forward - Vector3.Dot(forward, normal) * normal;
        Quaternion faceCamera = Quaternion.FromToRotation(forwardOnPlane, projectedForward);
        if (Mathf.Abs(normal.y) < 0.999f) {
            faceCamera = Quaternion.identity;
        }
        Quaternion alongNormal = Quaternion.LookRotation(-normal);
        Quaternion rotation = faceCamera * alongNormal;
        return rotation;
    }

    void Update () {
        bool leftClick = Input.GetMouseButtonDown(0);
        bool rightClick = Input.GetMouseButtonDown(1);

        if (leftClick || rightClick) {
            Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, _canHit)) {
                
                Portal portal = leftClick ? _leftPortal : _rightPortal;


                Quaternion rotation = CalculateRotation(Camera.main.transform.forward, hit.normal);
                portal.transform.position = hit.point;
                portal.transform.rotation = rotation;

                portal.AttachedCollider = hit.collider;

                portal.gameObject.SetActive(true);
            }
        }
	}
}
