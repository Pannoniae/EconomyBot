#!/bin/bash

DOTNET_DefaultStackSize="80000" DOTNET_EnableWriteXorExecute="0" DOTNET_GCHeapHardLimit="c800000" DOTNET_GCName="libclrgc.so" dotnet ./EconomyBot.dll &
java -jar -Xms10M -Xmx128M Lavalink.jar &