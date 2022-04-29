# module Alfheim

# Reference: https://arxiv.org/pdf/0904.2207.pdf

struct ReversibleDRMetropolis <: AbstractSolver
    maxSteps::Int64
    
    dEs::Vector{Float64} # dEs[i] = dE(i, i+1)

    Ns::Matrix{Float64} # without exp(-dE)
    Ds::Matrix{Float64} # without exp(-dE)
    alphas::Matrix{Float64} # alphas[i, j] = alpha(i, ..., j), with exp(-dE)
    reverseAlphas::Matrix{Float64} # rev[i, j] = alpha(j, ..., i), with exp(dE)
end

ReversibleDRMetropolis(N) = ReversibleDRMetropolis(
    N,
    
    zeros(N),
    zeros(N+1, N+1),
    zeros(N+1, N+1),
    zeros(N+1, N+1),
    zeros(N+1, N+1)
)

function step!(opt::ReversibleDRMetropolis, state::MCMCState; temperature::Float64 = 1.0)
    
    backup!(state)
    
    ds_all = VoxelDifference([], [])

    for DRiter in 1:opt.maxSteps
        
        # @show DRiter
        
        # i -> i+1
        if DRiter > 1
            ds = walk(state.walker, state, ds_all)
        else
            ds = walk(state.walker, state)
        end
        
        # @show ds
        
        ds_all = ds_all + ds
        
        # @show ds

        # dE(i, i+1) & alpha(i, i+1)
        dE = step!(state, ds)
        alpha = exp(-dE / temperature)
        
        # @show dE
        # @show state.p.energy[1].state.blockCount
        
        # Initial value
        opt.dEs[DRiter] = dE
        opt.Ns[DRiter, DRiter + 1] = 1
        opt.Ds[DRiter, DRiter + 1] = 1
        opt.alphas[DRiter, DRiter + 1] = min(1, alpha)
        
        # Calculate alpha's recursively
        for i in (DRiter-1):-1:1
            
            D = opt.Ds[i, DRiter] * ( 1 - opt.alphas[i, DRiter] )
            N = opt.Ns[i + 1, DRiter + 1] * ( 1 - opt.reverseAlphas[i + 1, DRiter + 1] )
            
            dESum = sum(opt.dEs[i:DRiter])
            
            opt.Ds[i, DRiter + 1]            = D
            opt.Ns[i, DRiter + 1]            = N
            opt.alphas[i, DRiter + 1]        = min(N / D * exp(-dESum / temperature), 1)
            opt.reverseAlphas[i, DRiter + 1] = min(D / N * exp(+dESum / temperature), 1)

        end
        
        # Obtain alpha for this step
        alpha = opt.alphas[1, DRiter + 1]

        if alpha >= 1 || rand(Uniform(0, 1)) <= alpha # Accepted
        # if DRiter == opt.maxSteps
            return DRiter, ds_all
        end

    end
    
    # Rejected
    # @show ds_all
    reject!(state)
    return 0, nothing
    
end

# Extend the RandomBlock walker For DR steps
function walk(walker::RandomBlockProposal, s::AbstractMCMCState, laststep::RandomBlockProposal)
    pos = []
    blk = []
    for i in 1:walker.count
        push!(pos, [rand(walker.boundsMin[i]:walker.boundsMax[i]) for i in 1:3])
        push!(blk, Block(rand(walker.dist)))
    end
    return VoxelDifference(pos, blk)
end
