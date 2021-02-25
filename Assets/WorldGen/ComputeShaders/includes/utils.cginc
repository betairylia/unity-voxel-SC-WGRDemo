#ifndef UTILS
#define UTILS

#include "../../../Voxelis/Data/Block.cginc"

// IQ's smooth minium function. 
float smin(float a, float b, float s) {

    float h = clamp(0.5 + 0.5 * (b - a) / s, 0., 1.);
    return lerp(b, a, h) - h * (1.0 - h) * s;
}

float smin(float a, float b, float s, float4 colorA, float4 colorB, out float4 colorOut)
{
	float h = clamp(0.5 + 0.5 * (b - a) / s, 0., 1.);
	colorOut = float4(lerp(colorB, colorA, h).rgb - (h * (1.0 - h) * s).rrr, 1.);
	return lerp(b, a, h) - h * (1.0 - h) * s;
}

// where = 0: B was used completely; where = 1: A was used completely.
float smin(float b, float a, float s, float4 colorB, float4 colorA, out float4 colorOut, out float where)
{
	where = clamp(0.5 + 0.5 * (b - a) / s, 0., 1.);
	colorOut = float4(lerp(colorB, colorA, where).rgb - (where * (1.0 - where) * s).rrr, 1.);
	return lerp(b, a, where) - where * (1.0 - where) * s;
}

// Smooth maximum, based on IQ's smooth minimum.
float smax(float a, float b, float s) {

    float h = clamp(0.5 + 0.5 * (a - b) / s, 0., 1.);
    return lerp(b, a, h) + h * (1.0 - h) * s;
}

float smax(float a, float b, float s, float4 colorA, float4 colorB, out float4 colorOut)
{
	float h = clamp(0.5 + 0.5 * (a - b) / s, 0., 1.);
	colorOut = float4(lerp(colorB, colorA, h).rgb - (h * (1.0 - h) * s).rrr, 1.);
	return lerp(b, a, h) + h * (1.0 - h) * s;
}

// where = 0: B was used completely; where = 1: A was used completely.
float smax(float b, float a, float s, float4 colorB, float4 colorA, out float4 colorOut, out float where)
{
	where = clamp(0.5 + 0.5 * (b - a) / s, 0., 1.);
	colorOut = float4(lerp(colorB, colorA, where).rgb - (where * (1.0 - where) * s).rrr, 1.);
	return lerp(b, a, where) - where * (1.0 - where) * s;
}

//https://www.shadertoy.com/view/lljSRV
float SmoothFloor(float x, float c)
{
	float a = frac(x);
	float b = floor(x);
	return ((pow(a, c) - pow(1.0 - a, c)) / 2.0) + b;
}

#endif //UTILS