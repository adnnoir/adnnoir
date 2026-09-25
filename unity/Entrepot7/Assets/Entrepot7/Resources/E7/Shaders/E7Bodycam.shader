// Effet « caméra piéton » : objectif grand angle, aberration chromatique, grain, vignette,
// halo lumineux (bloom), sang à l'écran, flash aveuglant et mort.
Shader "Hidden/E7/Bodycam"
{
    Properties { _MainTex ("", 2D) = "black" {} _Bloom ("", 2D) = "black" {} }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex; float4 _MainTex_TexelSize;
    sampler2D _Bloom;
    float _K, _CA, _Grain, _Hurt, _Dead, _Blind, _Vig, _Time2, _Threshold, _BloomAmt, _Exposure, _Aspect;
    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
    v2f vert (appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }
    float hash (float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
    float2 barrel (float2 uv, float k)
    {
        float2 c = uv * 2.0 - 1.0; float2 a = float2(c.x * _Aspect, c.y);
        float r2 = dot(a, a); float rm = _Aspect * _Aspect + 1.0;
        c *= (1.0 + k * r2) / (1.0 + k * rm);
        return c * 0.5 + 0.5;
    }
    // 0 : extraction des zones claires (bloom)
    float4 fragPre (v2f i) : SV_Target
    {
        float3 c = 0;
        float2 d = _MainTex_TexelSize.xy;
        c += tex2D(_MainTex, i.uv + float2(-d.x, -d.y)).rgb; c += tex2D(_MainTex, i.uv + float2(d.x, -d.y)).rgb;
        c += tex2D(_MainTex, i.uv + float2(-d.x, d.y)).rgb; c += tex2D(_MainTex, i.uv + float2(d.x, d.y)).rgb;
        c *= 0.25;
        float l = max(c.r, max(c.g, c.b));
        return float4(c * saturate((l - _Threshold) / max(l, 1e-4)), 1);
    }
    // 1 : flou de réduction
    float4 fragDown (v2f i) : SV_Target
    {
        float2 d = _MainTex_TexelSize.xy;
        float3 c = tex2D(_MainTex, i.uv).rgb * 4.0;
        c += tex2D(_MainTex, i.uv + float2(-d.x, -d.y)).rgb; c += tex2D(_MainTex, i.uv + float2(d.x, -d.y)).rgb;
        c += tex2D(_MainTex, i.uv + float2(-d.x, d.y)).rgb; c += tex2D(_MainTex, i.uv + float2(d.x, d.y)).rgb;
        return float4(c / 8.0, 1);
    }
    // 2 : composition finale
    float4 fragFinal (v2f i) : SV_Target
    {
        float2 uv = i.uv;
        float2 cc = uv * 2.0 - 1.0;
        float3 col;
        col.r = tex2D(_MainTex, barrel(uv, _K + _CA)).r;
        col.g = tex2D(_MainTex, barrel(uv, _K)).g;
        col.b = tex2D(_MainTex, barrel(uv, _K - _CA)).b;
        col += tex2D(_Bloom, barrel(uv, _K)).rgb * _BloomAmt;
        col *= _Exposure;
        // courbe « film » douce
        col = col * (2.51 * col + 0.03) / (col * (2.43 * col + 0.59) + 0.14);
        float l = dot(col, float3(0.299, 0.587, 0.114));
        col = lerp(l.xxx, col, 0.74);
        col = (col - 0.5) * 1.08 + 0.5;
        col *= float3(0.95, 1.0, 1.05);
        col = max(col, 0.02);
        float n = hash(uv * _ScreenParams.xy + frac(_Time2 * 7.13) * 91.0) - 0.5;
        col += n * _Grain * (1.25 - l);
        float vig = smoothstep(1.55, 0.35, length(cc * float2(1.0, 0.85)));
        col *= lerp(1.0, vig, _Vig);
        // blessure : bords rouges qui pulsent
        float edge = smoothstep(0.45, 1.3, length(cc));
        col = lerp(col, col * float3(1.2, 0.25, 0.2) + float3(0.12, 0, 0), edge * _Hurt);
        col = lerp(col, dot(col, float3(0.33, 0.33, 0.33)).xxx * float3(0.8, 0.3, 0.3), _Dead);
        col = lerp(col, float3(1, 1, 0.97), _Blind);
        // lignes de balayage légères
        col *= 0.985 + 0.015 * sin(uv.y * _ScreenParams.y * 1.5);
        return float4(saturate(col), 1);
    }
    ENDCG
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPre
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDown
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragFinal
            ENDCG }
    }
}
