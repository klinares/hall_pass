// A soft three-stop sky for the 3D places: blue overhead, a pale warm band at the horizon, a warm haze below it,
// and a faint glow where the sun is. Unlit, no textures, one draw. (Phase 8.1 look pass; the built-in procedural sky
// goes lime at the horizon under a warm sun.)
Shader "WorldTour/SkyGradient"
{
    Properties
    {
        _Top ("Top", Color) = (0.36, 0.55, 0.95, 1)
        _Mid ("Upper sky", Color) = (0.62, 0.78, 1.0, 1)
        _Horizon ("Horizon", Color) = (1.0, 0.88, 0.82, 1)
        _Bottom ("Below", Color) = (0.92, 0.82, 0.8, 1)
        _SunColor ("Sun glow", Color) = (1.0, 0.8, 0.55, 1)
        _SunDir ("Sun direction (towards the sun)", Vector) = (-0.5, 0.6, -0.6, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Top, _Mid, _Horizon, _Bottom, _SunColor;
            float4 _SunDir;

            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                float up = saturate(h);
                fixed3 c = lerp(_Horizon.rgb, _Mid.rgb, saturate(up * 4.0));
                c = lerp(c, _Top.rgb, saturate((up - 0.15) * 1.6));
                c = lerp(c, _Bottom.rgb, saturate(-h * 5.0));
                float s = saturate(dot(d, normalize(_SunDir.xyz)));
                c += _SunColor.rgb * (pow(s, 6.0) * 0.28 + pow(s, 60.0) * 0.5);
                return fixed4(c, 1);
            }
            ENDHLSL
        }
    }
}
