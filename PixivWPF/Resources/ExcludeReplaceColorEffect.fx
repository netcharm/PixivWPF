// ExcludeReplaceColor shader 

// Object Declarations

sampler2D implicitInput : register(s0);
float threshold : register(c0);
float4 srcColor : register(c1);
float4 dstColor : register(c2);

//--------------------------------------------------------------------------------------
// Pixel Shader
//--------------------------------------------------------------------------------------
float4 main(float2 uv : TEXCOORD) : COLOR
{
    float4 color = tex2D(implicitInput, uv);

    float bias_a = srcColor.a * threshold;
    float bias_r = srcColor.r * threshold;
    float bias_g = srcColor.g * threshold;
    float bias_b = srcColor.b * threshold;
    
    float4 result;
    if ( //srcColor.a - bias_a >= color.a && color.a >= srcColor.a + bias_a &&
         srcColor.r - bias_r >= color.r && color.r >= srcColor.r + bias_r &&
         srcColor.g - bias_g >= color.g && color.g >= srcColor.g + bias_g &&
         srcColor.b - bias_b >= color.b && color.b >= srcColor.b + bias_b )
    {
        result = dstColor;
    }
    else
    {
        result = srcColor;
    }

    return result;
}