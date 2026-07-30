Shader "Supercent/Rendering/Planar Shadow Baked" 
{
    Properties
    {
        _ShadowColor ("Shadow Color", Color) = (0, 0, 0, 0)
    }
    SubShader 
    {
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
        
        Pass
        {   
            ZWrite Off
            ZTest LEqual 
            Blend SrcAlpha OneMinusSrcAlpha
            
            Stencil
            {
                Ref 128           // 비트 7 = 1 (0b10000000)
                ReadMask 128      // 비트 7만 읽기 (다른 비트 무시)
                WriteMask 128     // 비트 7만 쓰기 (다른 비트 보존)
                Comp NotEqual     // 비트 7이 0이면 렌더링
                Pass Replace      // 비트 7을 1로 설정
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            
            float4 _ShadowColor;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _ShadowColor;
            }
            ENDCG
        }
    }
}
