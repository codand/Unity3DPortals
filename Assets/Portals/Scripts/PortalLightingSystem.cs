using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Portals;
using System.Linq;

[RequireComponent(typeof(Camera))]
public class PortalLightingSystem : MonoBehaviour {
    // TODO: Remove this and make work for all portals
    [SerializeField] Portal _portal;

    Camera _camera;
    Light[] _lights;
    Material _lightMaterial;
    CommandBuffer _cmdBuffer;
    Dictionary<Light, CommandBuffer> _lightBuffers;
    Mesh _lightMesh;

    static PortalLightingSystem _instance;
    public static PortalLightingSystem instance {
        get {
            if (!_instance) {
                _instance = FindObjectOfType<PortalLightingSystem>();
                if (!_instance) {
                    Debug.LogErrorFormat("No {0} found!", typeof(PortalLightingSystem).Name);
                }
            }
            return _instance;
        }
    }

    void Awake() {
        _camera = GetComponent<Camera>();
        _lights = FindObjectsOfType<Light>();
        _lightMaterial = new Material(Shader.Find("Portal/Portal-DeferredShading"));
        //_lightMaterial = new Material(Shader.Find("Hidden/Internal-DeferredShading"));

        if (!_camera.hdr) {
            _lightMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.DstColor);
            _lightMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        }
        _lightMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Greater);
        _lightMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Front);
        //_lightMaterial.EnableKeyword("SHADOWS_CUBE");

        _lightMesh = GetPrimitiveMesh(PrimitiveType.Sphere);
        _lightBuffers = new Dictionary<Light, CommandBuffer>();
        _cmdBuffer = new CommandBuffer();
        _cmdBuffer.name = "Deferred Portal Lighting";
        //foreach (Light light in _lights) {
        //    SetUpLight(light);
        //}

    }

    Mesh GetPrimitiveMesh(PrimitiveType type) {
        GameObject go = GameObject.CreatePrimitive(type);
        Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
        Destroy(go);
        return mesh;
    }

    public Material stencilMat;
    void OnPreRender() {
        SetUpCameraCommandBuffer();
    }

    Vector4 MakePlane(Vector3 p1, Vector3 p2, Vector3 p3) {
        Vector3 p3p1 = p3 - p1;
        Vector3 p2p1 = p2 - p1;
        Vector3 normal = Vector3.Normalize(Vector3.Cross(p3p1, p2p1));
        float w = Vector3.Dot(normal, p1);
        return new Vector4(normal.x, normal.y, normal.z, w);
    }

    void SetUpCameraCommandBuffer() {
        //stencilMat = new Material(Shader.Find("Hidden/Internal-StencilWrite"));

        _cmdBuffer.Clear();

        foreach (Light light in _lights) {
            Vector3 position = _portal.MultiplyPoint(light.transform.position);
            Quaternion rotation = _portal.WorldToPortalQuaternion() * light.transform.rotation;

            _cmdBuffer.SetGlobalVector("_LightPos", new Vector4(position.x, position.y, position.z, 1.0f / (light.range * light.range)));
            _cmdBuffer.SetGlobalColor("_LightColor", light.color * light.intensity);


            Vector3[] corners = _portal.ExitPortal.GetCorners();
            Vector4[] planes = new Vector4[] {
                MakePlane(corners[0], corners[1], corners[2]),
                MakePlane(position, corners[0], corners[1]),
                MakePlane(position, corners[1], corners[2]),
                MakePlane(position, corners[2], corners[3]),
                MakePlane(position, corners[3], corners[0]),
            };
            //Debug.DrawLine(position, corners[0], Color.white, 0.1f);
            //Debug.DrawLine(position, corners[1], Color.white, 0.1f);
            //Debug.DrawLine(position, corners[2], Color.white, 0.1f);
            //Debug.DrawLine(position, corners[3], Color.white, 0.1f);

            _cmdBuffer.SetGlobalVectorArray("_ShadowPlanes", planes);

            float scale = light.range * 2;
            Matrix4x4 trs = Matrix4x4.TRS(position, light.transform.rotation, new Vector3(scale, scale, scale));

            //_cmdBuffer.DrawMesh(_lightMesh, trs, stencilMat, 0, 0);
            //_lightMaterial.SetOverrideTag("ZTest", "Greater");

            _cmdBuffer.DrawMesh(_lightMesh, trs, _lightMaterial, 0, 0);
        }
    }

    void SetUpLight(Light light) {
        CommandBuffer buf;
        _lightBuffers.TryGetValue(light, out buf);
        if (buf == null) {
            buf = new CommandBuffer();
            buf.name = "Copy Shadow Map Texture";
            _lightBuffers[light] = buf;
        }

        //buf.Clear();

        buf.SetGlobalTexture("_ShadowMapTexture", BuiltinRenderTextureType.CurrentActive);

        light.AddCommandBuffer(LightEvent.AfterShadowMap, buf);
    }

    void OnEnable() {
        //_camera.AddCommandBuffer(CameraEvent.AfterLighting, _cmdBuffer);
        foreach (Camera cam in Resources.FindObjectsOfTypeAll<Camera>()) {
            cam.AddCommandBuffer(CameraEvent.AfterLighting, _cmdBuffer);
        }
    }

    void OnDisable() {
        //_camera.RemoveCommandBuffer(CameraEvent.AfterLighting, _cmdBuffer);
        foreach (Camera cam in Resources.FindObjectsOfTypeAll<Camera>()) {
            cam.RemoveCommandBuffer(CameraEvent.AfterLighting, _cmdBuffer);
        }
    }
}
