using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Hash1f
{
    public static float GetNoise(int _x, int _y, int _z)
    {
        uint __x = unchecked((uint)_x);
        uint __y = unchecked((uint)_y);
        uint __z = unchecked((uint)_z);

        // p = 1103515245U*((p.xyz >> 1U)^(p.yzx));
        uint x = 1103515245u * ((uint)(__x >> 1) ^ (uint)__y);
        uint y = 1103515245u * ((uint)(__y >> 1) ^ (uint)__z);
        uint z = 1103515245u * ((uint)(__z >> 1) ^ (uint)__x);

        // uint h32 = 1103515245U*((p.x^p.z)^(p.y>>3U));
        uint h = 1103515245U * ((x ^ z) ^ (y >> 3));
        // return h32^(h32 >> 16);
        h = h ^ (h >> 16);

        return (float)h * (1.0f / (float)(0xffffffffU));
    }

    public static float GetNoise(int _x, int _y)
    {
        uint __x = unchecked((uint)_x);
        uint __y = unchecked((uint)_y);

        // p = 1103515245U*((p.xyz >> 1U)^(p.yzx));
        uint x = 1103515245u * ((uint)(__x >> 1) ^ (uint)__y);
        uint y = 1103515245u * ((uint)(__y >> 1) ^ (uint)__x);

        // uint h32 = 1103515245U*((p.x^p.z)^(p.y>>3U));
        uint h = 1103515245U * ((x) ^ (y >> 3));
        // return h32^(h32 >> 16);
        h = h ^ (h >> 16);

        return (float)h * (1.0f / (float)(0xffffffffU));
    }

    public static float GetNoise(int _x)
    {
        uint __x = unchecked((uint)_x);

        // p = 1103515245U*((p.xyz >> 1U)^(p.yzx));
        uint x = 1103515245u * ((uint)(__x >> 1) ^ (uint)__x);

        // uint h32 = 1103515245U*((p.x^p.z)^(p.y>>3U));
        uint h = 1103515245U * ((x) ^ (x >> 3));
        // return h32^(h32 >> 16);
        h = h ^ (h >> 16);

        return (float)h * (1.0f / (float)(0xffffffffU));
    }
}
