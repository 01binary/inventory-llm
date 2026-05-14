#!/usr/bin/env bash
set -euo pipefail

# Run this while checked out on es-MX.
if [[ "$(git rev-parse --abbrev-ref HEAD)" != "es-MX" ]]; then
  echo "Error: check out es-MX before running this script." >&2
  exit 1
fi

if ! git diff --quiet || ! git diff --cached --quiet; then
  echo "Error: working tree is not clean. Commit or stash your changes first." >&2
  exit 1
fi

git fetch origin main
pre_merge_ref="$(git rev-parse HEAD)"

set +e
git merge --no-ff --no-commit origin/main
merge_exit=$?
set -e

locale_files=(
  SYSTEM_PROMPT.md
  STARTUP_PROMPT.md
  client/src/services/browserSpeech.js
  server/Options/SpeechOptions.cs
  server/appsettings.json
  server/appsettings.Development.json
)

git restore --source "$pre_merge_ref" -- "${locale_files[@]}"
git add "${locale_files[@]}"

unmerged_files="$(git diff --name-only --diff-filter=U)"
if [[ -n "$unmerged_files" ]]; then
  echo "Merge has conflicts outside locale files. Resolve these manually:" >&2
  echo "$unmerged_files" >&2
  exit 1
fi

if git diff --cached --quiet; then
  git merge --abort
  echo "No merge changes to commit. es-MX is already up to date."
  exit 0
fi

git commit -m "Merge main into es-MX (keep Spanish locale defaults)"

echo "Done. Review and push with: git push"

# If the merge command exited non-zero only due to resolved locale conflicts,
# we intentionally proceed as long as there are no remaining unmerged files.
if [[ $merge_exit -ne 0 ]]; then
  echo "Note: merge reported conflicts; locale conflicts were auto-resolved."
fi
