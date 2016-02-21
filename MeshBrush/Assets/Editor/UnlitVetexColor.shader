Shader "Unlit/Unlit Vetex Color"
{
	Properties
	{

	}
	SubShader
	{
		Tags 
		{ 
			"Queue" = "Transparent" 
			"RenderType" = "Transparent"
		}

		LOD 100
		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct VertIn
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
			};

			struct VertOut
			{
				float4 position : POSITION;
				float4 color : COLOR;
			};

			VertOut vert (VertIn v)
			{
				VertOut o;
				o.position = mul(UNITY_MATRIX_MVP, v.vertex);
				o.color = v.color;
				return o;
			}

			struct FragOut
			{
				float4 color : COLOR;
			};
			
			FragOut frag (VertOut i)
			{
				FragOut o;
				o.color = i.color;
				return o;
			}
			ENDCG
		}
	}
}
