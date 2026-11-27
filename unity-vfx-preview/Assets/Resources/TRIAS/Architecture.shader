// TRIAS architectural surface. Applies the Blender-baked fractured-ceramic plate atlas
// (tools/blender/build-plate-atlas.py) in object-aligned metric triplanar space, so primitive and
// authored meshes alike carry plates at a constant human scale regardless of their UVs.
// Primitive boxes (_BoxEdge = 1) also receive a shaded rounded bevel along their real edges.
Shader "TRIAS/Architecture"
{
    Properties
    {
        _Color("Plate", Color) = (.8,.77,.71,1)
        _AltColor("Alternate plate", Color) = (.62,.55,.46,1)
        _AltRate("Alternate plate rate", Range(0,1)) = .12
        _SeamColor("Seam", Color) = (.05,.055,.06,1)
        _SeamStrength("Seam strength", Range(0,1)) = 1
        _SeamTone("Seam follows plate value", Range(0,1)) = 0
        _SideSeams("Plate pattern on vertical faces", Range(0,1)) = 1
        _HardwareColor("Fasteners", Color) = (.45,.46,.47,1)
        _DataTex("Plate data (id, ao, edge, height)", 2D) = "gray" {}
        [Normal] _NormalTex("Plate normal", 2D) = "bump" {}
        _Scale("Atlas period (m)", Float) = 4
        _NormalStrength("Normal strength", Range(0,2)) = 1
        _Glossiness("Smoothness", Range(0,1)) = .5
        _GlossVar("Per-plate smoothness variance", Range(0,1)) = .18
        _Metallic("Metallic", Range(0,1)) = 0
        _PlateVar("Per-plate value variance", Range(0,1)) = .16
        _EdgeWear("Chamfer wear", Range(0,1)) = .35
        _AOStrength("Cavity occlusion", Range(0,1)) = .9
        _BoxEdge("Primitive box bevel", Range(0,1)) = 0
        _BevelWidth("Bevel width (m)", Float) = .045
        _Grime("Ground grime", Range(0,1)) = .25
        _PhaseTint("Phase plate tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.5
        #pragma multi_compile_instancing

        sampler2D _DataTex, _NormalTex;
        half4 _Color, _AltColor, _SeamColor, _HardwareColor, _PhaseTint;
        half _AltRate, _Scale, _NormalStrength, _Glossiness, _GlossVar, _Metallic, _PlateVar, _EdgeWear, _AOStrength;
        half _BoxEdge, _BevelWidth, _Grime, _SeamStrength, _SeamTone, _SideSeams;

        struct Input
        {
            float3 boxPos;
            float3 boxNormal;
            float3 objPos;
            float3 worldPos;
            float3 worldNormal;
            INTERNAL_DATA
        };

        void axes(out float3 ax, out float3 ay, out float3 az, out float3 scale)
        {
            float3 cx = unity_ObjectToWorld._m00_m10_m20;
            float3 cy = unity_ObjectToWorld._m01_m11_m21;
            float3 cz = unity_ObjectToWorld._m02_m12_m22;
            scale = float3(length(cx), length(cy), length(cz));
            ax = cx / scale.x; ay = cy / scale.y; az = cz / scale.z;
        }

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            float3 ax, ay, az, scale;
            axes(ax, ay, az, scale);
            float3 origin = unity_ObjectToWorld._m03_m13_m23;
            float3 d = mul(unity_ObjectToWorld, v.vertex).xyz - origin;
            // Offset the pattern per object so neighbouring modules never repeat the same plates.
            float3 seed = frac(sin(dot(floor(origin * 2.3), float3(12.9898, 78.233, 37.719))) * float3(43758.5, 22578.1, 19642.3)) * _Scale;
            o.boxPos = float3(dot(d, ax), dot(d, ay), dot(d, az)) + seed;
            float3 wn = UnityObjectToWorldNormal(v.normal);
            o.boxNormal = float3(dot(wn, ax), dot(wn, ay), dot(wn, az));
            o.objPos = v.vertex.xyz;
        }

        float3 WorldToTangentNormalVector(Input IN, float3 normal)
        {
            float3 t2w0 = WorldNormalVector(IN, float3(1, 0, 0));
            float3 t2w1 = WorldNormalVector(IN, float3(0, 1, 0));
            float3 t2w2 = WorldNormalVector(IN, float3(0, 0, 1));
            float3x3 t2w = float3x3(t2w0, t2w1, t2w2);
            return normalize(mul(t2w, normal));
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 ax, ay, az, scale;
            axes(ax, ay, az, scale);
            float3 n = normalize(IN.boxNormal);
            float3 w = pow(abs(n), 8);
            w /= dot(w, 1);
            float3 p = IN.boxPos / _Scale;
            float2 uvX = p.zy * float2(n.x >= 0 ? -1 : 1, 1);
            float2 uvY = p.xz * float2(n.y >= 0 ? 1 : -1, 1);
            float2 uvZ = p.xy * float2(n.z >= 0 ? 1 : -1, 1);

            half4 data = tex2D(_DataTex, uvX) * w.x + tex2D(_DataTex, uvY) * w.y + tex2D(_DataTex, uvZ) * w.z;
            half3 tX = UnpackNormal(tex2D(_NormalTex, uvX));
            half3 tY = UnpackNormal(tex2D(_NormalTex, uvY));
            half3 tZ = UnpackNormal(tex2D(_NormalTex, uvZ));
            tX.xy *= _NormalStrength * float2(n.x >= 0 ? -1 : 1, 1);
            tY.xy *= _NormalStrength * float2(n.y >= 0 ? 1 : -1, 1);
            tZ.xy *= _NormalStrength * float2(n.z >= 0 ? 1 : -1, 1);
            // Whiteout blend into the object frame.
            float3 nX = float3(abs(n.x) * tX.z * sign(n.x), tX.y + n.y, tX.x + n.z);
            float3 nY = float3(tY.x + n.x, abs(n.y) * tY.z * sign(n.y), tY.y + n.z);
            float3 nZ = float3(tZ.x + n.x, tZ.y + n.y, abs(n.z) * tZ.z * sign(n.z));
            float3 local = normalize(nX * w.x + nY * w.y + nZ * w.z);
            // Decks keep their slab pattern on walkable faces only; slab edges read as one cast edge.
            half pattern = lerp(_SideSeams, 1, saturate(abs(n.y) * 1.5 - .3));
            local = normalize(lerp(n, local, pattern));

            half id = data.r, ao = data.g, chamfer = data.b, height = data.a;
            half hardware = 1 - step(.045, id);
            half phase = step(.9, id);
            half seam = 1 - smoothstep(.06, .42, height);
            seam *= pattern;
            hardware *= pattern;
            phase *= pattern;
            id = lerp(.47, id, pattern);

            // Rounded bevel on the real edges of primitive boxes.
            half bevel = 0;
            UNITY_BRANCH
            if (_BoxEdge > 0)
            {
                float3 extent = scale * .5;
                float3 q = extent - abs(IN.objPos) * scale;
                float3 face = abs(n);
                float dx = face.x > .5 ? 1e3 : q.x, dy = face.y > .5 ? 1e3 : q.y, dz = face.z > .5 ? 1e3 : q.z;
                float m = min(dx, min(dy, dz));
                float3 dir = m == dx ? float3(sign(IN.objPos.x), 0, 0) : m == dy ? float3(0, sign(IN.objPos.y), 0) : float3(0, 0, sign(IN.objPos.z));
                float width = min(_BevelWidth, min(extent.x, min(extent.y, extent.z)) * .45);
                bevel = (1 - smoothstep(0, width, m)) * _BoxEdge;
                local = normalize(lerp(local, normalize(local + dir * 1.2), bevel));
            }

            float3 world = local.x * ax + local.y * ay + local.z * az;
            o.Normal = WorldToTangentNormalVector(IN, world);

            half3 albedo = _Color.rgb * (1 + (id - .47) * _PlateVar * 2);
            half alt = step(1 - _AltRate, frac(id * 13.731)) * (1 - phase);
            albedo = lerp(albedo, _AltColor.rgb * (1 + (frac(id * 31.7) - .5) * _PlateVar), alt);
            albedo *= lerp(half3(1, 1, 1), _PhaseTint.rgb, phase);
            albedo = lerp(albedo, albedo * 1.07 + .025, saturate(chamfer + bevel) * _EdgeWear);
            albedo = lerp(albedo, lerp(_SeamColor.rgb, albedo * .42, _SeamTone), seam * _SeamStrength);
            albedo = lerp(albedo, _HardwareColor.rgb, hardware);
            // Settled grime toward the lower world-up side of each plate field.
            half grime = saturate(1 - (IN.worldPos.y - floor(IN.worldPos.y / 3.3) * 3.3) / 1.1) * _Grime * (1 - abs(n.y));
            albedo *= 1 - grime * .22;

            half gloss = _Glossiness + (frac(id * 7.77) - .5) * _GlossVar;
            gloss = lerp(gloss, gloss * .6, saturate(chamfer + bevel) * _EdgeWear);
            gloss = lerp(gloss, .15, seam * _SeamStrength);
            gloss = lerp(gloss, .55, hardware);

            o.Albedo = albedo;
            o.Metallic = lerp(_Metallic, .85, hardware);
            o.Smoothness = saturate(gloss);
            o.Occlusion = lerp(1, ao * ao, _AOStrength * pattern) * (1 - seam * .5 * _SeamStrength);
        }
        ENDCG
    }
    FallBack "Standard"
}
