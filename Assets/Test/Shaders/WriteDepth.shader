Shader "Test/WriteDepth"
{
	SubShader
	{
		Tags { "Queue"="Geometry-10"}
		Cull Off
		ZWrite On
		ColorMask 0
		//ZTest Always

		Pass {}
	}
}
