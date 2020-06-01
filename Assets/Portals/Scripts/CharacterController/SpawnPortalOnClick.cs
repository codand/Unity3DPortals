using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Portals;

static internal class AnimationCurves {
    public static AnimationCurve Overshoot = new AnimationCurve(new Keyframe[] {
        new Keyframe(0.0f, 0.0f, 0.0f, 0.0f),
        new Keyframe(0.9f, 1.1f, 0.0f, 0.0f),
        new Keyframe(1.0f, 1.0f, 0.0f, 0.0f),
    });
}

public class SpawnPortalOnClick : MonoBehaviour {
    [SerializeField] Camera _camera;
    [SerializeField] GameObject _bulletPrefab;
    [SerializeField] float _bulletSpawnOffset = 3.0f;
    [SerializeField] float _bulletSpeed = 20.0f;
    [SerializeField] GameObject _portalPrefab;
    [SerializeField] LayerMask _canHit = -1;
    [SerializeField] AnimationCurve _portalSpawnCurve = AnimationCurves.Overshoot;
    [SerializeField] float _portalSpawnTime = 0.25f;
    [SerializeField] float _normalOffset = 0.05f;
    [SerializeField] bool _shootThroughPortals = false;
    [SerializeField] int _meshColliderIterationCount = 10;

    [SerializeField] float _portalWaveAmplitude = 0.2f;
    [SerializeField] float _portalWaveDuration = 0.5f;

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

        _leftPortal = SpawnPortal(Vector3.zero, Quaternion.identity, Color.blue);
        _rightPortal = SpawnPortal(Vector3.zero, Quaternion.identity, Color.red);

        _leftPortal.ExitPortal = _rightPortal;
        _rightPortal.ExitPortal = _leftPortal;

        _leftPortal.name = "Left Portal";
        _rightPortal.name = "Right Portal";

