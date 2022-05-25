# module Alfheim

using DataStructures
using InteractiveUtils

const Up = 1
const Down = 2
const Left = 3
const Right = 4
const Forward = 5
const Back = 6
const allDirections = 1:6

const directionVector = 
[
     0  1  0;
     0 -1  0;
    -1  0  0;
     1  0  0;
     0  0  1;
     0  0 -1
]

const invertDirc = 
[
    Down,
    Up,
    Right,
    Left,
    Back,
    Forward
]

# Calculate shortest paths.
# starts: Nx3 matrix; Please set new content (dist etc.) before calling BFS.
# dist: Start from current point, distance to the nearest end point. *- Independent with current point's content -*
# minB & maxB: only points within min ~ max can be pushed into BFS queue. This does not affect points in "starts".

# TODO: Distance will not be updated if an obstacle block is placed (which, should increase distance along its path first)
function BFSbad!(
    dist::Array{Int64}, 
    dirc::Array{UInt8}, 
    vox, 
    cost, 
    starts,
    visited;
    minB = [1,1,1], maxBFromBoundary = [0,0,0],

    #= 
        points will be checked related to checkorder.
        e.g., when checkorder = [0,0,0], cost[curr, dest, dirc] will be invoked;
        checkorder = [[0,0,0],[0,1,0],[0,-1,0]], then cost[curr, currHead, currFoot, dest, destHead, destFoot, dirc] will be invoked.
    =#
    checkOrder = [[0,0,0]]
)

    # Create structures
    # pq = PriorityQueue{Vector{Int64}, Int64}()
    # q = Queue{Pair{Vector{Int64}, Int64}}()
    # q = Queue{Pair{Vector{Int64}, Bool}}()
    q = Queue{Vector{Int64}}()
    # q = PriorityQueue{Vector{Int64}, Int64}()
    # qStart = Queue{Vector{Int64}}()
    # visited = zeros(Bool, size(vox)...)
    
    minBounds = minB
    maxBounds = size(vox) .+ maxBFromBoundary
    
    cnt = 0
    
    # Insert initial points
    # Iterate through array to get all points along the initial path
    for pp in starts
        for oo in checkOrder
            p = pp - oo
            # enqueue!(q, p)
            # visitedStart[p...] = true
            # @inbounds enqueue!(pq, p => dist[p...])
            # @inbounds enqueue!(q, p => dist[p...])

            if !(any(p .> maxBounds) || any(p .< minBounds) || visited[p...])
                enqueue!(q, p)# => dist[p...])
                @inbounds visited[p...] = true
            end

            for d in allDirections

                @inbounds newpos = p + directionVector[d, :]

                # Ignore out of range / visited positions
                if any(newpos .> maxBounds) || any(newpos .< minBounds) || visited[newpos...]
                    continue
                end

                # enqueue!(q, newpos => false)
                enqueue!(q, newpos)# => dist[newpos...])
                visited[newpos...] = true

            end
    #         @inbounds enqueue!(qStart, p)

    #         while !isempty(qStart)
    #             p = dequeue!(qStart)
    #             enqueue!(q, p => true)
    #             for d in allDirections
    #                 newpos = p .+ directionVector[d]

    #                 # Ignore out of range / visited positions
    #                 if any(newpos .> maxBounds) || any(newpos .< minBounds)
    #                     continue
    #                 end

    #                 # Omit blocks that are not pointing to self
    #                 id = invertDirc[d]
    #                 if dirc[newpos...] != id
    #                     continue
    #                 end

    #                 @inbounds src = [vox[(newpos .+ offset)...] for offset in checkOrder]
    #                 @inbounds dst = [vox[(p .+ offset)...] for offset in checkOrder]

    #                 @inbounds newdist = dist[p...] + cost[src..., dst..., id]
    #                 @inbounds dist[newpos...] = newdist
    #                 enqueue!(qStart, newpos)
    #             end
    #         end
        end
    end
    
    # Result buffers
    dDist = 0
    
    # Update all points
    # while !isempty(pq)
    while !isempty(q)
        
        cnt += 1
        
        # Pick current position; p is a vector with shape (3,)
        # p = dequeue!(pq)
        # (p, pDist) = dequeue!(q)
        p = dequeue!(q)
        @inbounds visited[p...] = false
        # (p, updated) = dequeue!(q)
        # @show p
        # flush(stdout)

        # We come here too late, lol
        # if pDist > dist[p...]
        #     continue
        # end
        # @show p
        
        minDist = 1e10
        minD = 0
        
        for d in allDirections
            
            id = invertDirc[d]
            
            newpos = p + directionVector[d, :]
            
            # Ignore out of range positions
            if any(newpos .> size(vox)) || any(newpos .< 1)
                continue
            end
            # if any(newpos .> maxBounds) || any(newpos .< minBounds)
            #     continue
            # end
            
            @inbounds src = [vox[(p .+ offset)...] for offset in checkOrder]
            @inbounds dst = [vox[(newpos .+ offset)...] for offset in checkOrder]
            
            newdist = dist[newpos...] + cost[src..., dst..., d]
            # @show dist[newpos...]
            # newdist = dist[p...] + cost[src..., dst..., id]
            
            if newdist < minDist
                minDist = newdist
                minD = d
            end
            
