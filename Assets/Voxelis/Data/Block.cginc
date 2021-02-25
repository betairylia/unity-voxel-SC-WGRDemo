#ifndef BLOCK
#define BLOCK

struct Block
{
    uint data; // ushort + ushort
};

uint GetBlockID(Block b)
{
    return (b.data & 0x0000FFFF);
}

uint GetBlockMeta(Block b)
{
    return b.data >> 16;
}

// Is Block b a solid block ?
bool IsSolidBlock(Block b)
{
    uint tmp = b.data & 0xFFFF0000;
    //       ID != 0               and  Alpha > 0 (if solid color block)
    //return ((tmp & 0xFFFF0000 != 0) && (tmp == 0xFFFF0000 && b.data & 0x0000000F > 0));
    return (b.data > 0);
}

// Currently same as above
// Should we cull faces neighboring Block b ?
bool IsSolidRenderingBlock(Block b)
{
    return IsSolidBlock(b);
}

// Currently same as above
// Should we render Block b ?
bool IsRenderableBlock(Block b)
{
    return IsSolidBlock(b);
}

// Convert a float4 color to Block
Block ToID(float4 color)
{
    Block b;
    b.data = 
        ((uint)(clamp(color.r, 0, 1) * 15) << 12) +
        ((uint)(clamp(color.g, 0, 1) * 15) <<  8) +
        ((uint)(clamp(color.b, 0, 1) * 15) <<  4) +
        ((uint)(clamp(color.a, 0, 1) * 15) <<  0);

    b.data = (b.data << 16) + 0x0000FFFF;

    // Stupid fix
    if (color.a < 0.02) { b.data = uint(0x00000000); }

    return b;
}

#endif