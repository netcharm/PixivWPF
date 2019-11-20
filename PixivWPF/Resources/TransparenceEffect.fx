// Transparence shader 

// Object Declarations

sampler2D implicitInput : register(s0);
float4 transColor : register(c0);

//--------------------------------------------------------------------------------------
// Pixel Shader
//--------------------------------------------------------------------------------------
float4 main(float2 uv : TEXCOORD) : COLOR
{
    float4 color = tex2D(implicitInput, uv);

    float4 result;
    if (color.r == transColor.r && color.g == transColor.g && color.b == transColor.b )//&& color.a == transColor.a)
    {
        result.a = 0;
        result.r = 0xff;
        result.g = 0xff;
        result.b = 0xff;
    }
    else
    {
        result = color;
    }

    return result;
}