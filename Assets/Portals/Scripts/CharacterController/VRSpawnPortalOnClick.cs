using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Portals;

public class VRSpawnPortalOnClick : MonoBehaviour {
    [SerializeField]
    GameObject _bulletPrefab;
    [SerializeField]
    float _bulletSpawnOffset = 3.0f;
    [SerializeField]
    float _bulletSpeed = 20.0f;
    [SerializeField]
    GameObject _portalPrefab;
    [SerializeField]
    LayerMask _canHit = -1;
    [SerializeField]
    AnimationCurve _portalSpawnCurve = AnimationCurves.Overshoot;
    [SerializeField]
    float _portalSpawnTime = 0.25f;
    [SerializeField]
    float _normalOffset = 0.01f;

    Portal _leftPortal;
    Portal _rightPortal;

    Portal _currentPortal;

    void Awake() {
        if (!isActiveAndEnabled) {
            return;
        }
    }

    void Start() {
        if (!isActiveAndEnabled) {
            return;
        }

        _leftPortal = SpawnPortal(Vector3.zero, Quaternion.identity, Color.blue);
        _rightPortal = SpawnPortal(Vector3.zero, Quaternion.identity, Color.red);

        _leftPortal.ExitPortal = _rightPortal;
        _rightPortal.ExitPortal = _leftPortal;

        _leftPortal.name = "Left Portal";
        _rightPortal.name = "Right Portal";

        _leftPortal.gameObject.SetActive(false);
        _rightPortal.gameObject.SetActive(false);

        _currentPortal = _leftPortal;

        var trackedController = GetComponent<SteamVR_TrackedController>();
        if (trackedController == null) {
            trackedController = gameObject.AddComponent<SteamVR_TrackedController>();
        }

        trackedController.TriggerClicked += new ClickedEventHandler(DoClick);
    }

    void DoClick(object sender, ClickedEventArgs e) {
        OnClick();
    }

    //void Update() {
    //    if (Input.GetMouseButtonDown(0)) {
    //        OnClick();
    //    }
    //}

    void OnClick() {
        Ray ray = new Ray(this.transform.position, transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, _canHit)) {
            Portal portal = _currentPortal;

            // Spawn a bullet that will auto-destroy itself after it travels a certain distance
            Color color = portal == _leftPortal ? Color.blue : Color.red;
            SpawnBullet(_bulletPrefab, transform.position + transform.forward * _bulletSpawnOffset, transform.forward, hit.distance, color);

            // Calculate the portal's rotation based off the hit object's normal.
            // Portals on walls should be upright, portals on the ground can be rotated in any way.
            Quaternion rotation = CalculateRotation(transform.forward, hit.normal);

            // Set portal position and rotation. Need to do this before calling FindFit so we can get
            // the portal's corners in world space
            portal.transform.position = hit.point;
            portal.transform.rotation = rotation;

            // Make sure the portal can fit flushly on the object we've hit.
            // If it can fit, but it's hanging off the edge, push it in.
            // Otherwise, disable the portal.
            Vector3 fitOffset;
            if (FindFit(portal, hit.collider, out fitOffset)) {
                portal.transform.position = fitOffset + hit.normal * _normalOffset;
                portal.IgnoredColliders = new Collider[] { hit.collider };
                portal.gameObject.SetActive(true);

                // Scale the portal's renderer up from 0 to 1 for a nice visual pop-in
                Renderer portalRenderer = portal.GetComponentInChildren<MeshRenderer>();
                SetScaleOverTime(portalRenderer.transform, Vector3.zero, Vector3.one, _portalSpawnCurve, _portalSpawnTime);

                if (_currentPortal == _leftPortal) {
                    _currentPortal = _rightPortal;
                } else {
                    _currentPortal = _leftPortal;
                }
            } else {
                portal.gameObject.SetActive(false);
            }
        }
    }

    Portal SpawnPortal(Vector3 location, Quaternion rotation, Color color) {
        GameObject obj = Instantiate(_portalPrefab, location, rotation);
        Portal portal = obj.GetComponent<Portal>();

        ParticleSystem particles = portal.GetComponentInChildren<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.startColor = color;

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

    bool FindFit(Portal portal, Collider collider, out Vector3 offset) {
        Vector3 minOffset = new Vector3(Mathf.Infinity, Mathf.Infinity, Mathf.Infinity);
        Vector3 maxOffset = new Vector3(Mathf.NegativeInfinity, Mathf.NegativeInfinity, Mathf.NegativeInfinity);
        foreach (Vector3 corner in portal.WorldSpaceCorners()) {
            Vector3 closestPoint = collider.ClosestPoint(corner);
            Vector3 offset_ = closestPoint - corner;

            minOffset = Vector3.Min(minOffset, offset_);
            maxOffset = Vector3.Max(maxOffset, offset_);
        }

        float epsilon = 0.00001f;
        if ((Mathf.Abs(minOffset.x) > epsilon && Mathf.Abs(maxOffset.x) > epsilon) ||
            (Mathf.Abs(minOffset.y) > epsilon && Mathf.Abs(maxOffset.y) > epsilon) ||
            (Mathf.Abs(minOffset.z) > epsilon && Mathf.Abs(maxOffset.z) > epsilon)) {
            offset = portal.transform.position;
            return false;
        } else {
            offset = portal.transform.position + minOffset + maxOffset;
            return true;
        }
    }

    void SetScaleOverTime(Transform t, Vector3 startSize, Vector3 endSize, AnimationCurve curve, float time) {
        StartCoroutine(ScaleOverTimeRoutine(t, startSize, endSize, curve, time));
    }

    IEnumerator ScaleOverTimeRoutine(Transform t, Vector3 startSize, Vector3 endSize, AnimationCurve curve, float time) {
        float elapsed = 0;
        while (elapsed < time) {
            t.localScale = Vector3.LerpUnclamped(startSize, endSize, curve.Evaluate(elapsed / time));
            yield return null;
            elapsed += Time.deltaTime;
        }
        t.localScale = endSize;
    }

    GameObject SpawnBullet(GameObject prefab, Vector3 position, Vector3 direction, float distance, Color color) {
        GameObject bullet = Instantiate(prefab);
        bullet.transform.position = position;
        bullet.GetComponent<Rigidbody>().velocity = direction * _bulletSpeed;

        ParticleSystem particles = bullet.transform.Find("Trail").GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.startColor = color;

        float duration = distance / _bulletSpeed;

        StartCoroutine(DestroyBulletAfterTime(bullet, duration));
        return bullet;
    }


    IEnumerator DestroyBulletAfterTime(GameObject bullet, float duration) {
        yield return new WaitForSeconds(duration);
        bullet.GetComponent<ParticleSystem>().Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Destroy(bullet, 1.0f);
    }
}
