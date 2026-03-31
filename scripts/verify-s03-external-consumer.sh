#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "${script_dir}/.." && pwd)"

readonly pack_project="${repo_root}/src/Azure.InMemory/Azure.InMemory.csproj"
readonly pack_output_dir="${repo_root}/artifacts/pack"
readonly expected_package="${pack_output_dir}/Azure.InMemory.1.0.0.nupkg"
readonly consumer_project="${repo_root}/samples/Azure.InMemory.ExternalConsumer/Azure.InMemory.ExternalConsumer.csproj"
readonly consumer_config="${repo_root}/samples/Azure.InMemory.ExternalConsumer/NuGet.Config"
readonly consumer_packages_dir="${repo_root}/samples/Azure.InMemory.ExternalConsumer/.nuget/packages"
readonly solution_path="${repo_root}/Azure.InMemory.sln"
readonly consumer_test_filter="FullyQualifiedName~ExternalConsumerQueueRedeliveryTests"

current_stage="initialization"
trap 'status=$?; if [[ $status -ne 0 ]]; then printf "\n[FAIL] Stage: %s (exit %d)\n" "$current_stage" "$status" >&2; fi' EXIT

log_stage() {
  current_stage="$1"
  printf "\n==> %s\n" "$current_stage"
}

require_file() {
  local path="$1"

  if [[ ! -f "$path" ]]; then
    printf "Required file is missing: %s\n" "$path" >&2
    exit 1
  fi
}

export DOTNET_CLI_UI_LANGUAGE="${DOTNET_CLI_UI_LANGUAGE:-en}"

require_file "$pack_project"
require_file "$consumer_project"
require_file "$consumer_config"
require_file "$solution_path"
mkdir -p "$pack_output_dir"

log_stage "Pack Azure.InMemory into ./artifacts/pack"
rm -f "$expected_package"
dotnet pack "$pack_project" -c Release -o "$pack_output_dir"
require_file "$expected_package"

log_stage "Recreate isolated consumer package cache"
rm -rf "$consumer_packages_dir"
mkdir -p "$consumer_packages_dir"

log_stage "Restore external consumer with sample-local NuGet.Config and no cache"
dotnet restore "$consumer_project" \
  --configfile "$consumer_config" \
  --packages "$consumer_packages_dir" \
  --no-cache

log_stage "Run focused external consumer package proof without restore"
dotnet test "$consumer_project" \
  --no-restore \
  --filter "$consumer_test_filter"

log_stage "Run producer solution regression suite"
dotnet test "$solution_path"

log_stage "Verification loop completed successfully"
printf "Fresh package verified through consumer and producer test boundaries.\n"
