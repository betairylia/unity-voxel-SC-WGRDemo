module WGR

using .Threads
using HTTP
using Printf: @printf, @sprintf
using NPZ

const serverAddr = "127.0.0.1:4444"

const Block = UInt32

mutable struct I3{T<:Integer}
    x::T
    y::T
    z::T
end

mutable struct BlockBuffer{T<:Integer}
    blks::Array{Tuple{I3{T}, Block}} # Just use T for I3's type ;x;;
    bufferSize::T
    count::T
    lk::ReentrantLock
end

BlockBuffer(bSize) = BlockBuffer(Array{Tuple{I3{Int64}, Block}}(undef, bSize), bSize, 0, ReentrantLock())

function getBlock(x::Integer, y::Integer, z::Integer)
    r = HTTP.request("GET", "http://$serverAddr/?pos=$x,$y,$z")
    return parse(UInt32,String(r.body))
end

function GetBlockWithin(xMin, yMin, zMin, xMax, yMax, zMax)
    r = HTTP.request("GET", "http://$serverAddr/numpy?min=$xMin,$yMin,$zMin&max=$(xMax+1),$(yMax+1),$(zMax+1)")
    # @show r.body
    open("tmp.npy", "w") do file
        write(file, r.body)
    end
    
    return npzread("tmp.npy") .% Block
end
    
function flush!(buffer::BlockBuffer)
    # println("Flush")
    reqText = ""
    for b in 1:buffer.count
        req = @sprintf "%d,%d,%d,%d\n" buffer.blks[b][1].x buffer.blks[b][1].y buffer.blks[b][1].z buffer.blks[b][2]
        reqText *= req
    end
    # @show reqText
    HTTP.request("POST", "http://$serverAddr/batched", [], reqText)
    
    # Empty it
    buffer.count = 0
end

# function setBlocks!(buffer::BlockBuffer, x::Integer, y::Integer, z::Integer, block::Block)
#     buffer.blks[buffer.count + 1] = (I3(x, y, z), block)
#     buffer.count += 1
#     if buffer.count >= buffer.bufferSize
#         flush!(buffer)
#     end
# end
function setBlocks!(buffer, x, y, z, block)
    lock(buffer.lk) do
        buffer.blks[buffer.count + 1] = (I3(x, y, z), block)
        buffer.count += 1
        if buffer.count >= buffer.bufferSize
            flush!(buffer)
        end
    end
end

function setBlock(x::Integer, y::Integer, z::Integer, block::Block)
    HTTP.request("POST", "http://$serverAddr/?pos=$x,$y,$z&blk=$block", [])
end

function refreshNd!(buffer::BlockBuffer, arr, pos, full = false)
    
    # Account for 1-based indexing
    pos = [p - 1 for p in pos]
    
    if full
        shape = size(arr)
        for x in 1:shape[1], y in 1:shape[2], z in 1:shape[3]
            setBlocks!(buffer, x + pos[1], y + pos[2], z + pos[3], arr[x, y, z])
        end
    else
        nonz = findall(x -> x>0, arr)
        for p in nonz
            setBlocks!(buffer, p[1] + pos[1], p[2] + pos[2], p[3] + pos[3], arr[p])
        end
    end
    
    flush()

end

function compose(r, g, b, a)
    return min(r, 255) * 0x01000000 + min(g, 255) * 0x00010000 + min(b, 255) * 0x00000100 + min(a, 255) * 0x00000001
end

function decompose(c)
    return ((c >> 24) & 255), ((c >> 16) & 255), ((c >> 8) & 255), ((c) & 255)
end

function darken(blk::Block, amount)
    r, g, b, a = decompose(blk)
    return compose(
        UInt8(round(r * (1 - amount))), 
        UInt8(round(g * (1 - amount))), 
        UInt8(round(b * (1 - amount))), 
        a
    )
end

end