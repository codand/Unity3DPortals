Shader "Portals/CopyGBuffer"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }

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
				float4 pos : SV_POSITION;
				float4 screenUV : TEXCOORD0;
            };

			struct FrameData
			{
				half4 gbuffer0 : SV_Target0;
				half4 gbuffer1 : SV_Target1;
				half4 gbuffer2 : SV_Target2;
				half4 gbuffer3 : SV_Target3;          // RT3: emission (rgb), --unused-- (a)
				//float extraDepth : SV_Target4;
				float depth : SV_DEPTH;
#if defined(SHADOWS_SHADOWMASK) && (UNITY_ALLOWED_MRT_COUNT > 4)
				half4 shadowMask : SV_Target5;       // RT4: shadowmask (rgba)
#endif
			};

			uniform sampler2D _CameraGBufferTexture0;
			uniform sampler2D _CameraGBufferTexture1;
			uniform sampler2D _CameraGBufferTexture2;
			uniform sampler2D _CameraGBufferTexture3;
			uniform sampler2D_float _CameraDepthTexture;

			uniform float4x4 _PortalMatrix_I;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
				o.screenUV = ComputeNonStereoScreenPos(o.pos);

                return o;
            }

			float4 FixNormals(float4 packedNormal) {
				// GBuffer 2 contains WORLD space normals from the perspective of the portal camera.
				// When we apply the portal transformation, those normals get transformed too,
				// so we must apply the inverse portal transformation to undo that.
				// Normals are packed in the gbuffer, and must be unpacked before applying the
				// correction, and must be repacked afterwards.

				float3 normal = packedNormal * 2 - 1;
				normal = mul((float3x3)_PortalMatrix_I, normal);
				normal = (normal + 1) * 0.5;

				return float4(normal, packedNormal.w);
			}

			FrameData frag (v2f i)
            {
				FrameData o;
				o.gbuffer0 = tex2Dproj(_CameraGBufferTexture0, i.screenUV);
				o.gbuffer1 = tex2Dproj(_CameraGBufferTexture1, i.screenUV);
				o.gbuffer2 = FixNormals(tex2Dproj(_CameraGBufferTexture2, i.screenUV));
				o.gbuffer3 = tex2Dproj(_CameraGBufferTexture3, i.screenUV);
				o.depth = tex2Dproj(_CameraDepthTexture, i.screenUV);

				//o.extraDepth = i.pos.z;
				return o;
            }
            ENDCG
        }


		// PASTE GBUFFER
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
					float4 pos : SV_POSITION;
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
					half4 shadowMask : SV_Target5;       // RT4: shadowmask (rgba)
	#endif
				};

				uniform sampler2D _PortalGBufferTexture0;
				uniform sampler2D _PortalGBufferTexture1;
				uniform sampler2D _PortalGBufferTexture2;
				uniform sampler2D _PortalGBufferTexture3;
				uniform sampler2D_float _PortalDepthTexture;

				uniform float4x4 _PortalMatrix_I;

				v2f vert(appdata v)
				{
					v2f o;
					o.pos = UnityObjectToClipPos(v.vertex);
					o.screenUV = ComputeNonStereoScreenPos(o.pos);
					return o;
				}

				float4 FixNormals(float4 packedNormal) {
					// GBuffer 2 contains WORLD space normals from the perspective of the portal camera.
					// When we apply the portal transformation, those normals get transformed too,
					// so we must apply the inverse portal transformation to undo that.
					// Normals are packed in the gbuffer, and must be unpacked before applying the
					// correction, and must be repacked afterwards.

					float3 normal = packedNormal * 2 - 1;
					normal = mul((float3x3)_PortalMatrix_I, normal);
					normal = (normal + 1) * 0.5;

					return float4(normal, packedNormal.w);
				}

				FrameData frag(v2f i)
				{
					FrameData o;
					o.gbuffer0 = tex2Dproj(_PortalGBufferTexture0, i.screenUV);
					o.gbuffer1 = tex2Dproj(_PortalGBufferTexture1, i.screenUV);
					o.gbuffer2 = tex2Dproj(_PortalGBufferTexture2, i.screenUV);
					o.gbuffer3 = tex2Dproj(_PortalGBufferTexture3, i.screenUV);
					o.depth = tex2Dproj(_PortalDepthTexture, i.screenUV);

					//o.extraDepth = i.pos.z;
					return o;
				}
				ENDCG
			}
    }
}
