// Draws a book page with the current video frame in its window (BookPageVideo, TG-180).
// Used only through Graphics.Blit, which keeps UV (0,0) at the image's bottom left on every graphics API.
// Lives in Resources so the runtime Resources.Load keeps it in builds.
Shader "Hidden/PsycheVR/BookPageVideoComposite"
{
    Properties
    {
        _MainTex ("Page", 2D) = "white" {}
        _VideoTex ("Video frame", 2D) = "black" {}
        _Window ("Window in page UV (x, y, width, height)", Vector) = (0, 0, 1, 1)
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _VideoTex;
            float4 _Window;

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 local = (i.uv - _Window.xy) / _Window.zw;
                fixed4 page = tex2D(_MainTex, i.uv);
                fixed4 video = tex2D(_VideoTex, saturate(local));
                bool inside = all(local >= 0) && all(local <= 1);
                return inside ? video : page;
            }
            ENDCG
        }
    }
}
