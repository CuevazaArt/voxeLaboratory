// =====================================================================
//  VoxelWireframe.shader
//  VoxelLab :: Overlays
//
//  Pseudo-wireframe basado en barycentric coords aproximadas.
//  Para overlay sobre la malla existente.
// =====================================================================
Shader "VoxelLab/Wireframe"
{
    Properties { _LineColor ("Line", Color) = (0,1,0,1) _Fill ("Fill", Color) = (0,0,0,0.2) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _LineColor; float4 _Fill;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // Aprox: usamos posición local para crear una rejilla en cada cara.
                o.uv = v.vertex.xy + v.vertex.zz * 0.5;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                float2 g = abs(frac(i.uv) - 0.5);
                float d = min(g.x, g.y);
                float w = smoothstep(0.02, 0.0, d);
                return lerp(_Fill, _LineColor, w);
            }
            ENDCG
        }
    }
}
