Shader "Unlit/HorizontalThreeColorGradientPreciseFade"
{
    Properties
    {
          _MainTex("Texture", 2D) = "white" {}
        _ColorA("Left Color", Color) = (0,0,1,1)
        _ColorB("Middle Color", Color) = (0,1,0,1)
        _ColorC("Right Color", Color) = (1,0,0,1)
        _Split1("Middle Start", Range(0,1)) = 0.3
        _Split2("Middle End", Range(0,1)) = 0.7
        _Fade("Fade Width", Range(0.001, 0.2)) = 0.02
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _ColorA;
            fixed4 _ColorB;
            fixed4 _ColorC;
            float _Split1;  
            float _Split2;
            float _Fade;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv =  TRANSFORM_TEX(v.uv, _MainTex);;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float x = i.uv.x;
                fixed4 color;

                // Tính phần blend vào từ A → B
                float tA = smoothstep(_Split1 - _Fade, _Split1, x);
                // Tính phần blend ra từ B → C
                float tC = smoothstep(_Split2, _Split2 + _Fade, x);

                // Blend 3 vùng lại
                color = _ColorA * (1.0 - tA);
                color += _ColorB * (tA * (1.0 - tC));
                color += _ColorC * tC;

                return color;
            }
            ENDCG
        }
    }
}
