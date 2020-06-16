#ifndef PORTAL_VR_HELPERS_INCLUDED
#define PORTAL_VR_HELPERS_INCLUDED

#include "UnityCG.cginc"

#ifdef PORTAL_WAVING_ENABLED
#include "PortalWaving.cginc"
#endif

#define PORTAL_VR_EYE_LEFT 0
#define PORTAL_VR_EYE_RIGHT 1
//#define PORTAL_VR_EYE_MONO 2

#ifdef UNITY_SINGLE_PASS_STEREO
#define PORTAL_VR_CURRENT_EYE (unity_StereoEyeIndex == 0 ? PORTAL_VR_EYE_LEFT : PORTAL_VR_EYE_RIGHT)
#else
// _PortalMultiPassCurrentEye is this enum: https://docs.unity3d.com/ScriptReference/Camera.MonoOrStereoscopicEye.html
float _PortalMultiPassCurrentEye;
#define PORTAL_VR_CURRENT_EYE ((_PortalMultiPassCurrentEye == 0 || _PortalMultiPassCurrentEye == 2) ? PORTAL_VR_EYE_LEFT : PORTAL_VR_EYE_RIGHT)
//#define PORTAL_VR_CURRENT_EYE (unity_CameraProjection[0][2] <= 0 ? PORTAL_VR_EYE_LEFT : PORTAL_VR_EYE_RIGHT)
#endif


#ifdef SAMPLE_PREVIOUS_FRAME
float4x4 PORTAL_MATRIX_VP;
#endif

static const float _AlphaCutoff = 0.1;

sampler2D _DefaultTexture;
sampler2D _LeftEyeTexture;
sampler2D _RightEyeTexture;
sampler2D _TransparencyMask;

sampler2D _PortalTexture;
float4x4 _PortalProjectionMatrix;

#ifdef IS_BACKFACE
float _BackfaceAlpha;
#endif

struct appdata
{
	float4 vertex : POSITION;
	float4 uv : TEXCOORD0;
};

struct v2f {
	float4 pos : SV_POSITION;
	float4 screenUV : TEXCOORD0;
	float4 objUV : TEXCOORD1;
    float4 objPos : TEXCOORD2; // TODO: Can be combined with objUV for performance gains
};

v2f vertPortal(appdata v)
{
	v2f o;
	o.pos = UnityObjectToClipPos(v.vertex);
    o.objPos = v.vertex;
	o.objUV = v.uv;

#ifdef SAMPLE_PREVIOUS_FRAME
	// Instead of getting the clip position of our portal from the currently rendering camera,
	// calculate the clip position of the portal from a higher level portal. PORTAL_MATRIX_VP == camera.projectionMatrix.
	float4 clipPos = mul(PORTAL_MATRIX_VP, mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)));
	clipPos.y *= _ProjectionParams.x;
	//clipPos.z = 1;    
	o.screenUV = ComputeNonStereoScreenPos(clipPos);
#else
	o.screenUV = ComputeNonStereoScreenPos(o.pos);
#endif
	return o;
}

float4 sampleCurrentTextureProj(float4 uv)
{
	if (PORTAL_VR_CURRENT_EYE == PORTAL_VR_EYE_LEFT) {
		return tex2Dproj(_LeftEyeTexture, uv);
	}
	else {
		return tex2Dproj(_RightEyeTexture, uv);
	}
}

// Given a vertex position that is NOT coplanar with the portal front face,
// reconstruct what this pixel's UV coordinate would be if it WERE on the front face.
// This is needed because the back-geometry of the portal still needs to sample 
// textures as though it were the front face.
// TODO: Do I need to handle reversed Z here?
float4 reconstructFrontFaceUV(float4 objPos) {
    float3 objSpaceCameraPos = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos.xyz, 1)).xyz;
    float3 camToVertex = objSpaceCameraPos - objPos.xyz;
    // Solve for z = 0
    // camPos.z + toVertex.z * t = 0
    // t = -camPos.z / toVertex.z
    float t = -objSpaceCameraPos.z / camToVertex.z;
    float2 uv = objSpaceCameraPos.xy + t * camToVertex.xy + 0.5;
    return float4(uv, 0, 1);
}

fixed4 fragPortal(v2f i, fixed face : VFACE) : SV_Target
{
#ifdef IS_BACKFACE
    clip(_BackfaceAlpha - _AlphaCutoff);
    i.objUV = reconstructFrontFaceUV(i.objPos);
#else
    //i.screenUV /= i.screenUV.w;
#endif
    i.screenUV /= i.screenUV.w;

#ifdef PORTAL_WAVING_ENABLED
    //float2 waveUVOffset = waveSlope(i.objUV.xy, _WaveOrigin, _WaveAmplitude);
    i.screenUV.xy = screenSpaceWave(i.screenUV.xy, i.objUV.xy);
#endif    

#ifdef SAMPLE_DEFAULT_TEXTURE
	float4 col = tex2D(_DefaultTexture, i.objUV);
#else 
	float4 col = sampleCurrentTextureProj(i.screenUV);
#endif

	col.a = tex2D(_TransparencyMask, i.objUV).r;
    clip(col.a - _AlphaCutoff);

	return col;
}

v2f vertPortalShadowCaster(appdata v) {
	v2f o;
	o.pos = UnityObjectToClipPos(v.vertex);
	o.objUV = v.uv;
	return o;
}

fixed fragPortalShadowCaster(v2f i) : SV_Target
{
	fixed alpha = tex2D(_TransparencyMask, i.objUV).x;
	clip(alpha - 0.5);
    
	return 0;
}

#endif // PORTAL_VR_HELPERS_INCLUDED
