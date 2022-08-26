import Base.Threads.@spawn

using NPZ

include("WGR-MCMClib/WGR.jl")
using .WGR

include("WGR-MCMClib/Alfheim.jl")
using .Alfheim

include("WGR-GAlib/Species.jl")
using .AlfheimG

import Base.flush
using Distributions

using Formatting
using Test
using Random

using Sockets
using ArgParse

using Base.Threads

# buf = WGR.BlockBuffer(32 * Threads.nthreads())
buf = WGR.BlockBuffer(128)
setBlocks(x,y,z,b) = WGR.setBlocks!(buf, x, y, z, b)
flush() = WGR.flush!(buf)
refreshNd(a,p,f = false) = WGR.refreshNd!(buf,a,p,f)

function parse_commandline()
    s = ArgParseSettings()

    @add_arg_table s begin
        # "--opt1"
        #     help = "an option with an argument"
        # "--opt2", "-o"
        #     help = "another option with an argument"
        #     arg_type = Int
        #     default = 0
        # "--flag1"
        #     help = "an option without argument, i.e. a flag"
        #     action = :store_true
        # "arg1"
        #     help = "a positional argument"
        #     required = true
        "--ipaddr", "-i"
            help = "ip to listen"
            default = "127.0.0.1"
        "--port", "-p"
            help = "port for server"
            arg_type = Int
            default = 4445
        "--wgraddr"
            help = "ip address of WGRDemo"
            default = "127.0.0.1"
        "--wgrport"
            help = "port of WGRDemo"
            arg_type = Int
            default = 4444
    end

    return parse_args(s)
end

# Energy calculation
function fit(instance::AlfheimG.Specie, inmap::AlfheimG.Map, E::Alfheim.VoxelGibbsDistribution)
    copiedmap = deepcopy(inmap)
    AlfheimG.populate!(instance, copiedmap)
    dummyState = Alfheim.createMCMCState(copiedmap.map, deepcopy(E), Alfheim.DummyWalker(copiedmap.min, copiedmap.max))
    # refreshNd(bRep[copiedmap.map], [0, 64, 0], true)
    return - dummyState.p(dummyState)
end

