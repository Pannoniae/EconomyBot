#!/bin/bash

# tune glibc memory allocation, optimize for low fragmentation
# limit the number of arenas
export MALLOC_ARENA_MAX=2

# disable brk/sbrk for less than a page allocations because it wastes memory (this doesn't fragment as much)
export MALLOC_MMAP_THRESHOLD_=8192
export MALLOC_TRIM_THRESHOLD_=8192
export MALLOC_TOP_PAD_=8192
#export MALLOC_MMAP_MAX_=65536

export LD_PRELOAD="/usr/lib/libjemalloc.so"
export MALLOC_CONF="narenas:1,tcache:false,dirty_decay_ms:1000,muzzy_decay_ms:1000,metadata_thp:disabled"

export DOTNET_EnableWriteXorExecute=0
# just pretend we don't have memory
export DOTNET_GCHighMemPercent=1
export DOTNET_GCConserveMemory=7

dotnet ./EconomyBot.dll &
java -jar -Xms10M -Xmx75m Lavalink.jar &