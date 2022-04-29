module WGR

using HTTP
using Printf: @printf, @sprintf

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
end

BlockBuffer(bSize) = BlockBuffer(Array{Tuple{I3{Int64}, Block}}(undef, bSize), bSize, 0)

function getBlock(x::Integer, y::Integer, z::Integer)
    r = HTTP.request("GET", "http://$serverAddr/?pos=$x,$y,$z")
    return parse(UInt32,String(r.body))
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
    buffer.blks[buffer.count + 1] = (I3(x, y, z), block)
    buffer.count += 1
    if buffer.count >= buffer.bufferSize
        flush!(buffer)
    end
end

function setBlock(x::Integer, y::Integer, z::Integer, block::Block)
    HTTP.request("POST", "http://$serverAddr/?pos=$x,$y,$z&blk=$block", [])
end

end