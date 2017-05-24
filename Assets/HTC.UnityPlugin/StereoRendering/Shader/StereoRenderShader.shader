//========= Copyright 2016, HTC Corporation. All rights reserved. ===========

Shader "Custom/StereoRenderShader"
{
    Properties
    {
        _LeftEyeTexture("Left Eye Texture", 2D) = "white" {}
        _RightEyeTexture("Right Eye Texture", 2D) = "white" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"
    #include "UnityInstancing.cginc"
    ENDCG

    SubShader
    {
        Tags{ "RenderType" = "Opaque" }

        Pass
        {
			//Cull OFF

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile __ STEREO_RENDER
            #pragma target 3.0

            struct v2f
            {
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_full i, out float4 outpos : SV_POSITION)
            {
                v2f o;
                outpos = mul(UNITY_MATRIX_MVP, i.vertex);

                o.uv = i.texcoord.xy;
                return o;
            }

            sampler2D _LeftEyeTexture;
            sampler2D _RightEyeTexture;

            fixed4 frag(v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
            {
                float2 screenUV = screenPos.xy / _ScreenParams.xy;
                fixed4 col = fixed4(0, 0, 0, 0);

                #ifdef UNITY_SINGLE_PASS_STEREO
                    if (unity_StereoEyeIndex == 0)
                    {
                        screenUV.x *= 2;
                        col = tex2D(_LeftEyeTexture, screenUV);
                    }
                    else
                    {
                        screenUV.x = (screenUV.x - 0.5) * 2;
                        col = tex2D(_RightEyeTexture, screenUV);
                    }
                #else
                    if (unity_CameraProjection[0][2] < 0)
                    {
                        col = tex2D(_LeftEyeTexture, screenUV);
                    }
                    else
                    {
                        col = tex2D(_RightEyeTexture, screenUV);
                    }
                #endif

                return col;
            }

            ENDCG
        }
    }

    FallBack "Diffuse"
}
