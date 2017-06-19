#ifndef PLANE_CLIPPING_INCLUDED
#define PLANE_CLIPPING_INCLUDED

#define UNITY_REQUIRE_FRAG_WORLDPOS

float3 distanceToPlane(float4 plane, float3 position) {
	return (dot(plane.xyz, position.xyz) + plane.w) / sqrt(dot(plane.xyz, plane.xyz));
}

float4 _ClippingPlane;

void planarClip(float3 position) {
	clip(distanceToPlane(_ClippingPlane, position));
}

#define PLANAR_CLIP(worldPos) planarClip(worldPos);

#endif // PLANE_CLIPPING_INCLUDED
