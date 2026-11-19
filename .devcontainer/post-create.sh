#!/bin/sh
set -eu

# The source solution includes both host variants; generated projects have one solution.
solution=AditiKraft.Krafter.slnx
if [ -f AditiKraft.Krafter.Dev.slnx ]; then
    solution=AditiKraft.Krafter.Dev.slnx
fi
dotnet restore "$solution"

installed_tools=$(dotnet tool list --global)
if ! printf '%s\n' "$installed_tools" | grep -q '^dotnet-ef[[:space:]]'; then
    dotnet tool install --global dotnet-ef --version '10.*'
fi
