Shader "Unlit/ShowGbuffer"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
			ZTest Always
			
            CGPROGRAM
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

			sampler2D _CameraGBufferTexture2;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
				o.screenUV = ComputeNonStereoScreenPos(o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2Dproj(_CameraGBufferTexture2, i.screenUV);
                return col;
            }
            ENDCG
        }
    }
}
