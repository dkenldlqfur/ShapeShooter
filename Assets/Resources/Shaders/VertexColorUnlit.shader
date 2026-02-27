Shader "Custom/VertexColorUnlit"
{
    Properties
    {
        _EdgeWidth ("Edge Width", Range(0.0, 3.0)) = 0.8
        _EdgeColor ("Edge Color", Color) = (0, 0, 0, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100
        ZWrite On
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            float _EdgeWidth;
            fixed4 _EdgeColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv0 : TEXCOORD0; // Barycentric coordinates (b0, b1), b2 = 1 - b0 - b1
                float3 uv1 : TEXCOORD1; // Original/background color RGB
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 bary : TEXCOORD0;
                float3 origColor : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
            };

            // Hash function for procedural noise
            float hash(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            // 3D Value noise with smooth interpolation
            float vnoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = hash(i + float3(0, 0, 0));
                float n100 = hash(i + float3(1, 0, 0));
                float n010 = hash(i + float3(0, 1, 0));
                float n110 = hash(i + float3(1, 1, 0));
                float n001 = hash(i + float3(0, 0, 1));
                float n101 = hash(i + float3(1, 0, 1));
                float n011 = hash(i + float3(0, 1, 1));
                float n111 = hash(i + float3(1, 1, 1));

                float n00 = lerp(n000, n100, f.x);
                float n10 = lerp(n010, n110, f.x);
                float n01 = lerp(n001, n101, f.x);
                float n11 = lerp(n011, n111, f.x);

                float n0 = lerp(n00, n10, f.y);
                float n1 = lerp(n01, n11, f.y);

                return lerp(n0, n1, f.z);
            }

            // Fractal Brownian Motion for organic noise pattern
            float fbm(float3 p)
            {
                float v = 0;
                float a = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    v += a * vnoise(p);
                    p *= 2.17;
                    a *= 0.5;
                }
                return v;
            }

            // Compute wireframe edge factor from barycentric coordinates.
            // Returns 0.0 on edges, 1.0 in triangle interior.
            float wireframeEdge(float3 bary, float width)
            {
                float3 d = fwidth(bary);
                float3 f3 = smoothstep(d * (width - 0.5), d * (width + 0.5), bary);
                return min(min(f3.x, f3.y), f3.z);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.bary = v.uv0;
                o.origColor = v.uv1;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float fill = i.color.a;

                // Reconstruct full barycentric coords
                float3 bary = float3(i.bary.x, i.bary.y, 1.0 - i.bary.x - i.bary.y);

                // Compute wireframe edge blend: 0 = edge, 1 = interior
                float edgeFactor = wireframeEdge(bary, _EdgeWidth);

                float3 faceColor;

                // Fast path: fully filled, just use vertex color
                if (fill >= 0.99)
                {
                    faceColor = i.color.rgb;
                }
                // Fast path: zero fill, just use original color
                else if (fill <= 0.01)
                {
                    faceColor = i.origColor;
                }
                else
                {
                    // Compute barycentric distance from triangle center
                    float3 center = float3(0.3333, 0.3333, 0.3333);
                    float baryDist = length(bary - center) / 0.8165; // Normalize 0..1

                    // Organic noise based on world position for ice/bleeding look
                    float noise = fbm(i.worldPos * 8.0) * 0.35;

                    // Remap fill to 0..1.4 range so fill=1 fully covers the face
                    float threshold = fill * 1.4;

                    // Compare distance + noise vs threshold
                    float edge = baryDist + noise * (1.0 - fill * 0.5);
                    float mask = 1.0 - smoothstep(threshold - 0.12, threshold + 0.03, edge);

                    // Blend original color -> target color based on mask
                    faceColor = lerp(i.origColor, i.color.rgb, mask);

                    // Subtle glow at the spreading edge
                    float edgeGlow = smoothstep(threshold - 0.15, threshold - 0.05, edge)
                                   * (1.0 - smoothstep(threshold - 0.05, threshold + 0.03, edge));
                    faceColor += i.color.rgb * edgeGlow * 0.3;
                }

                // Blend wireframe edge color over face color
                float3 finalColor = lerp(_EdgeColor.rgb, faceColor, edgeFactor);

                return fixed4(finalColor, 1.0);
            }
            ENDCG
        }
    }
}
