using Formatting

mutable struct TestCity{T<:Integer} <: Specie{T}
    g::Vector{T}
    
    #=
    Building A - Size 5x5x5 ~ 8x6x8; Color blue ; [3sX 2sY 3sZ] [pX] [pZ]
    Building B - Size 2x3x2 ~ 5x4x5; Color green; [3sX 2sY 3sZ] [pX] [pZ]
    =#
end

function findInHeightMap(pX, pZ, sX, sZ, map::Map)
    
    shape = editsize(map)
    shape = [shape[1], shape[3]]
    
    minH = 255
    maxH = -255
    
    # inbound check
    if pX+sX-1 > shape[1] || pZ+sZ-1 > shape[2]
        return minH, maxH
    end
    
    # Find min & maximum height
    for x in pX:(pX+sX-1), z in pZ:(pZ+sZ-1)
        @inbounds currH = map.heightMap[x + map.min[1] - 1, z + map.min[3] - 1] - map.min[2] + 1
        minH = min(minH, currH)
        maxH = max(maxH, currH)
    end
    
    return minH, maxH
end
    
function addBlkToMap!(pos, blk, map::Map)
    tpos = pos+map.min.-1
    map.map[(tpos)...] = blk
    if blk != AirBlock && map.heightMap[tpos[1], tpos[3]] < tpos[2]
        map.heightMap[tpos[1], tpos[3]] = tpos[2]
    end
end

function populate!(s::TestCity, map::Map)
    
    i = 1 # Pointer
    shape = editsize(map)
    
    sizeX = 1
    sizeY = 1
    sizeZ = 1
    posX  = 1
    posY  = 1
    posZ  = 1
    blk   = 0x02
    
    g = s.g
    
    while i <= length(g)
        
        # Generate building info
        if g[i] == 0 # Termination
            break
        elseif g[i] % 2 == 0
        # if g[i] % 2 == 0
            # Building A
            bsize = g[i+1]
            sizeX = ((bsize & 0b11100000) >> 5) % 4 + 5
            sizeY = ((bsize & 0b00011000) >> 3) % 2 + 5
            sizeZ = ((bsize & 0b00000111) >> 0) % 4 + 5
            posX  = g[i+2] % shape[1] + 1
            posZ  = g[i+3] % shape[3] + 1
            blk   = 0x03
            i += 4
        elseif g[i] % 2 == 1
            # Building B
            bsize = g[i+1]
            sizeX = ((bsize & 0b11100000) >> 5) % 4 + 2
            sizeY = ((bsize & 0b00011000) >> 3) % 2 + 3
            sizeZ = ((bsize & 0b00000111) >> 0) % 4 + 2
            posX  = g[i+2] % shape[1] + 1
            posZ  = g[i+3] % shape[3] + 1
            blk   = 0x04
            i += 4
        end
        
        sizeX = convert(Int64, sizeX)
        sizeY = convert(Int64, sizeY)
        sizeZ = convert(Int64, sizeZ)
         posX = convert(Int64,  posX)
         posZ = convert(Int64,  posZ)
        
        # Place the building
        # printfmt("{:1d} x {:1d} x {:1d} @ {:2d}, {:2d} : 0x{:08x}", sizeX, sizeY, sizeZ, posX, posZ, blk)
        minH, maxH = findInHeightMap(posX, posZ, sizeX, sizeZ, map)
        
        if minH != maxH || (maxH + sizeY) > shape[2]
            # print(" - Failed\n")
            continue
        end
        
        posY = maxH+1
        
        for x in posX:(posX+sizeX-1), y in posY:(posY+sizeY-1), z in posZ:(posZ+sizeZ-1)
            addBlkToMap!([x, y, z], blk, map)
        end
        
        # print("\n")
    end
    
    # return input, heightMap
end

# mutable struct MCMCEnergy <: Enviorment
#     energy::Alfehim.AbstractVoxelDistribution
# end

# function fit(e::Enviorment, input::Array{Block})
