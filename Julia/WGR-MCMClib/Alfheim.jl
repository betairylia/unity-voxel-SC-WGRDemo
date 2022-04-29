#=
TODO
- Asymmetric proposals
- Delayed rejections
- Swapping
- NN proposals
- Connectivity constraints
=#

module Alfheim

using Distributions
using LinearAlgebra
import Base.+
import Base.deepcopy

const Block = UInt8

#=
State: defines a state, typically contains the voxels generated so far, current energy, #iter, #accepts and many other information.
=#
abstract type AbstractMCMCState end

#=
StateDifference: reflects an difference between two states, commonly state @ time t and time t+1.
    step!(state, stateDifference)
returns the new state after the difference has been applied.
=#
abstract type AbstractMCMCStateDifference end

#=
EnergyProvider: calculates some kind of energy based on current state.
    (e::Energy)(s::MCMCState)
will compute the energy for the state from stretch, as an initialization.
while
    (e::Energy)(s::MCMCState, ds::MCMCStateDifference)
calculates the change in energy (dE) after ds applied to s.
=#
abstract type AbstractEnergy end
abstract type AbstractEnergyState end

function reject!(e::AbstractEnergy)
    # copy!(e.state, e.backupStates[1])
    e.state = e.backupStates[1]
end

function backup!(e::AbstractEnergy)
    if length(e.backupStates) < 1
        push!(e.backupStates, deepcopy(e.state))
    else
        # copy!(e.backupStates[1], e.state)
        e.backupStates[1] = deepcopy(e.state)
    end
end

Base.deepcopy(m::T) where T <: AbstractEnergyState = T([ deepcopy(getfield(m, k)) for k ∈ fieldnames(T) ]...)
# Base.copy!(d::T, s::T) where T <: AbstractEnergyState = [ getfield(d, k) .= getfield(s, k) for k ∈ fieldnames(T) ]

#=
VoxelDistribution: defines the distribution of the entire structure generated.
A common example is simply a weighted collection of EnergyProviders.
    (vd::VoxelDistribution)(s::MCMCState; temperature = 1.0)
    (vd::VoxelDistribution)(s::MCMCState, ds::MCMCStateDifference; temperature = 1.0)
will return P(s) & P(s) / P(s+ds), respectively.
=#
abstract type AbstractVoxelDistribution end

#=
Walker: to give new proposals.
They might be initialized with some problem-specific data (e.g. block marginal distributions)
Later they can be used with 
    walk(walker, state) => MCMCStateDifference
to give a proposal step.

Currently they are assumed to be symmetric, but it might support asymmetric walkers later.
=#
abstract type AbstractWalker end

# TODO
abstract type AbstractSolver end
function step!(opt::AbstractSolver, state::AbstractMCMCState; temperature::Float64)::Tuple{Boolean, AbstractMCMCStateDifference} end

###########################################################################################
# STATE AND DIFFERENCES

mutable struct VoxelDifference <: AbstractMCMCStateDifference
    pos::Vector{Vector{Int}}
    blk::Vector{Block}
end

function Base.:+(x::VoxelDifference, y::VoxelDifference)
    return VoxelDifference([x.pos; y.pos], [x.blk; y.blk])
end

# TODO: This level of abstraction?
# function forward(ds::VoxelDifference, v::Array{Block})
#     invmod = VoxelDifference([], [])
#     for (p, b) in zip(ds.pos, ds.blk)
#         push!(invmod.pos, p)
#         push!(invmod.pos, s.voxels[p...])
#         s.voxels[p...] = b
#     end
#     return invmod
# end

mutable struct MCMCState1 <: AbstractMCMCState
    # Current voxels
    voxels::Array{Block}
    invmod::VoxelDifference # ds that will do the reject (revert) after proposal modifications.
    
    # Distribution
    p::AbstractVoxelDistribution
    
    # Proposals
    walker::AbstractWalker

    # Meta-data
    inited::Bool
end
MCMCState = MCMCState1 # For flexible definitions

createMCMCState(v, p, w) = MCMCState(v, VoxelDifference([], []), p, w, false)

