#ifndef NOISE
#define NOISE

#include "../../RNGs/Hashes.cginc"

//==========================================================================================
// noises
//==========================================================================================

// value noise, and its analytical derivatives
float4 noised(float3 x)
{
    float3 p = floor(x);
    float3 w = frac(x);

    float3 u = w * w * w * (w * (w * 6.0 - 15.0) + 10.0);
    float3 du = 30.0 * w * w * (w * (w - 2.0) + 1.0);

    float n = p.x + 317.0 * p.y + 157.0 * p.z;

    float a = hash11(n + 0.0);
    float b = hash11(n + 1.0);
    float c = hash11(n + 317.0);
    float d = hash11(n + 318.0);
    float e = hash11(n + 157.0);
    float f = hash11(n + 158.0);
    float g = hash11(n + 474.0);
    float h = hash11(n + 475.0);

    float k0 = a;
    float k1 = b - a;
    float k2 = c - a;
    float k3 = e - a;
    float k4 = a - b - c + d;
    float k5 = a - c - e + g;
    float k6 = a - b - e + f;
    float k7 = -a + b + c - d + e - f - g + h;

    return float4(-1.0 + 2.0 * (k0 + k1 * u.x + k2 * u.y + k3 * u.z + k4 * u.x * u.y + k5 * u.y * u.z + k6 * u.z * u.x + k7 * u.x * u.y * u.z),
        2.0 * du * float3(k1 + k4 * u.y + k6 * u.z + k7 * u.y * u.z,
            k2 + k5 * u.z + k4 * u.x + k7 * u.z * u.x,
            k3 + k6 * u.x + k5 * u.y + k7 * u.x * u.y));
}

float noise(float3 x)
{
    float3 p = floor(x);
    float3 w = frac(x);

    float3 u = w * w * w * (w * (w * 6.0 - 15.0) + 10.0);

    float n = p.x + 317.0 * p.y + 157.0 * p.z;

    float a = hash11(n + 0.0);
    float b = hash11(n + 1.0);
    float c = hash11(n + 317.0);
    float d = hash11(n + 318.0);
    float e = hash11(n + 157.0);
    float f = hash11(n + 158.0);
    float g = hash11(n + 474.0);
    float h = hash11(n + 475.0);

    float k0 = a;
    float k1 = b - a;
    float k2 = c - a;
    float k3 = e - a;
    float k4 = a - b - c + d;
    float k5 = a - c - e + g;
    float k6 = a - b - e + f;
    float k7 = -a + b + c - d + e - f - g + h;

    return -1.0 + 2.0 * (k0 + k1 * u.x + k2 * u.y + k3 * u.z + k4 * u.x * u.y + k5 * u.y * u.z + k6 * u.z * u.x + k7 * u.x * u.y * u.z);
}

float3 noised(float2 x)
{
    float2 p = floor(x);
    float2 w = frac(x);

    float2 u = w * w * w * (w * (w * 6.0 - 15.0) + 10.0);
    float2 du = 30.0 * w * w * (w * (w - 2.0) + 1.0);

    float a = hash12(p + float2(0, 0));
    float b = hash12(p + float2(1, 0));
    float c = hash12(p + float2(0, 1));
    float d = hash12(p + float2(1, 1));

    float k0 = a;
    float k1 = b - a;
    float k2 = c - a;
    float k4 = a - b - c + d;

    return float3(-1.0 + 2.0 * (k0 + k1 * u.x + k2 * u.y + k4 * u.x * u.y),
        2.0 * du * float2(k1 + k4 * u.y,
            k2 + k4 * u.x));
}

