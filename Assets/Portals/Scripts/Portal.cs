using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Portal : MonoBehaviour {
    public Portal exitPortal;
    [SerializeField] Mesh _containerMesh;
    [SerializeField] Mesh _containerMeshInverted;
    
    RenderTexture _renderTexture;
    Camera _mainCamera;
    Camera _myCamera;
    MeshFilter _meshFilter;
    Mesh _defaultMesh;

    bool _insidePortal = false;

    void Awake() {
        _mainCamera = Camera.main;
        _myCamera = GetComponentInChildren<Camera>();
        _meshFilter = GetComponentInChildren<MeshFilter>();
        _defaultMesh = _meshFilter.mesh;

        // Create and assign RenderTexture
        _renderTexture = new RenderTexture(_myCamera.pixelWidth, _myCamera.pixelWidth, 16);
        _renderTexture.Create();
        _myCamera.targetTexture = _renderTexture; 
    }

    void Start() {
        // Assign RenderTexture to portal quad's material
        GetComponentInChildren<Renderer>().material.SetTexture("_MainTex", _renderTexture);
    }

    Quaternion WorldToPortalQuaternion(Transform ref1, Transform ref2) {
        // Transforms a quaternion or vector into the second portal's space.
        // We have to flip the camera in between so that we face the outside direction of the portal
        return ref2.rotation * Quaternion.Euler(180, 0, 180) * Quaternion.Inverse(ref1.rotation);
    }

    void ApplyWorldToPortalTransform(Transform target, Transform reference, Transform portalEnter, Transform portalExit) {
        Quaternion worldToPortal = WorldToPortalQuaternion(portalEnter, portalExit);

        // Rotate
        target.rotation = worldToPortal * reference.rotation;

        // Scale
        Vector3 scale = new Vector3(
            portalExit.localScale.x / portalEnter.localScale.x,
            portalExit.localScale.y / portalEnter.localScale.y,
            portalExit.localScale.z / portalEnter.localScale.z
        );
        
        // Translate
        Vector3 positionDelta = reference.position - portalEnter.position;
        Vector3 scaledPositionDelta = Vector3.Scale(positionDelta, scale);
        Vector3 transformedDelta = worldToPortal * scaledPositionDelta;
        target.position = portalExit.position + transformedDelta;
        target.localScale = Vector3.Scale(reference.localScale, scale);

    }

    void LateUpdate() {
        ApplyWorldToPortalTransform(_myCamera.transform, _mainCamera.transform, this.transform, exitPortal.transform);
    }

    void OnTriggerEnter(Collider collider) {
        //Debug.Log(collider.gameObject.name + " entered " + this.gameObject.name);
        CharacterController controller = collider.GetComponent<CharacterController>();
        Rigidbody rigidbody = collider.GetComponent<Rigidbody>();
        if (controller == null && rigidbody == null) {
            // No velocity, cannot determine whther or not the object is entering the portal
            return;
        }

        Vector3 velocity = rigidbody == null ? controller.velocity : rigidbody.velocity;
        bool enteringPortal = Vector3.Dot(velocity, transform.forward) >= 0 ? true : false;
        if (enteringPortal) {
            _meshFilter.mesh = _containerMesh;
            //exitPortal.transform.Find("PortalPlane").GetComponent<MeshFilter>().mesh = _containerMeshInverted;
            _insidePortal = true;
        }
    }

    void OnTriggerExit(Collider collider) {
        //Debug.Log(collider.gameObject.name + " exited " + this.gameObject.name);
        CharacterController controller = collider.GetComponent<CharacterController>();
        Rigidbody rigidbody = collider.GetComponent<Rigidbody>();
        if (controller == null && rigidbody == null) {
            // No velocity, cannot determine whther or not the object is entering the portal
            return;
        }


        Vector3 velocity = rigidbody == null ? controller.velocity : rigidbody.velocity;
        bool exitingThroughPortal = Vector3.Dot(velocity, transform.forward) >= 0 ? true : false;
        if (_insidePortal && exitingThroughPortal) {
            _meshFilter.mesh = _defaultMesh;
            //exitPortal.transform.Find("PortalPlane").GetComponent<MeshFilter>().mesh = _defaultMesh;

            ApplyWorldToPortalTransform(collider.gameObject.transform, collider.gameObject.transform, this.transform, exitPortal.transform);
        } 
        _meshFilter.mesh = _defaultMesh;
        _insidePortal = false;
    }
}
