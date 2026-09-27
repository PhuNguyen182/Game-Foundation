Shader "DracoRuan/UISystem/TutorialMask"
{
    // Punches a hole in an otherwise-dimmed full-screen overlay, shaped by _MaskTex's alpha
    // (REWRITE_PLAN.md mục 5 bước 10: "tutorial highlight hook (mask lỗ + chặn input ngoài
    // vùng)" - user instruction: any sprite can drive the hole's shape, not a hard-coded
    // circle/rect). One shared material/shader instance is reused across every tutorial step;
    // only _MaskRect (screen-space placement) and _MaskTex (which sprite) change per step - see
    // UITutorialMaskController.
    Properties
    {
        _Color ("Dim Color", Color) = (0, 0, 0, 0.75)

        // Declared (but unused by the fragment shader below) only because UnityEngine.UI.Image
        // always calls Material.SetTexture("_MainTex", spriteTexture) on the material it's
        // given, sprite atlas support included - a material with no _MainTex property logs an
        // error on every such call (harmless in normal Play, but strict under
        // UnityEngine.TestTools.LogAssert in PlayMode tests, which fails the test on any
        // unexpected error log). Real masking is driven entirely by _MaskTex/_MaskRect below.
        _MainTex ("Unused - present only so Image's own SetTexture call doesn't error", 2D) = "white" {}

        _MaskTex ("Mask Sprite (alpha = hole shape)", 2D) = "white" {}

        // Screen-space rect the mask sprite is placed into: xy = normalized center (0-1 of
        // screen), zw = normalized half-size (0-1 of screen). Computed from the highlighted
        // RectTransform's screen corners each time a step changes - see
        // UITutorialMaskController.ComputeMaskRect.
        _MaskRect ("Mask Rect (center.xy, halfSize.zw, normalized 0-1)", Vector) = (0.5, 0.5, 0.1, 0.1)

        // Soft edge width in normalized screen units, so the cutout doesn't have a hard aliased
        // border - purely cosmetic, 0 = hard edge.
        _Softness ("Edge Softness", Range(0, 0.2)) = 0.02
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
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
                float4 vertex : SV_POSITION;
                float2 screenUv : TEXCOORD0;
                float2 maskUv : TEXCOORD1;
            };

            fixed4 _Color;
            sampler2D _MaskTex;
            float4 _MaskRect;
            float _Softness;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // v.uv is this quad's own 0-1 UV (the overlay is expected to be one full-screen
                // quad/Image), which doubles as normalized screen position here.
                o.screenUv = v.uv;
                o.maskUv = (v.uv - (_MaskRect.xy - _MaskRect.zw)) / (2.0 * _MaskRect.zw);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 d = abs(i.screenUv - _MaskRect.xy) - _MaskRect.zw;
                float outsideRect = max(d.x, d.y);

                fixed maskAlpha = 0;
                if (i.maskUv.x >= 0 && i.maskUv.x <= 1 && i.maskUv.y >= 0 && i.maskUv.y <= 1)
                    maskAlpha = tex2D(_MaskTex, i.maskUv).a;

                // Inside the mask rect, punch by the sprite's own alpha; outside, punch nothing
                // (mask sprite doesn't tile past its own rect) - softness only feathers the
                // rect's own edge, not the sprite shape itself, keeping the shader simple.
                float edgeFade = saturate(outsideRect / max(_Softness, 1e-5));
                fixed hole = maskAlpha * (1 - edgeFade);

                fixed alpha = _Color.a * (1 - hole);
                return fixed4(_Color.rgb, alpha);
            }
            ENDCG
        }
    }
}
