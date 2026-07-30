Shader "Supercent/Rendering/Planar Shadow" 
{
    Properties
    {
        _LightDirection ("Light Direction", Vector) = (0, 0, 0, 0)
        _ShadowColor ("Shadow Color", Color) = (0, 0, 0, 0)
        _ShadowPivotOffset ("Position Offset (X, Y, Z)", Vector) = (0, 0, 0, 0)
    }
    SubShader {
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
        
        Pass {
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
            #pragma shader_feature _USE_OFFSET
            
            #include "UnityCG.cginc"
            
            float3 _LightDirection;
            float4 _ShadowColor;
            float3 _ShadowPivotOffset;
            
            #ifdef _USE_OFFSET
            #define APPLY_OFFSET
            #endif
            
            struct vsOut
            {
                float4 pos : SV_POSITION;
            };
            
            vsOut vert(appdata_base v)
            {
                vsOut o;
                
                float4 vPosWorld = mul(unity_ObjectToWorld, v.vertex);
                float3 vPos = vPosWorld.xyz + (_LightDirection * (vPosWorld.y - _ShadowPivotOffset.y));
            
                #ifdef APPLY_OFFSET
                vPos.x += _ShadowPivotOffset.x;
                vPos.z += _ShadowPivotOffset.z;
                #endif
            
                o.pos = mul(UNITY_MATRIX_VP, float4(vPos.x, _ShadowPivotOffset.y, vPos.z, 1));
                
                return o;
            }
            
            fixed4 frag(vsOut i) : COLOR
            {
                return _ShadowColor;
            }
            
            ENDCG
        }
    }
}
