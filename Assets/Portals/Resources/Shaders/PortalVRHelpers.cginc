#ifndef PORTAL_VR_HELPERS_INCLUDED
#define PORTAL_VR_HELPERS_INCLUDED

#include "UnityCG.cginc"

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

sampler2D _DefaultTexture;
sampler2D _LeftEyeTexture;
sampler2D _RightEyeTexture;
sampler2D _TransparencyMask;

sampler2D _PortalTexture;
float4x4 _PortalProjectionMatrix;
float4x4 _PortalProjectionMatrix2;

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
};

v2f vertPortal(appdata v)
{
	v2f o;
	o.pos = UnityObjectToClipPos(v.vertex);

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
	//return tex2Dproj(_PortalTexture, uv);
}

fixed4 fragPortal(v2f i, fixed face : VFACE) : SV_Target
{
#ifdef SAMPLE_DEFAULT_TEXTURE
	float4 col = tex2Dproj(_DefaultTexture, i.objUV);
#else 
	float4 col = sampleCurrentTextureProj(i.screenUV);
#endif

#ifdef IS_BACKFACE
	col.a = _BackfaceAlpha;
#else
	col.a = tex2D(_TransparencyMask, i.objUV).r;
#endif

	return col;
}

sampler2D _CameraDepthTexture;

sampler2D _PortalGBuffer0;
sampler2D _PortalGBuffer1;
sampler2D _PortalGBuffer2;
sampler2D _PortalGBuffer3;
sampler2D _PortalDepthBuffer;

void fragDeferred(
	v2f i,
	out half4 outGBuffer0 : SV_Target0,
	out half4 outGBuffer1 : SV_Target1,
	out half4 outGBuffer2 : SV_Target2,
	out half4 outEmission : SV_Target3,          // RT3: emission (rgb), --unused-- (a)
	out float extraDepth : SV_Target4,
	out float outDepth : SV_DEPTH
#if defined(SHADOWS_SHADOWMASK) && (UNITY_ALLOWED_MRT_COUNT > 4)
	, out half4 outShadowMask : SV_Target4       // RT4: shadowmask (rgba)
#endif
) {
	outGBuffer0 = tex2Dproj(_PortalGBuffer0, i.screenUV);
	outGBuffer1 = tex2Dproj(_PortalGBuffer1, i.screenUV);
	outGBuffer2 = tex2Dproj(_PortalGBuffer2, i.screenUV);
	outEmission = tex2Dproj(_PortalGBuffer3, i.screenUV);
	outDepth = tex2Dproj(_PortalDepthBuffer, i.screenUV);

	//float sceneDepth = tex2Dproj(_CameraDepthTexture, i.screenUV);
	//float actualDepth = i.pos.z;
	////clip (actualDepth - sceneDepth);
	//outDepth = sceneDepth;
	extraDepth = i.pos.z;
#if defined(SHADOWS_SHADOWMASK) && (UNITY_ALLOWED_MRT_COUNT > 4)
	outShadowMask = 0;
#endif
}

#endif // PORTAL_VR_HELPERS_INCLUDED