function step!(s::MCMCState, ds::VoxelDifference)
    
    # Tell all energy functions to prepare for modification ds
    preModification!(s.p, s, ds)
    
    # Cache previous voxels for possible rejections
    for (p, b) in zip(ds.pos, ds.blk)
        
        # Store them
        push!(s.invmod.pos, p)
        push!(s.invmod.blk, s.voxels[p...])

        # Do the modification
        s.voxels[p...] = b

    end
    
    # Return the prob ratio from the distirbution definition
    return postModification!(s.p, s, ds)
end

function backup!(s::MCMCState)
    backup!(s.p)
    
    empty!(s.invmod.pos)
    empty!(s.invmod.blk)
end

function reject!(s::MCMCState)

    # @show s.invmod
    
    # Revert from previous cache
    for i in length(s.invmod.pos):-1:1
        p = s.invmod.pos[i]
        b = s.invmod.blk[i]
        s.voxels[p...] = b
    end

    # Revert all energy / distribution calculators
    reject!(s.p)

end

###########################################################################################
# DISTRIBUTION

# Temperature ?
mutable struct VoxelGibbsDistribution <: AbstractVoxelDistribution
    energy::Vector{AbstractEnergy}
    weight::Vector{Float64}
    
    # Cache (weighted) energy & dEnergy of each module.
    E::Vector{Float64}
    dE::Vector{Float64}
end

VoxelGibbsDistribution(energy, weight) = VoxelGibbsDistribution(energy, weight, zeros(length(energy)), zeros(length(energy)))

function (vs::VoxelGibbsDistribution)(s::MCMCState)
    for i in 1:length(vs.energy)
        vs.E[i] = vs.weight[i] * vs.energy[i](s)
    end
    return sum(vs.E)
end

function backup!(vs::VoxelGibbsDistribution)
    for i in 1:length(vs.energy)
        backup!(vs.energy[i])
        vs.dE[i] = 0
    end
end

function preModification!(vs::VoxelGibbsDistribution, s::MCMCState, ds::AbstractMCMCStateDifference)
    for i in 1:length(vs.energy)
        if vs.weight[i] > 0
            preModification!(vs.energy[i], s, ds)
        end
    end
end

function postModification!(vs::VoxelGibbsDistribution, s::MCMCState, ds::AbstractMCMCStateDifference)
    for i in 1:length(vs.energy)
        if vs.weight[i] > 0
            d = vs.weight[i] * postModification!(vs.energy[i], s, ds)
            vs.dE[i] += d
            vs.E[i] += d
        end
    end
    dE = sum(vs.dE)
    return dE
end

function reject!(vs::VoxelGibbsDistribution)
    for i in 1:length(vs.energy)
        reject!(vs.energy[i])
        vs.E[i] -= vs.dE[i]
    end
end

###########################################################################################
# ENERGY FUNC

############################
### Block Marginals

mutable struct BlockMarginsState <: AbstractEnergyState
    blockCount::Vector{Int64}
    E::Float64
end

mutable struct BlockMargins <: AbstractEnergy
    # prevBlockCount::Vector{Int64}
    
    reference::Vector{Float64}
    numBlocksTotal::Int64
    
    # distribution difference function, p, q -> s \in \mathbb{R}
    distDiff::Function
    
    # E::Float64
    # prev_E::Float64
    
    state::BlockMarginsState
    backupStates::Vector{BlockMarginsState}
end

const L2(p, q) = norm(p - q)
function BlockMargins(ref)
    return BlockMargins(
        # zeros(length(ref)),
        # zeros(length(ref)),
        ref,
        0,
        L2,
        # 0,
        # 0
        BlockMarginsState(zeros(length(ref)), 0),
        []
    )
end

function (e::BlockMargins)(s)
    
    # Count number of each blocks
    for i in 1:length(e.reference)
        e.state.blockCount[i] = sum(s.voxels .== i)
    end
    
    e.numBlocksTotal = sum(e.state.blockCount)
    
    # Calculate energy
    e.state.E = e.distDiff(e.reference, e.state.blockCount ./ e.numBlocksTotal)
    return e.state.E
    
end