function main()
    
    args = parse_commandline()
    println("Parsed args:")
    for (arg,val) in args
        println("  $arg  =>  $val")
    end
    
    print("Collecting ingredients ...\n")

    mapsize = 32
    mapheight = 16
    bRep = [0x00000000, 0xBA8D65FF, 0x3155A6FF, 0x96C78CFF]

    ###### Initialization

    Nb = length(bRep)
    padding = 2

    _map = ones(AlfheimG.Block, mapsize + 2*padding, mapheight + 2*padding, mapsize + 2*padding)
    _map[1+padding:mapsize+padding, 1:padding, 1+padding:mapsize+padding] .= 0x02

    # ############# Create Paths energy

    # # Costs
    # costs = zeros(Int, Nb, Nb, Nb, Nb, 6)

    # costs[1:Nb, 1:Nb, 1, 1, 1:6] .= 10 # -> Void
    # costs[1:Nb, 1:Nb, 2:Nb, 1:Nb, 1:6] .= 10 # -> Solid

    # costs[1:Nb, 1:Nb, 1, 2:Nb, 1:2] .= 10 # -> Road, vertical (should not appear)
    # costs[1:Nb, 1:Nb, 1, 2:Nb, 3:6] .= 2 # -> Road, horizontal

    # costs[1, 2:Nb, 1, 1, 1:6] .= 3 # Road -> Void (stairs)
    # costs[1, 1, 1, 2:Nb, 1:6] .= 3 # Void -> Road (stairs)

    # # Test cost
    # # costs = zeros(Int, Nb, Nb, 6)
    # # costs[1:Nb, 1:Nb, 1:6] .= 1

    # @show findall(x -> x>0, costs .== 0)

    # # Arrays
    # dist = fill(Int(2^30), size(_map))
    # dirc = ones(UInt8, size(_map)...)

    # # Entrance
    # entranceSize = mapsize ÷ 4
    # entranceStart = (mapsize + 2 * padding - entranceSize) ÷ 2 + 1
    # entranceEnd = entranceStart + entranceSize

    # # 4 Entries
    # starts = []

    # dist[entranceStart:entranceEnd, 1:1 + padding, 1:padding] .= 0
    # starts = [starts; [[x, 1 + padding, padding] for x in entranceStart:entranceEnd]]
    # _map[entranceStart:entranceEnd, 1:padding, 1:padding] .= 3

    # dist[1:padding, 1:1 + padding, entranceStart:entranceEnd] .= 0
    # starts = [starts; [[padding, 1 + padding, x] for x in entranceStart:entranceEnd]]
    # _map[1:padding, 1:padding, entranceStart:entranceEnd] .= 3

    # dist[entranceStart:entranceEnd, 1:1 + padding, end:-1:end-padding+1] .= 0
    # starts = [starts; [[x, 1 + padding, size(_map)[3]-padding+1] for x in entranceStart:entranceEnd]]
    # _map[entranceStart:entranceEnd, 1:padding, end:-1:end-padding+1] .= 3

    # dist[end:-1:end-padding+1, 1:1 + padding, entranceStart:entranceEnd] .= 0
    # starts = [starts; [[size(_map)[1]-padding+1, 1 + padding, x] for x in entranceStart:entranceEnd]]
    # _map[end:-1:end-padding+1, 1:padding, entranceStart:entranceEnd] .= 3

    # @show [1, 1, 1] .+ padding

    ########### Create Map

    testmap = AlfheimG.Map(
        _map,
        [1 + padding, 1 + padding, 1 + padding],
        [mapsize + padding, mapheight + padding, mapsize + padding]
    )

    ########### Create objectives

    # pathsEnergy = Alfheim.Paths(costs, starts, [[0,0,0], [0,-1,0]], testmap.min, testmap.max, dist, dirc)
    # pathsWeight = 0.07

    blockMarginalsE = Alfheim.BlockPropotion(0.7, AlfheimG.AirBlock) # Target: 65% Air remains
    blockMarginalsW = mapsize^2 * mapheight * 6

    # Energies
    Etotal = Alfheim.VoxelGibbsDistribution(
        [
            blockMarginalsE,
            # pathsEnergy
        ],
        [
            blockMarginalsW,
            # pathsWeight
        ]
    )
    
    nThreads = Threads.nthreads()
    print("Starting server with $nThreads threads ...\n")
    
    server = listen(IPv4(args["ipaddr"]), args["port"])
    printfmt("Server waiting connection at {}:{}!\n", args["ipaddr"], args["port"])
    clientID = 1
    while true
        socket = accept(server)
        @async begin
            try
                printfmt("Client $clientID connected.\n")
                clientID += 1
                while true
                    # Read total size
                    total_num = read(socket, Int32)
                    # batch size
                    batch_size = read(socket, Int32)
                    geneLenth = total_num ÷ batch_size
                    
                    print("Total size: $total_num\n")
                    print(" - batch size: $batch_size\n")
                    
                    # Create array
                    pool = []
                    fitness = zeros(Float32, batch_size)
                    
                    for pi in 1:batch_size
                        gene = zeros(UInt8, geneLenth)
                        for gi in 1:geneLenth
                            gene[gi] = read(socket, UInt8)
                        end
                        push!(pool, AlfheimG.TestCity(gene))
                    end
                    
                    print("Read complete!\n")
                    
                    # Compute fitness
                    @threads for ix in 1:batch_size
                    # for ix in 1:batch_size
                        fitness[ix] = fit(pool[ix], testmap, Etotal)
                    end
                    
                    write(socket, fitness)
                    print("* Fitness:\n")
                    print(fitness)
                    print("\n")
                    
                    # Visualization in WGR
                    if true
                        print("Visualizing ...")
                        row = convert(Int64, floor(sqrt(batch_size)))
                        for ix in 1:length(pool)
                            copiedmap = deepcopy(testmap)
                            AlfheimG.populate!(pool[ix], copiedmap)
                            refreshNd(
                                bRep[copiedmap.map], 
                                [
                                       ((ix-1) % row) * (mapsize + 2*padding + 2), 
                                    65, 
                                    div((ix-1),  row) * (mapsize + 2*padding + 2)
                                ], 
                                true
                            )
                        end
                        print(" Done.\n")
                    end
                end
            catch err
                if isa(err, EOFError)
                    print("(EOF - Client disconnected.)\n")
                else
                    @error "Error:" exception=(err, catch_backtrace())
                end
            end
        end
    end
    
end

main()
