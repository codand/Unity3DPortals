#ifndef PORTAL_VR_HELPERS_INCLUDED
#define PORTAL_VR_HELPERS_INCLUDED

#include "UnityCG.cginc"

#define PORTAL_VR_EYE_LEFT -1
#define PORTAL_VR_EYE_RIGHT 1
#define PORTAL_VR_EYE_MONO 0

#ifdef UNITY_SINGLE_PASS_STEREO
#define PORTAL_VR_CURRENT_EYE (unity_StereoEyeIndex == 0 ? PORTAL_VR_EYE_LEFT : PORTAL_VR_EYE_RIGHT)
#else
#define PORTAL_VR_CURRENT_EYE (unity_CameraProjection[0][2] >= 0 ? PORTAL_VR_EYE_LEFT : PORTAL_VR_EYE_RIGHT)
//#define PORTAL_VR_CURRENT_EYE (unity_CameraProjection[0][2] == 0 ? PORTAL_VR_EYE_MONO : (unity_CameraProjection[0][2] > 0 ? PORTAL_VR_EYE_LEFT : PORTAL_VR_EYE_RIGHT))
#endif

#ifdef SAMPLE_PREVIOUS_FRAME
float4x4 PORTAL_MATRIX_VP;
#endif

sampler2D _DefaultTexture;
sampler2D _LeftEyeTexture;
sampler2D _RightEyeTexture;
sampler2D _TransparencyMask;
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

fixed4 fragPortal(v2f i) : SV_Target
{
#ifdef SAMPLE_DEFAULT_TEXTURE
	fixed4 col = tex2Dproj(_DefaultTexture, i.objUV);
#else 
	fixed4 col = sampleCurrentTextureProj(i.screenUV);
#endif

#ifdef IS_BACKFACE
	col.a = _BackfaceAlpha;
	//col = float4(0, 0, 1, 1);
#else
	col.a = tex2D(_TransparencyMask, i.objUV).r;
#endif
	return col;
}

#endif // PORTAL_VR_HELPERS_INCLUDED
