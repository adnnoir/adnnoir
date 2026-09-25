// Version transparente (marquages, verre, sang, tags sur les murs).
Shader "E7/LitFade"
{
    Properties
    {
        _Color ("Couleur", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        [Normal] _BumpMap ("Normales", 2D) = "bump" {}
        _BumpScale ("Force des normales", Float) = 1
        _Metallic ("Métal", Range(0,1)) = 0
        _Roughness ("Rugosité", Range(0,1)) = 0.6
        _RoughMap ("Carte de rugosité", 2D) = "white" {}
        [HDR] _EmissionColor ("Émission", Color) = (0,0,0,1)
        _EmissionMap ("Carte d'émission", 2D) = "white" {}
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Faces", Float) = 2
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 300
        Cull [_Cull]
        ZWrite Off
        Offset -1, -1
        CGPROGRAM
        #pragma surface surf Standard alpha:fade
        #pragma target 3.0
        sampler2D _MainTex, _BumpMap, _RoughMap, _EmissionMap;
        fixed4 _Color;
        half _Metallic, _Roughness, _BumpScale;
        half4 _EmissionColor;
        struct Input { float2 uv_MainTex; float2 uv_BumpMap; float2 uv_RoughMap; float2 uv_EmissionMap; };
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            half3 n = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
            n.xy *= _BumpScale;
            o.Normal = normalize(n);
            o.Smoothness = 1.0 - saturate(_Roughness * tex2D(_RoughMap, IN.uv_RoughMap).g);
            o.Metallic = _Metallic;
            o.Emission = _EmissionColor.rgb * tex2D(_EmissionMap, IN.uv_EmissionMap).rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
