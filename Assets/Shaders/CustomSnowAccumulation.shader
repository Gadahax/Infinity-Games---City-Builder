Shader "Custom/SnowAccumulation"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _SnowColor ("Snow Color", Color) = (1,1,1,1)
        _SnowAmount ("Snow Amount", Range(0,1)) = 0.0
        _SnowDirection ("Snow Direction", Vector) = (0,1,0,0)
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        
        sampler2D _MainTex;
        
        struct Input
        {
            float2 uv_MainTex;
            float3 worldNormal;
        };
        
        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        fixed4 _SnowColor;
        float _SnowAmount;
        float3 _SnowDirection;
        
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Calculate how aligned this surface is with the snow direction
            float snowAlignment = dot(normalize(IN.worldNormal), normalize(_SnowDirection));
            
            // Only accumulate snow on upward-facing surfaces
            float snowCoverage = saturate(snowAlignment - (1 - _SnowAmount * 2));
            
            // Sample base texture
            fixed4 baseColor = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            
            // Blend between base color and snow color
            o.Albedo = lerp(baseColor.rgb, _SnowColor.rgb, snowCoverage);
            
            // Adjust properties for snow areas
            o.Metallic = lerp(_Metallic, 0.0, snowCoverage); 
            o.Smoothness = lerp(_Glossiness, 0.3, snowCoverage); // Snow is less glossy
            o.Alpha = baseColor.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}