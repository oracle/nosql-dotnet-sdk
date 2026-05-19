#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TFM="${TFM:-net5.0}"
CONFIG="${1:-${NOSQL_CONFIG_FILE:-$ROOT/Oracle.NoSQL.SDK.Samples/cloudsim.json}}"
TEST_PROJECT="$ROOT/Oracle.NoSQL.SDK/tests/Oracle.NoSQL.SDK.Tests/Oracle.NoSQL.SDK.Tests.csproj"
SMOKE_PROJECT="$ROOT/Oracle.NoSQL.SDK/tests/Oracle.NoSQL.SDK.SmokeTest/Oracle.NoSQL.SDK.SmokeTest.csproj"
DOTNET="${DOTNET:-$(command -v dotnet || true)}"

if [[ -z "$DOTNET" && -x /usr/local/share/dotnet/dotnet ]]; then
    DOTNET=/usr/local/share/dotnet/dotnet
fi

if [[ -z "$DOTNET" ]]; then
    echo "dotnet was not found. Set DOTNET=/path/to/dotnet."
    exit 2
fi

echo "Building $TFM"
"$DOTNET" build "$ROOT/Oracle.NoSQL.SDK.sln" -f "$TFM"

echo "Running retry safety unit tests"
"$DOTNET" test "$TEST_PROJECT" -f "$TFM" \
    --filter "ClassName~RetrySafetyTests"

echo "Running targeted local regression tests with $CONFIG"
"$DOTNET" test "$TEST_PROJECT" -f "$TFM" \
    --filter "ClassName~PutTests|ClassName~DeleteTests|ClassName~DeleteRangeTests|ClassName~WriteManyTests|ClassName~TableDDLTests" \
    -- \
    TestRunParameters.Parameter\(name=\"noSQLConfigFile\",value=\"$CONFIG\"\) \
    TestRunParameters.Parameter\(name=\"skipRateLimiterTests\",value=\"true\"\)

echo "Running local smoke test with $CONFIG"
"$DOTNET" run -f "$TFM" -p:UseAppHost=false \
    --project "$SMOKE_PROJECT" -- "$CONFIG"
