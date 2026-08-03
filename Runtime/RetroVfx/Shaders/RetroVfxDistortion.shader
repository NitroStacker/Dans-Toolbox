Shader "Dans Toolbox/Retro VFX/Distortion"
{
    Properties
    {
        _MainTex ("Distortion Mask", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,0.16)
        _Strength ("Strength", Range(0, 1)) = 0.12
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+80"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        GrabPass { "_RetroVfxGrabTexture" }

        Pass
        {
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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
                float4 grabPos : TEXCOORD1;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _RetroVfxGrabTexture;
            float4 _RetroVfxGrabTexture_TexelSize;
            fixed4 _Color;
            float _Strength;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.grabPos = ComputeGrabScreenPos(output.vertex);
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 mask = tex2D(_MainTex, input.uv);
                float2 centered = input.uv * 2.0 - 1.0;
                float2 offset = centered * mask.a * input.color.a * _Strength * 24.0;
                offset *= _RetroVfxGrabTexture_TexelSize.xy;
                float4 grab = input.grabPos;
                grab.xy += offset * grab.w;
                fixed4 scene = tex2Dproj(_RetroVfxGrabTexture, UNITY_PROJ_COORD(grab));
                scene.a = saturate(mask.a * input.color.a);
                return scene;
            }
            ENDCG
        }
    }

    Fallback "Particles/Standard Unlit"
}
