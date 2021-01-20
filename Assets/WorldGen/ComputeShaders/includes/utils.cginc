﻿#ifndef UTILS
#define UTILS

// Convert a float4 color to uint ID
unsigned int ToID(float4 color)
{
	return
		((unsigned int)(clamp(color.r, 0, 1) * 255) << 24) +
		((unsigned int)(clamp(color.g, 0, 1) * 255) << 16) +
		((unsigned int)(clamp(color.b, 0, 1) * 255) << 8) +
		((unsigned int)(clamp(color.a, 0, 1) * 255) << 0);
}

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

#endif //UTILS