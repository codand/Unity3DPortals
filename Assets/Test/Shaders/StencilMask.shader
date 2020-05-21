Shader "Portals/StencilMask"
{
	SubShader
	{
		Pass
		{
			Tags{ "RenderType" = "Opaque" }
			ZWrite Off
			ColorMask 0

			Stencil{
				Comp Always
				Pass IncrSat
			}
		}
	}
	FallBack "VertexLit"
}
