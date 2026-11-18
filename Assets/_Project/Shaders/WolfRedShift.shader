// 늑대 2페이즈 색 반전: 그림 속 초록 계열(연두 번개·이끼·눈)만 골라 빨강으로 바꾼다.
// _WolfRed(전역 값, 0~1)를 올리면 서서히 붉게 물든다. 나머지 색(회색 털, 갈색 가지)은 그대로.
// SpriteRenderer용 (Sprites/Default와 같은 뒤집기·색 입히기를 그대로 쓴다)
Shader "PersistentWorld/WolfRedShift"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            float _WolfRed;

            float3 RgbToHsv(float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            float3 HsvToRgb(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 tex = SampleSpriteTexture(IN.texcoord);
                // 그림은 미리 곱해진 알파가 아니므로 색 판단은 그대로 한다
                float3 hsv = RgbToHsv(tex.rgb);
                float green = smoothstep(0.14, 0.18, hsv.x) * (1.0 - smoothstep(0.45, 0.49, hsv.x)) * smoothstep(0.22, 0.34, hsv.y);
                // 빨강으로 (밝은 곳은 살짝 주황빛)
                float3 red = HsvToRgb(float3(frac((hsv.z - 0.5) * 0.06 + 1.0), saturate(hsv.y * 1.05), hsv.z));
                tex.rgb = lerp(tex.rgb, red, green * _WolfRed);
                fixed4 c = tex * IN.color;
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
