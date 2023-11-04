#!/bin/bash

dotnet ./EconomyBot.dll &
java -jar -Xms10M -Xmx256M Lavalink.jar &