#!/bin/sh
# Compiles and runs the guild logic tests (no game needed).
# Usage: sh tests/run.sh
cd "$(dirname "$0")/.."
CSC="/c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe"
mkdir -p tests/bin
"$CSC" -nologo -out:'tests\bin\GuildTests.exe' 'tests\GuildTests.cs' 'src\GuildData.cs' || exit 2
tests/bin/GuildTests.exe
