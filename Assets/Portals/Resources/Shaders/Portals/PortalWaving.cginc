#ifndef PORTAL_WAVING_INCLUDED
#define PORTAL_WAVING_INCLUDED

#include "UnityCG.cginc"

float _WaveAmplitude;
float2 _WaveOrigin; // UV space

float wave(float2 position, float2 origin, float time) {
    float d = length(position - origin);
    float t = time - d * 20;
    return sin(t);
}

float2 waveSlope(float2 position, float2 origin, float amplitude) {
    const float2 dx = float2(0.01f, 0);
    const float2 dy = float2(0, 0.01f);

    float2 time = _Time.y * 50;
    float w = wave(position, origin, time);
    float2 dw = float2(wave(position + dx, origin, time) - w, wave(position + dy, origin, time) - w);
    return dw * amplitude;
}

// UV->Object->World->View->Clip->NDC->Screen
float3 screenUVToViewDir(float2 uv) {
    float4 clipPos;
    clipPos.xy = uv * 2 - 1;
    clipPos.zw = float2(_ProjectionParams.y, 1);
    //clipPos.xy *= _ProjectionParams.x;

    // TODO: Is this specific to DirectX?
    unity_CameraInvProjection[3, 2] *= -1;

    // TODO: All of these could be combined for improved performance
    float3 viewDirVS = (float3) mul(unity_CameraInvProjection, clipPos);
    float3 viewDirWS = mul((float3x3) unity_CameraToWorld, viewDirVS);
    float3 viewDirOS = mul((float3x3) unity_WorldToObject, viewDirWS);
    return normalize(viewDirOS);
}

float2 screenUVToObjUV(float2 uv) {
    float3 cameraPosOS = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos.xyz, 1)).xyz;
    float3 viewDir = screenUVToViewDir(uv);
    float t = -cameraPosOS.z / viewDir.z;
    return cameraPosOS.xy + t * viewDir.xy + 0.5;
}

float2 objUVToScreenUV(float2 objUV) {
    float3 objPos;
    objPos.xy = objUV - 0.5;
    objPos.z = 0;

    float4 clipPos = UnityObjectToClipPos(objPos);
    float4 screenPos = ComputeNonStereoScreenPos(clipPos);
    return screenPos.xy / screenPos.w;
}

float2 clampScreenUV(float2 screenUV) {
    float2 objUV = screenUVToObjUV(screenUV);

    // TODO: This is not accurate and clamps too harshly.
    // What it needs to do is clamp to the nearest VISIBLE pixel in screen space. How?
    objUV = clamp(objUV, 0.01, 0.99);

    return objUVToScreenUV(objUV);
}

float2 screenSpaceWave(float2 screenUV, float2 objUV) {
    float2 waveUVOffset = waveSlope(objUV, _WaveOrigin, _WaveAmplitude);
    screenUV += waveUVOffset;
    screenUV = clampScreenUV(screenUV);
	return screenUV;
}

#endif // PORTAL_WAVING_INCLUDED