function preModification!(e::BlockMargins, s, ds)
    for (p, b) in zip(ds.pos, ds.blk)
        e.state.blockCount[s.voxels[p...]] -= 1
        e.state.blockCount[b] += 1
    end
end

function postModification!(e::BlockMargins, s, ds)
    # Stright forward calculation
    dE = e.distDiff(e.reference, e.state.blockCount ./ e.numBlocksTotal) - e.state.E

    # Cache previous E for rejects
    e.state.E += dE
    
    return dE
end

###########################################################################################
# WALKERS

walk(walker::AbstractWalker, s, ds) = walk(walker, s)

mutable struct RandomBlockProposal <: AbstractWalker
    dist::Categorical
    count::Int64
    boundsMin::Vector{Int64}
    boundsMax::Vector{Int64}
end

function walk(walker::RandomBlockProposal, s::AbstractMCMCState)
    pos = []
    blk = []
    for i in 1:walker.count
        push!(pos, [rand(walker.boundsMin[i]:walker.boundsMax[i]) for i in 1:3])
        push!(blk, Block(rand(walker.dist)))
    end
    return VoxelDifference(pos, blk)
end

mutable struct RandomBlockProposalAlwaysDifferent <: AbstractWalker
    dist::Categorical
    count::Int64
    boundsMin::Vector{Int64}
    boundsMax::Vector{Int64}
    
    newDist::Vector{Categorical}
end

function RandomBlockProposalAlwaysDifferent(d, c, bi, bx)
    r = RandomBlockProposalAlwaysDifferent(d, c, bi, bx, [])
    
    # Build alternative distributions
    # Special case: just 2 blocks
    if length(r.dist.p) <= 2
        # nothing
    else
        for b in 1:length(r.dist.p)
            original = b
            newDistP = copy(r.dist.p)
            newDistP[original] = 0
            newDistP = newDistP / sum(newDistP)

            push!(r.newDist, Categorical(newDistP))
        end
    end
    
    return r
end

function walk(walker::RandomBlockProposalAlwaysDifferent, s::AbstractMCMCState)
    pos = []
    blk = []
    for i in 1:walker.count
        
        p = [rand(walker.boundsMin[i]:walker.boundsMax[i]) for i in 1:3]
        original = s.voxels[p...]
        
        push!(pos, p)
        if length(walker.newDist) == 0
            push!(blk, Block(length(walker.dist.p) - original + 1))
        else
            push!(blk, Block(rand(walker.newDist[original])))
        end

    end
    return VoxelDifference(pos, blk)
end

# For delayed rejections
function walk(walker::RandomBlockProposalAlwaysDifferent, s::AbstractMCMCState, ds::AbstractMCMCStateDifference)
    pos = []
    blk = []
    for i in 1:walker.count
        
        # Randomly move 1 step from the last modified point
        p = copy(ds.pos[end])
        p[rand(1:3)] += rand([-1, 1])
        
        # Map p into the valid range
        p = [mod((p[i] - walker.boundsMin[i]), (walker.boundsMax[i] - walker.boundsMin[i])) + walker.boundsMin[i] for i in 1:3]
        
        original = s.voxels[p...]
        
        push!(pos, p)
        if length(walker.newDist) == 0
            push!(blk, Block(length(walker.dist.p) - original + 1))
        else
            push!(blk, Block(rand(walker.newDist[original])))
        end

    end
    return VoxelDifference(pos, blk)
end

###########################################################################################
# "SOLVERS"

function init!(state::MCMCState)

    state.p(state)
    state.inited = true

end

struct Metropolis <: AbstractSolver
end

function step!(opt::Metropolis, state::MCMCState; temperature::Float64 = 1.0)
    
    backup!(state)

    ds = walk(state.walker, state)
    
    dE = step!(state, ds)
    alpha = exp(-dE / temperature)
    
    if alpha < 1
        if rand(Uniform(0, 1)) > alpha # Rejected
            reject!(state)
            return 0, nothing
        end
        return 2, ds
    end
    
    return 1, ds
    
end

include("./Slices.jl")
include("./SimpleDelayedRejection.jl")
include("./Paths.jl")

end # module Alfheim