#ifndef OLD_GRID_ECOSYSTEM
#define OLD_GRID_ECOSYSTEM

#include "noise.cginc"
#include "utils.cginc"

static const float EcoGap = 8.0, EcoGrid = 12.0, EcoRadius = 0.35, EcoNoise = 0.2;

float EvalEcoGrid(int2 gridPos, float3 p, inout uint blk);
float EvalGeometry(float3 pos, out float height, inout uint blk);

float BallEcoSystem(float3 pos, float height, inout uint blk)
{
    int2 s = ceil((pos.xz - EcoGrid) / EcoGap);
    int2 e = floor(pos.xz / EcoGap);

    float d = EcoGrid;

    for (int gX = s.x; gX <= e.x; gX++)
    {
        for (int gZ = s.y; gZ <= e.y; gZ++)
        {
            float _d = EvalEcoGrid(int2(gX, gZ), pos, blk);
            d = min(d, _d);
        }
    }

    return d;
}

float EvalEcoGrid(int2 gridPos, float3 p, inout uint blk)
{
    float3 rng = hash32(float2(gridPos.x, gridPos.y));
    if (rng.x > 0.6)
    {
        float3 root = float3(gridPos.x * EcoGap + ((rng.y - 0.5) * (1 - 2 * EcoRadius) + 0.5) * EcoGrid, p.y, gridPos.y * EcoGap + ((rng.z - 0.5) * (1 - 2 * EcoRadius) + 0.5) * EcoGrid);
        float h = 0;
        uint b = 0;

        float size = EcoGrid * EcoRadius;

        EvalGeometry(root, h, b);
        //EvalGeometry(p, h, b);
        root.y = h;

        if (h > 10)
        {
            //float trunk = vCyclinder(p, root + float3(0, size, 0), size * 2.5, size * 0.2);
            //float leaves = sphere(p, root + float3(0, size * 3, 0), size) + fbm_4(p / (0.15 * EcoGrid)) * (EcoNoise * EcoGrid);
            float trunk = 1.0f; // No trunk
            float leaves = sphere(p, root + float3(0, 0, 0), size) + fbm_4(p / (0.15 * EcoGrid)) * (EcoNoise * EcoGrid);
            if (trunk > leaves&& leaves < 0)
            {
                blk = (blk == 0 ? ToID(float4(0.255, 0.665, 0.165, 1)) : blk);
                return leaves;
            }
            else if (trunk < 0)
            {
                blk = (blk == 0 ? ToID(float4(0.665, 0.665, 0.165, 1)) : blk);
                return trunk;
            }
        }
    }

    return EcoGrid;
}

#endif //OLD_GRID_ECOSYSTEM