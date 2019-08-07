Shader "Portals/CopyGBuffer"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
			ZTest Always
			
            CGPROGRAM
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };


            struct v2f
            {
				float4 vertex : SV_POSITION;
				float4 screenUV : TEXCOORD0;
            };

			struct FrameData
			{
				half4 gbuffer0 : SV_Target0;
				half4 gbuffer1 : SV_Target1;
				half4 gbuffer2 : SV_Target2;
				half4 gbuffer3 : SV_Target3;          // RT3: emission (rgb), --unused-- (a)
				float depth : SV_DEPTH;
#if defined(SHADOWS_SHADOWMASK) && (UNITY_ALLOWED_MRT_COUNT > 4)
				half4 shadowMask : SV_Target4;       // RT4: shadowmask (rgba)
#endif
			};

			sampler2D _CameraGBufferTexture0;
			sampler2D _CameraGBufferTexture1;
			sampler2D _CameraGBufferTexture2;
			sampler2D _CameraGBufferTexture3;
			sampler2D_float _CameraDepthTexture;

			float4x4 _PortalMatrix;
			float4x4 _PortalMatrix_I;

			float4x4 _PortalViewMatrix;
			float4x4 _PortalViewMatrix_I;

			float4x4 _PortalParentViewMatrix;
			float4x4 _PortalParentViewMatrix_I;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
				o.screenUV = ComputeNonStereoScreenPos(o.vertex);
                return o;
            }

			FrameData frag (v2f i)
            {
				FrameData o;
				o.gbuffer0 = tex2Dproj(_CameraGBufferTexture0, i.screenUV);
				o.gbuffer1 = tex2Dproj(_CameraGBufferTexture1, i.screenUV);

				float4 normal = tex2Dproj(_CameraGBufferTexture2, i.screenUV);
				normal = normal * 2.0 - 1.0;
				normal.xyz = mul((float3x3)_PortalMatrix_I, normal.xyz).xyz;
				normal = (normal + 1) * 0.5;

				o.gbuffer2 = normal;
				o.gbuffer3 = tex2Dproj(_CameraGBufferTexture3, i.screenUV);
				o.depth = tex2Dproj(_CameraDepthTexture, i.screenUV);

				return o;
            }
            ENDCG
        }
    }
}
