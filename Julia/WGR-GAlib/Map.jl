mutable struct Map{T<:Integer}

    map::Array{T}
    heightMap::Array{T}
    
    # Editable area
    min::Vector
    max::Vector
    
end

function heightMap(input::Array{Block})
    
    shape = size(input)
    
    # Get heightmap
    hmap = zeros(UInt8, shape[1], shape[3])

    for x in 1:shape[1], y in 1:shape[2], z in 1:shape[3]
        if input[x, y, z] != AirBlock
            if y > hmap[x, z]
                hmap[x, z] = y
            end
        end
    end
    
    return hmap
end

function editsize(map::Map)
    return map.max - map.min .+ 1
end

# function set!(map::Map, pos, )

Map(map::Array) = Map(
    map,
    heightMap(map),
    
    [1, 1, 1],
    [i for i in size(map)] # Tuple -> Array. Better methods?
)

Map(map, min, max) = Map(
    map,
    heightMap(map),
    
    min,
    max
)