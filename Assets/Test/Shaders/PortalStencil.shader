Shader "Portal/Stencil"
{
	SubShader
	{
		Pass
		{
			Tags{ "RenderType" = "Opaque" }
			ZWrite Off
			ColorMask 0

			Stencil{
				Comp always
				Pass IncrSat
			}
		}
	}
	FallBack "VertexLit"
}
