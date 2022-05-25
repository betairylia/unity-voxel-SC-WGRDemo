module AlfheimG

const Block = UInt8
const AirBlock = 0x01

using Random

abstract type Specie{T<:Integer} end
    # g::Vector{T}

abstract type Enviorment end

# function populate(s::Specie, input::Array{Block})
# function fit(e::Enviorment, input::Array{Block})

function mutate!(s::Specie, p::Float64 = 0.1)
    
    for i in 1:length(s.g)
        if Random.rand() < p
            s.g[i] = Random.rand(UInt8)
        end
    end
    
end

function offspring(s1::Specie, s2::Specie, crossoverMix::Float64 = 0.05)

    s = deepcopy(s1)
    froms1 = true
    
    for i in 1:length(s1.g)
        
        if Random.rand() < crossoverMix
            froms1 = !froms1
        end
        
        if !froms1
            s.g[i] = s2.g[i]
        end
    
    end
    
    return s

end

#######################################################

include("./Map.jl")
include("./BasicTest.jl")

end