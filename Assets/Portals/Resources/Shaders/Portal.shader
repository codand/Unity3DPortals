Shader "Portal/Portal"
{
	Properties
	{
		_DefaultTexture("DefaultTexture", 2D) = "white" {}
		_LeftEyeTexture("LeftEyeTexture", 2D) = "bump" {}
		_RightEyeTexture("RightEyeTexture", 2D) = "red" {}
		_TransparencyMask("TransparencyMask", 2D) = "white" {}
	}
	SubShader
	{
		Tags{
			"RenderType" = "Opaque"
			"Queue" = "Geometry+100"
			"IgnoreProjector" = "True"
		}

		//Pass
		//{
		//	Name "PortalStencilMask"

		//	ColorMask 0
		//	ZWrite Off
		//	Lighting Off
		//	ZTest Always
		//	Cull Front

		//	Stencil{
		//		Ref 1
		//		Comp Always
		//		Pass Replace
		//		Fail Zero
		//	}

		//	CGPROGRAM
		//	#pragma target 3.0

		//	#include "UnityCG.cginc"
		//	#include "PortalVRHelpers.cginc"

		//	#pragma multi_compile __ SAMPLE_PREVIOUS_FRAME SAMPLE_DEFAULT_TEXTURE

		//	#pragma vertex vertPortal
		//	#pragma fragment fragPortal

		//	ENDCG
		//}

		//Pass
		//{
		//	Name "PortalVisibleFaces"

		//	//Blend SrcAlpha OneMinusSrcAlpha
		//	ZWrite On
		//	Lighting Off
		//	Cull Back
		//	ZTest LEqual

		//	Stencil{
		//		Ref 0
		//		Comp Equal
		//		//Pass Zero
		//		//Fail Zero
		//	}

		//	CGPROGRAM
		//	#pragma target 3.0

		//	#include "UnityCG.cginc"
		//	#include "PortalVRHelpers.cginc"

		//	#pragma multi_compile __ SAMPLE_PREVIOUS_FRAME SAMPLE_DEFAULT_TEXTURE

		//	#pragma vertex vertPortal
		//	#pragma fragment fragPortal

		//	ENDCG
		//}

		//Pass
		//{
		//	Name "Portal Depth Only"

		//	ZWrite Off
		//	ColorMask 0
		//}
		//Pass
		//{
		//	Stencil {
		//		Ref 5
		//		Comp Always
		//		Pass Replace
		//		ZFail Keep
		//	}			
		//	Name "Portal Increment Stencil"
		//	Tags { "LightMode" = "ForwardBase" }
		//	ZWrite On
		//	ZTest LEqual
		//	ColorMask 0
		//}

		//Pass
		//{
		//	Name "Portal Zero Depth"
		//	Tags { "LightMode" = "ForwardBase" }
		//	ZWrite On
		//	ZTest LEqual
		//	//ColorMask 0

		//	CGPROGRAM

		//	#include "UnityCG.cginc"

		//	#pragma vertex Vertex
		//	#pragma fragment Fragment

		//	struct VertexInput
		//	{
		//		float4 vertex : POSITION;
		//		float4 uv : TEXCOORD0;
		//	};

		//	struct Varyings {
		//		float4 pos : SV_POSITION;
		//		float4 screenUV : TEXCOORD0;
		//		float4 objUV : TEXCOORD1;
		//	};

		//	Varyings Vertex(VertexInput input) {
		//		Varyings output = (Varyings)0;

		//		output.pos = UnityObjectToClipPos(input.vertex);
		//		output.objUV = input.uv;
		//		output.screenUV = ComputeNonStereoScreenPos(output.pos);

		//		return output;
		//	}

		//	float Fragment(Varyings input, out float depth : SV_Depth) : SV_Target {
		//		depth = 1;
		//		return 0;
		//	}


		//	ENDCG
		//}


		//Pass
		//{
		//	/*Stencil {
		//		Ref 100
		//		Pass Replace
		//	}			*/
		//	//Tags { "Queue" = "Geometry-1000"}
		//	Name "Portal ShadowCaster"
		//	Tags { "LightMode" = "ShadowCaster" }
		//	ZWrite On
		//	ZTest Always
		//	ColorMask 0
		//	Cull Front

		//	CGPROGRAM
		//	#include "UnityCG.cginc"

		//	#pragma vertex vert
		//	#pragma fragment frag

		//	#pragma multi_compile_shadowcaster

		//	float4 vert(in float4 vertex : POSITION) : POSITION {
		//		// Bias scales the portal slightly when rendering depth to prevent shadow bleeding
		//		float bias = 1.01;
		//		return UnityObjectToClipPos(vertex * bias);
		//	}

		//	float frag() : DEPTH{
		//		return 0;
		//	}
		//	ENDCG
		//}

		Pass
		{
			// Stencil prevents the backface from rendering if we've already seen the frontface
			Stencil{
				Comp Always
				Pass IncrSat
				ZFail IncrSat
			}

			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite On
			ZTest LEqual
			Lighting Off
			Cull Back
			Offset -1,-5

			CGPROGRAM
			#include "UnityCG.cginc"
			#include "PortalVRHelpers.cginc"

			#pragma multi_compile _ SAMPLE_PREVIOUS_FRAME SAMPLE_DEFAULT_TEXTURE

			#pragma vertex vertPortal
			#pragma fragment fragPortal
			ENDCG
		}


		Pass
		{
			Name "Portal ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }

			ZWrite On
			ZTest LEqual
			Cull Back

			CGPROGRAM
			#include "UnityCG.cginc"
			#include "PortalVRHelpers.cginc"

			#pragma vertex vertPortalShadowCaster
			#pragma fragment fragPortalShadowCaster
			ENDCG
		}

		//UsePass "Standard/SHADOWCASTER"
	}
	//FallBack "VertexLit"
}