#             if newdist < dist[newpos...] || dirc[newpos...] == id
#             # if newdist < dist[p...]
#                 dDist += newdist - dist[newpos...]
#                 dist[newpos...] = newdist
#                 dirc[newpos...] = id
                
#                 # dDist += newdist - dist[p...]
#                 # dist[p...] = newdist
#                 # dirc[p...] = d
                
#                 # if haskey(pq, newpos)
#                 #     pq[newpos] = newdist
#                 # else
#                 #     enqueue!(pq, newpos => newdist)
#                 # end
#                 enqueue!(q, newpos => newdist)
                
#                 # updated = true
#             end
        end
        
        # Enqueue all surrounding positions
        if dist[p...] != minDist
            
            dDist += minDist - dist[p...]
            dist[p...] = minDist
            dirc[p...] = minD
            
            for d in allDirections

                newpos = p + directionVector[d, :]

                # Ignore out of range / visited positions
                if any(newpos .> maxBounds) || any(newpos .< minBounds) || visited[newpos...]
                    continue
                end

                # enqueue!(q, newpos => false)
                enqueue!(q, newpos)# => dist[newpos...])
                visited[newpos...] = true

            end
            
        end
        
    end
    
    # @show cnt
    
    return dDist
end

############################################# Efficient version

# Calculate shortest paths.
# starts: Nx3 matrix; Please set new content (dist etc.) before calling BFS.
# dist: Start from current point, distance to the nearest end point. *- Independent with current point's content -*
# minB & maxB: only points within min ~ max can be pushed into BFS queue. This does not affect points in "starts".

# TODO: Distance will not be updated if an obstacle block is placed (which, should increase distance along its path first)
function BFS!(
    dist::Array{Int64}, 
    dirc::Array{UInt8}, 
    vox, 
    cost, 
    starts,
    visited;
    minB = [1,1,1], maxBFromBoundary = [0,0,0],

    #= 
        points will be checked related to checkorder.
        e.g., when checkorder = [0,0,0], cost[curr, dest, dirc] will be invoked;
        checkorder = [[0,0,0],[0,1,0],[0,-1,0]], then cost[curr, currHead, currFoot, dest, destHead, destFoot, dirc] will be invoked.
    =#
    checkOrder = [[0,0,0]]
)

    # Create structures
    #               Position       Start  Shrink  Expand  Original dist
    # q = Queue{Tuple{Vector{Int64}, Bool,  Bool,   Bool,   Int32}}()
    
    #               Position       Shrink Expand
    q = Queue{Tuple{Vector{Int64}, Bool,  Bool,}}()
    startQ = Queue{Vector{Int64}}()
    
    minBounds = minB
    maxBounds = size(vox) .+ maxBFromBoundary
    
    cnt = 0
    
    # Result buffers
    dDist = 0
    
    # Insert initial points
    # Iterate through array to get all points along the initial path
    for pp in starts
        for oo in checkOrder
            p = pp - oo

            if any(p .> size(vox)) || any(p .< 1)
                continue
            end
            
            # Handle the start point in a special way
            for d in allDirections
            
                id = invertDirc[d]

                newpos = p + directionVector[d, :]

                # Ignore out of range positions
                if any(newpos .> maxBounds) || any(newpos .< minBounds)
                    continue
                end

                @inbounds src = [@inbounds vox[(newpos .+ offset)...] for offset in checkOrder]
                @inbounds dst = [@inbounds vox[(p .+ offset)...] for offset in checkOrder]

                @inbounds newdist = dist[p...] + cost[src..., dst..., id]

                od = dist[newpos...]
                if newdist > od && dirc[newpos...] == id # Continue expand incoming edges to satisfy short-path upper bound condition
                    dDist += newdist - od
                    dist[newpos...] = newdist
                    
                    # if !visited[newpos...]
                        enqueue!(startQ, newpos) # This is for initial expanding
                        enqueue!(q, (newpos, false, true)) # This is for later relaxing
                        # @inbounds visited[newpos...] = true
                    # end
                end
                
                if newdist < od # Can relax
                    dDist += newdist - od
                    dist[newpos...] = newdist
                    dirc[newpos...] = id
                    
                    # if !visited[newpos...]
                        enqueue!(q, (newpos, true, false)) # Don't follow the path if relaxed, since upper-bound condition already satisfied
                        # @inbounds visited[newpos...] = true
                    # end
                end
                
            end
            
            # Use another loop to avoid calculate distances for non-incoming edges (since previously we need also consider relaxing)
            # This is for all current paths ends at start point p
            while !isempty(startQ)
                
                sp = dequeue!(startQ)
                
                for d in allDirections
                    
                    id = invertDirc[d]
                    newpos = sp + directionVector[d, :]
                    
                    # Ignore out of range positions
                    if any(newpos .> maxBounds) || any(newpos .< minBounds)
                        continue
                    end
                    
                    if dirc[newpos...] == id # Expand path

                        @inbounds src = [@inbounds vox[(newpos .+ offset)...] for offset in checkOrder]
                        @inbounds dst = [@inbounds vox[(sp .+ offset)...] for offset in checkOrder]

                        @inbounds newdist = dist[sp...] + cost[src..., dst..., id]

                        dDist += newdist - dist[newpos...]
                        dist[newpos...] = newdist

                        # if !visited[newpos...]
                            enqueue!(startQ, newpos) # This is for initial expanding
                            enqueue!(q, (newpos, false, true)) # This is for later relaxing
                            # @inbounds visited[newpos...] = true
                        # end

                    end
                    
                end
                
            end
            
            # enqueue!(q, (p, true, false, false, dist[p...]))
            # @inbounds visited[p...] = true
        end
    end
    
    # Update all points
    while !isempty(q)
        
        cnt += 1
        
        # Pick current position; p is a vector with shape (3,)
        p, isShrink, isExpand = dequeue!(q)
        # @inbounds visited[p...] = false
        
        ################
        # 1. if node expanded (dist increased), try find out if self dist can be updated by considering all directions
        # i.e. try to find smaller dist by connect self to other neighbors
        # Then perform correspondingly as if self dist decreased (shrinked)
        
        # Try to find new place
        if isExpand
            
            minDist = 1e10
            minD = 0

            for d in allDirections

                id = invertDirc[d]
                newpos = p + directionVector[d, :]

                # Ignore out of range positions
                if any(newpos .> size(vox)) || any(newpos .< 1) || dirc[newpos...] == id
                    continue
                end
                # if any(newpos .> maxBounds) || any(newpos .< minBounds)
                #     continue
                # end

                @inbounds src = [vox[(p .+ offset)...] for offset in checkOrder]
                @inbounds dst = [vox[(newpos .+ offset)...] for offset in checkOrder]

                newdist = dist[newpos...] + cost[src..., dst..., d]

                if newdist < minDist
                    minDist = newdist
                    minD = d
                end
                
            end
            
            orgDist = dist[p...]
            dDist += minDist - orgDist
            dist[p...] = minDist
            dirc[p...] = minD
            isExpand = false
            
            if minDist < orgDist
                isShrink = true
            end
            
        end # isExpand
        
        ################
        # 2. If node shrinked (dist decreased), try to relax neighbors
        
        if isShrink
            
            for d in allDirections
            
                id = invertDirc[d]

                newpos = p + directionVector[d, :]

                # Ignore out of range positions
                if any(newpos .> maxBounds) || any(newpos .< minBounds)
                    continue
                end

                @inbounds src = [@inbounds vox[(newpos .+ offset)...] for offset in checkOrder]
                @inbounds dst = [@inbounds vox[(p .+ offset)...] for offset in checkOrder]

                @inbounds newdist = dist[p...] + cost[src..., dst..., id]

                od = dist[newpos...]
                
                if newdist < od # Relax
                    dDist += newdist - od
                    dist[newpos...] = newdist
                    dirc[newpos...] = id
                    
                    # if !visited[newpos...]
                        enqueue!(q, (newpos, true, false))
                        # @inbounds visited[newpos...] = true
                    # end
                end
                
            end
            
        end # isShrink
        
    end
    
    # @show cnt
    
    return dDist
