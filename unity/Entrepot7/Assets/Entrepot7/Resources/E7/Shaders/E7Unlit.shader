// Couleur sans éclairage (lumières, point rouge, flammes de tir, cônes de lumière). Mode de mélange réglable.
Shader "E7/Unlit"
{
    Properties
    {
        [HDR] _Color ("Couleur", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst", Float) = 0
        _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Faces", Float) = 2
        _Fog ("Brouillard", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST; float4 _Color; float _Fog;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 col : COLOR; UNITY_FOG_COORDS(1) };
            v2f vert (appdata_full v)
            {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = TRANSFORM_TEX(v.texcoord, _MainTex); o.col = v.color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            float4 frag (v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv) * _Color * i.col;
                if (_Fog > 0.5) { UNITY_APPLY_FOG_COLOR(i.fogCoord, c, fixed4(0,0,0,0)); }
                return c;
            }
            ENDCG
        }
    }
}
