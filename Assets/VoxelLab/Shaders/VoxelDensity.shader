// =====================================================================
//  VoxelDensity.shader / VoxelMaterial.shader  (combinados aquí)
//  VoxelLab :: Overlays
//
//  Density: pinta la malla en escala de grises mostrando alfa de color
//           de vértice (usada por overlays de densidad).
//  Material: pinta el color de vértice "puro" (sin shading) para que
//            los ids de material se distingan a primera vista.
// =====================================================================
Shader "VoxelLab/Density"
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
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; };
            struct v2f { float4 pos:SV_POSITION; float4 col:COLOR; };
            v2f vert(appdata v){ v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.col=v.color; return o; }
            fixed4 frag(v2f i):SV_Target {
                float lum = dot(i.col.rgb, float3(0.299,0.587,0.114));
                return fixed4(lum, lum, lum, 1);
            }
            ENDCG
        }
    }
}