end

######################################

mutable struct PathsState <: AbstractEnergyState
    dist::Array{Int64}
    dirc::Array{UInt8}
end

mutable struct Paths <: AbstractEnergy
    cost::Array{Int}
    minBounds::Vector{Int}
    maxBounds::Vector{Int}
    
    starts::Array{Vector{Int}}
    checks::Array{Vector{Int}}
    visited::Array{Bool}
    
    state::PathsState
    backupStates::Vector{PathsState}
end

Paths(c, s, checks, minB::Vector{Int}, maxB::Vector{Int}, dist::Array{Int}, dirc::Array{UInt8}) = Paths(
    c, 
    minB, 
    maxB, 
    s,
    checks,
    [],
    PathsState(dist, dirc), 
    []
)

function (e::Paths)(s)
    e.maxBounds = e.maxBounds .- size(s.voxels)
    e.visited = zeros(Bool, size(s.voxels)...)
    BFS!(e.state.dist, e.state.dirc, s.voxels, e.cost, e.starts, e.visited; minB = e.minBounds, maxBFromBoundary = e.maxBounds, checkOrder = e.checks)
    distValid = e.state.dist[[i:j for (i,j) in zip(e.minBounds, size(s.voxels) .+ e.maxBounds)]...]
    # @show distValid
    sumDist = sum(distValid)
    return sumDist # as Energy
end

function preModification!(e::Paths, s, ds)
end

function postModification!(e::Paths, s, ds)
    dE = BFSbad!(e.state.dist, e.state.dirc, s.voxels, e.cost, ds.pos, e.visited; minB = e.minBounds, maxBFromBoundary = e.maxBounds, checkOrder = e.checks)
    return dE
end

# function addCost!(costArr, src, dst, dirc, cost)
#     for (s, d, i) in Iterators.product(src, dst, dirc)
#         costArr[s..., d..., i] = cost
#     end
# end
