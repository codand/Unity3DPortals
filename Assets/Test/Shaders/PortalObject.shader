Shader "Portal/Object"
{
	SubShader
	{
		Pass
		{
			Tags{ "RenderType" = "Opaque" }

			Stencil{
				Ref 0
				Comp NotEqual
			}
		}
	}
	FallBack "VertexLit"
}
