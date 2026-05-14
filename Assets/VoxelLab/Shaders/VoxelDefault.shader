// =====================================================================
//  VoxelOverlays.shader
//  VoxelLab :: Overlays
//
//  Tres sub-shaders unlit muy simples utilizados por OverlayController:
//      - VoxelLab/Default        : usa el color de vertice (que ya
//                                  incluye color de material).
//      - VoxelLab/Wireframe      : color sólido para wireframe (Unity
//                                  muestra triángulos en GL.Wire mode
//                                  desde el editor; aquí pintamos
//                                  alpha bajo para falsa rejilla).
//      - VoxelLab/Density        : color en grises modulado por la
//                                  componente alpha del color de
//                                  vértice (interpretada como densidad).
//
//  Estos shaders intencionalmente evitan PBR: el laboratorio prioriza
//  legibilidad sobre fidelidad.
// =====================================================================
Shader "VoxelLab/Default"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float4 col : COLOR; };

            v2f vert(appdata v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.col = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                float ndl = saturate(dot(normalize(i.n), normalize(float3(0.5,1,0.3))));
                float3 c = i.col.rgb * (0.4 + 0.6 * ndl);
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
}
