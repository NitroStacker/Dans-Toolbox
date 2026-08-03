Shader "Dans Toolbox/Retro VFX/Uber"
{
    Properties
    {
        _MainTex ("Particle Mask", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HDR] _EdgeColor ("Edge Glow", Color) = (1,0.45,0.08,1)
        _Emission ("Emission", Range(0,8)) = 1
        _EdgeGlow ("Edge Strength", Range(0,2)) = 0.35
        _DissolveTex ("Dissolve / Noise", 2D) = "gray" {}
        _Dissolve ("Dissolve", Range(0,1)) = 0
        _DissolveWidth ("Dissolve Edge", Range(0.001,0.5)) = 0.1
        _FlowTex ("Flow Map", 2D) = "gray" {}
        _FlowSpeed ("Flow Speed", Vector) = (0,0,0,0)
        _InvFade ("Soft Particle Fade", Range(0.01,10)) = 1
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 5
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 10
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "RetroVfxUnlit"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend [_SrcBlend] [_DstBlend]
            Cull Off
            ZWrite Off
            ZTest LEqual

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_particles
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float4 projected : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _DissolveTex;
            sampler2D _FlowTex;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            fixed4 _Color;
            fixed4 _EdgeColor;
            float _Emission;
            float _EdgeGlow;
            float _Dissolve;
            float _DissolveWidth;
            float4 _FlowSpeed;
            float _InvFade;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                output.projected = ComputeScreenPos(output.vertex);
                COMPUTE_EYEDEPTH(output.projected.z);
                UNITY_TRANSFER_FOG(output, output.vertex);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 flowSample = tex2D(_FlowTex, input.uv + _Time.y * _FlowSpeed.xy).rg * 2.0 - 1.0;
                float2 flowedUv = input.uv + flowSample * 0.035 * saturate(length(_FlowSpeed.xy));
                fixed4 mask = tex2D(_MainTex, flowedUv);
                float noise = tex2D(_DissolveTex, input.uv + _Time.y * _FlowSpeed.zw).r;
                float edgeWidth = max(0.001, _DissolveWidth);
                float coverage = smoothstep(_Dissolve - edgeWidth, _Dissolve + edgeWidth, noise);
                float edge = saturate(1.0 - abs(noise - _Dissolve) / edgeWidth) * step(0.0001, _Dissolve);
                fixed4 result = input.color * mask;
                result.rgb = result.rgb * _Emission + _EdgeColor.rgb * edge * _EdgeGlow;
                result.a *= coverage;

                #if defined(SOFTPARTICLES_ON)
                    float sceneZ = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(input.projected)));
                    float partZ = input.projected.z;
                    result.a *= saturate(_InvFade * (sceneZ - partZ));
                #endif

                clip(result.a - 0.001);
                UNITY_APPLY_FOG_COLOR(input.fogCoord, result, fixed4(0,0,0,0));
                return result;
            }
            ENDCG
        }
    }

    Fallback "Particles/Standard Unlit"
}
