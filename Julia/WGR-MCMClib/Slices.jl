# module Alfheim

abstract type SliceEnergy <: AbstractEnergy end

import Base.getindex, Base.push!
function push!(sW::SliceEnergy, pattern)
    if !haskey(sW.slicesCount, pattern)
        sW.slicesCount[pattern] = 0
    end
    sW.slicesCount[pattern] += 1
    sW.totalNum += 1
end

Base.getindex(sW::SliceEnergy, key) = haskey(sW.slices, key) ? sW.slices[key] : sW.eps

# Slice
function readSlices!(sW::SliceEnergy, arr)
    N = sW.N
    arrShape = size(arr)
    for x in 1:arrShape[1]-N, y in 1:arrShape[2]-N, z in 1:arrShape[3]-N
        nSlice = arr[x:x+N-1, y:y+N-1, z:z+N-1]
        for dx in [1:1:N, N:-1:1], dz in [1:1:N, N:-1:1]
            push!(sW, nSlice[dx, :, dz])
        end
    end
end

function (sW::SliceEnergy)(s)
    
    # Freeze all patterns
    sW.slices = Dict(k => sW.slicesCount[k] / sW.totalNum for k in keys(sW.slicesCount))
    init!(sW, s)
    
    sW.E = 0.0
    N = sW.N
    
    # Count all patterns
    for (x, y, z) in Iterators.product([1:size(s.voxels)[i] - N for i in 1:3]...)
        nSlice = s.voxels[x:x+N-1, y:y+N-1, z:z+N-1]
        initSlices!(sW, nSlice)
    end
    
    calcE!(sW, s)
    return sW.E
    
end

function preModification!(sW::SliceEnergy, s, ds)
    
    sW.dE = 0.0
    N = sW.N
    
    preMod!(sW, s)
    
    # Old energy
    for (p, b) in zip(ds.pos, ds.blk)
        
        starts = [max(p[i] - N + 1, 1) for i in 1:3]
        ends = [min(p[i], size(s.voxels)[i] - N + 1) for i in 1:3]
        
        for (x, y, z) in Iterators.product([s:e for (s,e) in zip(starts, ends)]...)
            nSlice = s.voxels[x:x+N-1, y:y+N-1, z:z+N-1]
            removeSlices!(sW, nSlice)
        end

    end
    
end

function postModification!(sW::SliceEnergy, s, ds)
    
    N = sW.N
    
    # New energy
    for (p, b) in zip(ds.pos, ds.blk)
        
        starts = [max(p[i] - N + 1, 1) for i in 1:3]
        ends = [min(p[i], size(s.voxels)[i] - N + 1) for i in 1:3]
        
        for (x, y, z) in Iterators.product([s:e for (s,e) in zip(starts, ends)]...)
            nSlice = s.voxels[x:x+N-1, y:y+N-1, z:z+N-1]
            addSlices!(sW, nSlice)
        end

    end
    
    calcdE!(sW, s)
    
    return sW.dE
    
end

function init!(sW::SliceEnergy, s) end
function calcE!(sW::SliceEnergy, s) end
function preMod!(sW::SliceEnergy, s) end
function calcdE!(sW::SliceEnergy, s) end

###############################################################

mutable struct Patterns <: SliceEnergy
    slices::Dict{Array{Block}, Float64}
    slicesCount::Dict{Array{Block}, Int64}
    totalNum::Int64
    
    eps::Float64
    N::Int64
    stride::Int64
    
    E::Float64
    dE::Float64
end

Patterns(N) = Patterns(Dict{Array{Block}, Int64}(), Dict{Array{Block}, Int64}(), 0, 1e-5, N, 1, 0.0, 0.0)

function initSlices!(sW::Patterns, nSlice::Array{Block, 3})
    sW.E -= log(sW[nSlice])
end

function addSlices!(sW::Patterns, nSlice::Array{Block, 3})
    sW.dE -= log(sW[nSlice])
end

function removeSlices!(sW::Patterns, nSlice::Array{Block, 3})
    sW.dE += log(sW[nSlice])
