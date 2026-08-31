// Arazi ve proplar için mobil dostu shader.
//
// Neden gerekli: URP/Lit vertex renklerini yok sayar, bizim arazi yükseklik
// bantları ve prop tonlarımız vertex color'da. Ayrıca GPU instancing ile
// instance başına renk verebilmek için _BaseColor'ın instancing buffer'ında
// olması gerekiyor — hazır shader'larda yok.
//
// Maliyet: tek yönlü ışık + basit ambient. Specular yok, normal map yok.
// Mobilde neredeyse bedava, yine de hacim hissi veriyor.

Shader "DreamCar/VertexLit"
{
    Properties
    {
        _BaseMap        ("Doku", 2D) = "white" {}
        _BaseColor      ("Taban Renk", Color) = (1,1,1,1)
        _VertexColorMix ("Vertex Renk Katkısı", Range(0,1)) = 1
        _AmbientBoost   ("Ambient Güçlendirme", Range(0,2)) = 1
        _Cutoff         ("Alpha Kesme", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"        = "Opaque"
            "RenderPipeline"    = "UniversalPipeline"
            "UniversalMaterialType" = "SimpleLit"
            "Queue"             = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float4 color      : COLOR;
                float  fogFactor  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // Instance başına değişebilen özellikler bu blokta olmalı — aksi halde
            // DrawMeshInstanced ile gönderilen renk dizisi shader'a ulaşmaz.
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            // Weather.Update her karede Shader.SetGlobalFloat ile yazar (0 = kuru,
            // 1 = sırılsıklam). Global değişken materyal başına değil sahne başına
            // olduğu için CBUFFER dışında, dosya kapsamında tanımlanmalı —
            // ShadowCaster geçişindeki _LightDirection ile aynı kalıp.
            float _GlobalWetness;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float  _VertexColorMix;
                float  _AmbientBoost;
                float  _Cutoff;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS   = nrm.normalWS;
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.color      = IN.color;
                OUT.fogFactor  = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 baseColor = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);

                half4 albedo = tex * baseColor;
                // Vertex renk katkısı ayarlanabilir: arazide 1, düz yüzeylerde 0
                albedo.rgb = lerp(albedo.rgb, albedo.rgb * IN.color.rgb, _VertexColorMix);

                // Islaklık: ıslak yüzey ışığın çoğunu aynasal olarak ileri yansıttığı
                // için dağınık (diffuse) bileşeni azalır — göze koyulaşma olarak gelir.
                // Tek lerp, mobilde maliyeti ölçülemeyecek kadar küçük.
                half wetness = saturate(_GlobalWetness);
                albedo.rgb = lerp(albedo.rgb, albedo.rgb * half3(0.52h, 0.55h, 0.60h), wetness);

                float3 normalWS = normalize(IN.normalWS);

                // Gölge koordinatı — ana ışık gölgesi açıksa kullanılır
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                    Light mainLight = GetMainLight(shadowCoord);
                #else
                    Light mainLight = GetMainLight();
                #endif

                half ndotl = saturate(dot(normalWS, mainLight.direction));
                // Yarım lambert — arka yüzler tamamen siyah olmasın, mobilde daha hoş
                half wrapped = ndotl * 0.5h + 0.5h;
                half3 diffuse = mainLight.color * (wrapped * wrapped) * mainLight.shadowAttenuation;

                // Ambient de hafif kırılır: yağmurda gökyüzü kapalıdır, yüzeye her
                // yönden gelen ışık azalır. Abartmadan — sadece ıslak hissi pekiştirir.
                half3 ambient = SampleSH(normalWS) * _AmbientBoost * lerp(1.0h, 0.82h, wetness);

                half3 finalColor = albedo.rgb * (diffuse + ambient);
                finalColor = MixFog(finalColor, IN.fogFactor);

                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }

        // Gölge dökümü — büyük yapılar gölge yapsın diye
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_instancing
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            ShadowVaryings shadowVert(ShadowAttributes IN)
            {
                ShadowVaryings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

                OUT.positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    OUT.positionCS.z = min(OUT.positionCS.z, OUT.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    OUT.positionCS.z = max(OUT.positionCS.z, OUT.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                return OUT;
            }

            half4 shadowFrag(ShadowVaryings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    // URP yoksa (Built-in pipeline) bu shader derlenmez; yedek olarak standart bir
    // shader'a düşülür ki proje hiç açılmaz hale gelmesin.
    Fallback "Universal Render Pipeline/Simple Lit"
}
