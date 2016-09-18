using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;

public class PortalCam : MonoBehaviour {
    [SerializeField] Portal _portal;

    Camera _camera;
    Vector4 _clipPlane;

    void Awake() {
        _camera = GetComponent<Camera>();
        _camera.tag = "Untagged";
        _camera.depth = Camera.main.depth - 1;
        _camera.aspect = Camera.main.aspect;
        _camera.cullingMask = ~(0 << 1);
        _camera.fieldOfView = Camera.main.fieldOfView;
        _camera.farClipPlane = Camera.main.farClipPlane;
        _camera.nearClipPlane = Camera.main.nearClipPlane;
        _camera.renderingPath = Camera.main.renderingPath;
        _camera.useOcclusionCulling = Camera.main.useOcclusionCulling;
        _camera.hdr = Camera.main.hdr;
    }

    void OnPreCull() {
        // Adjust camera projection matrix so that the clipping plane aligns with our portal
        Vector4 clippingPlane = GetTransformPlane(_portal.exitPortal.transform);

        // Test to see which side of the clipping plane our portal cam is on.
        // If we're in front of it, we should use the normal projection.
        // We add a small amount of leeway (the value of nearClipPlane) to prevent flickering
        Plane plane = new Plane(-1 * new Vector3(clippingPlane.x, clippingPlane.y, clippingPlane.z), clippingPlane.w + _camera.nearClipPlane);
        if (plane.GetSide(_camera.transform.position)) {
            _camera.ResetProjectionMatrix();
        } else {
            UpdateCameraClippingPlane(clippingPlane, _portal.exitPortal.transform.localScale);
        }
    }

    Vector4 GetTransformPlane(Transform trans) {
        Vector3 normal = trans.forward;
        float d = Vector3.Dot(normal, trans.position);
        Vector4 plane = new Vector4(normal.x, normal.y, normal.z, d);
        return plane;
    }

    void UpdateCameraClippingPlane(Vector4 plane, Vector3 scale) {
        // Restore original projection matrix
        _camera.ResetProjectionMatrix();

        // Copy original projection matrix
        Matrix4x4 mat = _camera.projectionMatrix;

        // Project our world space clipping plane to the camera's local coordinates
        // e.g. normal (0, 0, 1) becomes (1, 0, 0) if we're looking left parallel to the plane
        Vector4 transformedNormal = _camera.transform.InverseTransformDirection(plane).normalized;
        Vector4 transformedPoint = _camera.transform.InverseTransformPoint(plane.w * plane);

        // Calculate the d value for our plane by projecting our transformed point
        // onto our transformed normal vector. The normal must also be scaled to match our exit portal's scale
        float projectedDistance = Vector4.Dot(Vector3.Scale(transformedNormal, scale), transformedPoint);
        Vector4 transformedPlane = new Vector4(-transformedNormal.x, -transformedNormal.y, transformedNormal.z, projectedDistance);

        // Calculate the new projection matrix
        CalculateObliqueProjectionMatrix(ref mat, transformedPlane);

        // Reassign to camera
        _camera.projectionMatrix = mat;
    }

    void CalculateObliqueProjectionMatrix(ref Matrix4x4 projection, Vector4 clipPlane) {
        Vector4 q = projection.inverse * new Vector4(
            Mathf.Sign(clipPlane.x),
            Mathf.Sign(clipPlane.y),
            1.0f,
            1.0f
        );
        Vector4 c = clipPlane * (2.0F / (Vector4.Dot(clipPlane, q)));
        // third row = clip plane - fourth row
        projection[2] = c.x - projection[3];
        projection[6] = c.y - projection[7];
        projection[10] = c.z - projection[11];
        projection[14] = c.w - projection[15];
    }


    //void OnEnable() {
    //    CommandBuffer cmdbuf = new CommandBuffer();
    //    cmdbuf.name = "Foo";
    //    cmdbuf.ClearRenderTarget(true, false, Color.blue, 0.0f);
    //    _camera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, cmdbuf);
    //}

    //void OnDisable() {
    //    _camera.RemoveAllCommandBuffers();
    //}




    //// Postprocess the image
    //void OnRenderImage(RenderTexture source, RenderTexture destination) {
    //    Graphics.Blit(source, destination, material);
    //}

    //void OnPreCull() {
    //    _camera.ResetWorldToCameraMatrix();
    //    _camera.ResetProjectionMatrix();
    //    _camera.projectionMatrix = _camera.projectionMatrix * Matrix4x4.Scale(new Vector3(1, -1, 1));
    //}

    //void OnPreRender() {
    //    GL.invertCulling = true;
    //}

    //void OnPostRender() {
    //    GL.invertCulling = false;
    //}
}