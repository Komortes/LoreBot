#!/usr/bin/env bash
set -euo pipefail
# TODO: implement per plan
dotnet test src/LoreBot.Evaluations --logger "console;verbosity=normal"