float noise(float2 x)
{
    float2 p = floor(x);
    float2 w = frac(x);
    float2 u = w * w * w * (w * (w * 6.0 - 15.0) + 10.0);

#if 0
    p *= 0.3183099;
    float kx0 = 50.0 * frac(p.x);
    float kx1 = 50.0 * frac(p.x + 0.3183099);
    float ky0 = 50.0 * frac(p.y);
    float ky1 = 50.0 * frac(p.y + 0.3183099);

    float a = frac(kx0 * ky0 * (kx0 + ky0));
    float b = frac(kx1 * ky0 * (kx1 + ky0));
    float c = frac(kx0 * ky1 * (kx0 + ky1));
    float d = frac(kx1 * ky1 * (kx1 + ky1));
#else
    float a = hash12(p + float2(0, 0));
    float b = hash12(p + float2(1, 0));
    float c = hash12(p + float2(0, 1));
    float d = hash12(p + float2(1, 1));
#endif

    return -1.0 + 2.0 * (a + (b - a) * u.x + (c - a) * u.y + (a - b - c + d) * u.x * u.y);
}

//==========================================================================================
// fbm constructions
//==========================================================================================

static const float3x3 m3 = {
    0.00, 0.80, 0.60,
    -0.80, 0.36, -0.48,
    -0.60, -0.48, 0.64 };
static const float3x3 m3i = {
    0.00, -0.80, -0.60,
    0.80, 0.36, -0.48,
    0.60, -0.48, 0.64 };
static const float2x2 m2 = {
    0.80, 0.60,
    -0.60, 0.80 };
static const float2x2 m2i = {
    0.80, -0.60,
    0.60, 0.80 };

//------------------------------------------------------------------------------------------

float fbm_4(float3 x, float s = 0.5)
{
    float f = 2.0;
    float a = 0.0;
    float b = 0.5;
    for (int i = 0; i < 4; i++)
    {
        float n = noise(x);
        a += b * n;
        b *= s;
        x = f * mul(m3, x);
    }
    return a;
}

float4 fbmd_8(float3 x)
{
    float f = 1.92;
    float s = 0.5;
    float a = 0.0;
    float b = 0.5;
    float3  d = float3(0, 0, 0);
    float3x3  m = float3x3(1.0, 0.0, 0.0,
        0.0, 1.0, 0.0,
        0.0, 0.0, 1.0);
    for (int i = 0; i < 7; i++)
    {
        float4 n = noised(x);
        a += b * n.x;          // accumulate values		
        d += b * mul(m, n.yzw);      // accumulate derivatives
        b *= s;
        x = f * mul(m3, x);
        m = f * mul(m3i, m);
    }
    return float4(a, d);
}

float fbm_9(float2 x)
{
    float f = 1.9;
    float s = 0.55;
    float a = 0.0;
    float b = 0.5;
    for (int i = 0; i < 9; i++)
    {
        float n = noise(x);
        a += b * n;
        b *= s;
        x = f * mul(m2, x);
    }
    return a;
}

float3 fbmd_9(float2 x)
{
    float f = 1.9;
    float s = 0.55;
    float a = 0.0;
    float b = 0.5;
    float2  d = float2(0.0, 0);
    float2x2  m = float2x2(1.0, 0.0, 0.0, 1.0);
    for (int i = 0; i < 9; i++)
    {
        float3 n = noised(x);
        a += b * n.x;          // accumulate values		
        d += b * mul(m, n.yz);       // accumulate derivatives
        b *= s;
        x = f * mul(m2, x);
        m = f * mul(m2i, m);
    }
    return float3(a, d);
}

float fbm_4(float2 x)
{
    float f = 1.9;
    float s = 0.55;
    float a = 0.0;
    float b = 0.5;
    for (int i = 0; i < 4; i++)
    {
        float n = noise(x);
        a += b * n;
        b *= s;
        x = f * mul(x, m2);
    }
    return a;
}

float sphere(float3 p, float3 o, float r)
{
    return length(p - o) - r;
}

float vCyclinder(float3 p, float3 o, float l, float r)
{
    return max((length(p.xz - o.xz) - r), abs(p.y - o.y) - (l / 2));
}

#endif //NOISE