end

function reject!(e::Patterns) end
function backup!(e::Patterns) end

###############################################################

mutable struct PatternMarginalsState <: AbstractEnergyState
    MCMCslicesCount::Vector{Int64}
    E::Float64
end

mutable struct PatternMarginals <: SliceEnergy
    slices::Dict{Array{Block}, Float64}
    slicesCount::Dict{Array{Block}, Int64}
    totalNum::Int64
    
    eps::Float64
    N::Int64
    stride::Int64
    
    # Marginals
    MCMCnumSlices::Int64
    sliceIDs::Dict{Array{Block}, Int64}
    reference::Vector{Float64}
    
    distDiff::Function
    
    E::Float64
    dE::Float64
    
    # States
    state::PatternMarginalsState
    backupStates::Vector{PatternMarginalsState}
end

function reject!(e::PatternMarginals)
    copy!(e.state.MCMCslicesCount, e.backupStates[1].MCMCslicesCount)
    e.state.E = e.backupStates[1].E
end

function backup!(e::PatternMarginals)
    if length(e.backupStates) < 1
        push!(e.backupStates, deepcopy(e.state))
    else
        copy!(e.backupStates[1].MCMCslicesCount, e.state.MCMCslicesCount)
        e.backupStates[1].E = e.state.E
    end
end

PatternMarginals(N) = PatternMarginals(
    Dict{Array{Block}, Int64}(), 
    Dict{Array{Block}, Int64}(), 
    0, 
    
    1e-5, 
    N, 
    1,
    
    # [], 
    # [], 
    0, 
    Dict{Array{Block}, Int64}(), 
    [],
    
    L2,
    
    0,
    0,
    
    # 0.0, 
    # 0.0, 
    # 0.0
    PatternMarginalsState([], 0),
    []
)

function init!(sW::PatternMarginals, s)

    sW.state.MCMCslicesCount = zeros(length(sW.slices) + 1)
    # sW.prevMCMCslicesCount = zeros(length(sW.slices) + 1)
    sW.reference = zeros(length(sW.slices) + 1)
    
    cnt = 0
    for k in keys(sW.slices)
        cnt += 1
        sW.sliceIDs[k] = cnt
        sW.reference[cnt] = sW.slices[k]
    end

end

function initSlices!(sW::PatternMarginals, nSlice)
    
    if haskey(sW.sliceIDs, nSlice)
        sW.state.MCMCslicesCount[sW.sliceIDs[nSlice]] += 1
    else
        sW.state.MCMCslicesCount[end] += 1
    end
    
    sW.MCMCnumSlices += 1
    
end

function calcE!(sW::PatternMarginals, s)
    
    sW.reference .*= sW.MCMCnumSlices # Equalivent to normalizing every distributions later
    sW.state.E = sW.distDiff(sW.reference, sW.state.MCMCslicesCount) / sqrt(sW.MCMCnumSlices)
    
    @show sW.reference[1:10]
    @show sW.state.MCMCslicesCount[1:10]
    @show sW.state.E
    @show sW.MCMCnumSlices
    
    sW.E = sW.state.E
    
end

function addSlices!(sW::PatternMarginals, nSlice)
    if haskey(sW.sliceIDs, nSlice)
        sW.state.MCMCslicesCount[sW.sliceIDs[nSlice]] += 1
    else
        sW.state.MCMCslicesCount[end] += 1
    end
end

function removeSlices!(sW::PatternMarginals, nSlice)
    if haskey(sW.sliceIDs, nSlice)
        sW.state.MCMCslicesCount[sW.sliceIDs[nSlice]] -= 1
    else
        sW.state.MCMCslicesCount[end] -= 1
    end
end

function calcdE!(sW::PatternMarginals, s)
    sW.dE = sW.distDiff(sW.reference, sW.state.MCMCslicesCount) / sqrt(sW.MCMCnumSlices) - sW.state.E

    sW.state.E += sW.dE
    # @show sum(sW.state.MCMCslicesCount)
end