        _leftPortal.gameObject.SetActive(false);
        _rightPortal.gameObject.SetActive(false);

    }

    void Update () {
        bool leftClick = Input.GetMouseButtonDown(0);
        bool rightClick = Input.GetMouseButtonDown(1);

        if (leftClick || rightClick) {
            Polarity polarity = leftClick ? Polarity.Left : Polarity.Right;
            Fire(polarity);
        }
	}

    private enum Polarity {
        Left,
        Right
    }

    private void Fire(Polarity polarity) {
        Ray ray = new Ray(_camera.transform.position, _camera.transform.forward);
        RaycastHit hit;
        bool hitWall;

        if (_shootThroughPortals) {
            hitWall = PortalPhysics.Raycast(ray, out hit, Mathf.Infinity, _canHit, QueryTriggerInteraction.Ignore);
        } else {
            hitWall = Physics.Raycast(ray, out hit, Mathf.Infinity, _canHit, QueryTriggerInteraction.Ignore);
        }

        // Test if we hit a portal first to make them wobble
        bool hitPortal = Physics.Raycast(ray, out RaycastHit portalHit, Mathf.Infinity, PortalPhysics.PortalLayerMask, QueryTriggerInteraction.Collide);
        if (hitPortal) {
            Portal portal = portalHit.collider.GetComponentInParent<Portal>();
            WavePortalOverTime(portal, hit.point, _portalWaveAmplitude, _portalWaveDuration);
        } else if (hitWall) {
            TrySpawnPortal(polarity, hit);
        }
        
        // Spawn a bullet that will auto-destroy itself after it travels a certain distance
        Color color = polarity == Polarity.Left ? Color.blue : Color.red;
        SpawnBullet(_bulletPrefab, _camera.transform.position + _camera.transform.forward * _bulletSpawnOffset, _camera.transform.forward, hit.distance, color);
    }

    private void TrySpawnPortal(Polarity polarity, RaycastHit hit) {
        Portal portal = polarity == Polarity.Left ? _leftPortal : _rightPortal;

        // Calculate the portal's rotation based off the hit object's normal.
        // Portals on walls should be upright, portals on the ground can be rotated in any way.
        Quaternion rotation = CalculateRotation(_camera.transform.forward, hit.normal);

        // Set portal position and rotation. Need to do this before calling FindFit so we can get
        // the portal's corners in world space
        portal.transform.position = hit.point;
        portal.transform.rotation = rotation;

        // Make sure the portal can fit flushly on the object we've hit.
        // If it can fit, but it's hanging off the edge, push it in.
        // Otherwise, disable the portal.
        Vector3 newPosition;
        if (FindFit(portal, hit.collider, out newPosition)) {
            portal.transform.position = newPosition + hit.normal * _normalOffset;
            portal.IgnoredColliders = new Collider[] { hit.collider };
            portal.gameObject.SetActive(true);

            // Scale the portal's renderer up from 0 to 1 for a nice visual pop-in
            Renderer portalRenderer = portal.GetComponentInChildren<MeshRenderer>();
            SetScaleOverTime(portalRenderer.transform, Vector3.zero, Vector3.one, _portalSpawnCurve, _portalSpawnTime);
        } else {
            portal.gameObject.SetActive(false);
        }
    }

    Portal SpawnPortal(Vector3 location, Quaternion rotation, Color color) {
        GameObject obj = Instantiate(_portalPrefab, location, rotation);
        Portal portal = obj.GetComponent<Portal>();

        ParticleSystem particles = portal.GetComponentInChildren<ParticleSystem>();
        if (particles) {
            ParticleSystem.MainModule main = particles.main;
            main.startColor = color;
        }
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

    bool FindFit(Portal portal, Collider collider, out Vector3 newPosition) {
        if (collider is BoxCollider) {
            return FindFitBoxCollider(portal, collider, out newPosition);
        } else if (collider is MeshCollider) {
            return FindFitMeshCollider(portal, collider, out newPosition);
        } else {
            newPosition = portal.transform.position;
            return true;
        }
    }

    bool FindFitBoxCollider(Portal portal, Collider collider, out Vector3 offset) {
        // Loop through each corner of the portal rect.
        // For each point, calculate the min and max distance to the collider surface
        // and choose the most extreme of each.
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
    
    int RaycastCorners(Collider collider, Vector3[] corners, Vector3 offset, Vector3 direction, bool[] outHits) {
        int numHits = 0;
        for (int i = 0; i < corners.Length; i++) {
            Vector3 corner = corners[i] + offset;

            // Perform raycast from a tiny bit back from the contact point to a little bit through the contact point
            float normalOffset = 0.01f;
            Ray ray = new Ray(corner - direction * normalOffset, direction);
            outHits[i] = collider.Raycast(ray, out RaycastHit hitInfo, normalOffset * 2);
            if (outHits[i]) {
                numHits++;
            }
        }
        return numHits;
    }

    bool FindViableCoplanarRectOnCollider(Collider collider, Vector3 center, Vector3[] corners, Vector3 forward, int iterations, out Vector3 offset) {
        bool[] hits = new bool[4];
        int numHits = 0;
        offset = Vector3.zero;
        int currentIteration = 0;
        for (currentIteration = 0; currentIteration < iterations; currentIteration++) {
            numHits = RaycastCorners(collider, corners, offset, forward, hits);

            // Success
            if (numHits == 4) {
                break;
            }

            // If none of the corner raycasts hit the collider, can't guess which direction to go
            if (numHits == 0) {
                break;
            }

            Vector3 stepOffset = Vector3.zero;
            for (int i = 0; i < corners.Length; i++) {
                if (hits[i]) {
                    Vector3 toCorner = corners[i] - center;
                    stepOffset += toCorner;
                }
            }

            // If two of our corners are coplanar and share an edge, our offset will be facing the correct direction,
            // but it will have too much magnitude because the vectors face partially in the same direction.
            // If there are three hits, this isn't an issue because two of them will have to be opposites, so the two
            // will cancel eachother out.
            if (numHits == 2) {
                stepOffset /= 2;
            }

            // Reduce our offset distance by a power of 2 each iteration.
            stepOffset *= Mathf.Pow(0.5f, currentIteration);

            offset += stepOffset;
        }

        // Test again with the latest offset
        numHits = RaycastCorners(collider, corners, offset, forward, hits);

        // If viable solution is found, try to improve it with remaining iterations by creeping backwards
        if (numHits == 4) {
            for (int i = currentIteration; i < iterations; i++) {
                
                // Creep backwards by a smaller distance each iteration
                Vector3 stepOffset = offset * Mathf.Pow(0.5f, i);
                Vector3 newOffset = offset - stepOffset;

                // Check new offset
                int foo = RaycastCorners(collider, corners, newOffset, forward, hits);
                if (foo == 4) {
                    offset = newOffset;
                }
            }
        }
       
        return numHits == 4;
    }

    bool FindFitMeshCollider(Portal portal, Collider collider, out Vector3 newPosition) {
        newPosition = portal.transform.position;

        MeshCollider meshCollider = collider as MeshCollider;
        if (!meshCollider) {
            return false;
        }
        Vector3 center = portal.transform.position;
        Vector3[] corners = portal.WorldSpaceCorners();
        Vector3 forward = portal.transform.forward;

        // TODO: magic number
        int numIterations = _meshColliderIterationCount;
        bool viablePositionExists = FindViableCoplanarRectOnCollider(collider, center, corners, forward, numIterations, out Vector3 offset);
        if (!viablePositionExists) {
            return false;
        }

        newPosition = portal.transform.position + offset;
        return true;
    }

    void SetScaleOverTime(Transform t, Vector3 startSize, Vector3 endSize, AnimationCurve curve, float duratio) {
        StartCoroutine(ScaleOverTimeRoutine(t, startSize, endSize, curve, duratio));
    }

    IEnumerator ScaleOverTimeRoutine(Transform t, Vector3 startSize, Vector3 endSize, AnimationCurve curve, float duration) {
        float elapsed = 0;
        while (elapsed < duration) {
            t.localScale = Vector3.LerpUnclamped(startSize, endSize, curve.Evaluate(elapsed / duration));
            yield return null;
            elapsed += Time.deltaTime;
        }
        t.localScale = endSize;
    }

    private Vector2 CalculatePortalUVSpacePoint(Portal portal, Vector3 pointWorldSpace) {
        // This calculation is specific to the portal mesh used at the time of writing
        return portal.transform.InverseTransformPoint(pointWorldSpace) + Vector3.one * 0.5f;
    }

    private void WavePortalOverTime(Portal portal, Vector3 originWorldSpace, float amplitude, float duration) {
        StartCoroutine(WavePortalOverTimeRoutine(portal, originWorldSpace, amplitude, duration));
    }

    IEnumerator WavePortalOverTimeRoutine(Portal portal, Vector3 originWorldSpace, float amplitude, float duration) {
        Vector2 originUVSpace = CalculatePortalUVSpacePoint(portal, originWorldSpace);
        portal.PortalRenderer.FrontFaceMaterial.SetVector("_WaveOrigin", originUVSpace);
        portal.PortalRenderer.BackFaceMaterial.SetVector("_WaveOrigin", originUVSpace);

        float elapsed = 0;
        while (elapsed < duration) {
            float ratio = elapsed / duration;
            float amp = (1 - ratio) * amplitude; // Fade to zero over time
            portal.PortalRenderer.FrontFaceMaterial.SetFloat("_WaveAmplitude", amp);
            portal.PortalRenderer.BackFaceMaterial.SetFloat("_WaveAmplitude", amp);
            yield return null;
            elapsed += Time.deltaTime;
        }
        portal.PortalRenderer.FrontFaceMaterial.SetFloat("_WaveAmplitude", 0);
        portal.PortalRenderer.BackFaceMaterial.SetFloat("_WaveAmplitude", 0);
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
