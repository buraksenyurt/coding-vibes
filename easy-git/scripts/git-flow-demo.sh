#!/usr/bin/env bash
#
# git-flow-demo.sh — builds a small, purely local repository that follows the
# git flow branching model, so you can open it in easy-git and watch how the
# branches are drawn. No remote, no network, nothing outside the target folder.
#
# Usage:   ./scripts/git-flow-demo.sh [target-folder]      (default: ./git-flow-sandbox)
# Windows: run it from Git Bash (installed together with Git for Windows).
#
# The story spans the last two months and ends today, so the "stale" badge
# (no commit for 30+ days, not merged) lands only on the abandoned branch:
#
#   main       ●──────────────────●(v1.0.0)──●(v1.0.1)──────────────
#   develop     ●──●────●────●────●───────────●──────●────●─────────  (HEAD)
#   feature/user-auth        (merged, deleted -> name recovered from the merge)
#   feature/product-search   (merged, deleted)
#   feature/shopping-cart    (synced with develop once, then merged, deleted)
#   feature/wishlist         (abandoned 7 weeks ago -> "stale")
#   release/1.0.0            (merged into main + develop, deleted)
#   hotfix/1.0.1             (merged into main + develop, deleted)
#   feature/payment-gateway  (in progress)
#   release/1.1.0            (in progress)

set -euo pipefail

TARGET="${1:-./git-flow-sandbox}"

if [ -e "$TARGET" ] && [ -n "$(ls -A "$TARGET" 2>/dev/null)" ]; then
  echo "error: '$TARGET' already exists and is not empty. Pick another folder or delete it first." >&2
  exit 1
fi
mkdir -p "$TARGET"
cd "$TARGET"

# --- helpers -----------------------------------------------------------------

# A fake clock. Each chapter of the story starts a given number of days ago
# (days_ago), and every commit or merge inside it moves the clock a little.
NOW=$(date +%s)
CLOCK=$NOW
days_ago() { CLOCK=$(( NOW - $1 * 86400 )); }

# Fictional people; your own git identity and config are never used.
as_ayla() { AUTHOR_NAME="Ayla Kaya";   AUTHOR_EMAIL="ayla@example.com"; }
as_mert() { AUTHOR_NAME="Mert Yildiz"; AUTHOR_EMAIL="mert@example.com"; }
as_ayla

# Runs git with a fixed identity, the current fake clock, and no signing prompts.
g() {
  GIT_AUTHOR_NAME="$AUTHOR_NAME" GIT_AUTHOR_EMAIL="$AUTHOR_EMAIL" \
  GIT_COMMITTER_NAME="$AUTHOR_NAME" GIT_COMMITTER_EMAIL="$AUTHOR_EMAIL" \
  GIT_AUTHOR_DATE="@$CLOCK +0300" GIT_COMMITTER_DATE="@$CLOCK +0300" \
  git -c init.defaultBranch=main -c commit.gpgsign=false -c tag.gpgsign=false \
      -c core.autocrlf=false -c advice.detachedHead=false "$@"
}

# later <hours>: advance the fake clock.
later() { CLOCK=$(( CLOCK + $1 * 3600 )); }

# commit <file> <message>: every commit writes its own file, so merges never conflict.
commit() {
  mkdir -p "$(dirname "$1")"
  echo "$2" > "$1"
  g add "$1"
  g commit -q -m "$2"
  later 3
}

branch()  { g checkout -q -b "$1" "$2"; }                       # branch <new> <from>
switch()  { g checkout -q "$1"; }
merge()   { switch "$1"; g merge -q --no-ff --no-edit "$2"; later 2; }   # merge <into> <from>
finish()  { g branch -q -d "$1"; }                              # delete a finished branch
tag()     { g tag -a "$1" -m "Release $1"; }

say() { printf '  %s\n' "$*"; }

# --- the story ---------------------------------------------------------------

echo "Building a git flow sample in $(pwd)"
g init -q

say "main and develop"
days_ago 58
commit README.md "Initial commit"
commit .gitignore "Add .gitignore"
branch develop main
commit src/app.txt "Set up application skeleton"

say "feature/user-auth (merged and deleted)"
days_ago 55
as_mert
branch feature/user-auth develop
commit src/auth/login.txt "Add login screen"
commit src/auth/token.txt "Issue JWT tokens on login"
commit src/auth/logout.txt "Add logout"
merge develop feature/user-auth
finish feature/user-auth

say "feature/wishlist (started, then abandoned)"
days_ago 50
as_ayla
branch feature/wishlist develop
commit src/wishlist/model.txt "Add wishlist model"
commit src/wishlist/api.txt "Draft wishlist endpoints"

say "feature/product-search and feature/shopping-cart in parallel"
days_ago 40
switch develop
branch feature/product-search develop
commit src/search/index.txt "Build product search index"
as_mert
branch feature/shopping-cart develop
commit src/cart/model.txt "Add cart model"
as_ayla
switch feature/product-search
commit src/search/filters.txt "Add price and category filters"
as_mert
switch feature/shopping-cart
commit src/cart/totals.txt "Calculate cart totals"
as_ayla
merge develop feature/product-search
finish feature/product-search

say "release/1.0.0 (stabilise, ship, merge back)"
days_ago 30
as_mert
branch release/1.0.0 develop
commit VERSION "Bump version to 1.0.0"
commit src/app-fix.txt "Fix typo on the welcome page"
merge main release/1.0.0
tag v1.0.0
merge develop release/1.0.0
finish release/1.0.0

say "hotfix/1.0.1 (urgent fix on production)"
days_ago 24
branch hotfix/1.0.1 main
commit src/auth/expiry.txt "Fix token expiry off-by-one"
merge main hotfix/1.0.1
tag v1.0.1
merge develop hotfix/1.0.1
finish hotfix/1.0.1

say "feature/shopping-cart catches up with develop, then finishes"
days_ago 14
switch feature/shopping-cart
g merge -q --no-ff --no-edit develop
later 2
commit src/cart/checkout.txt "Add checkout button"
as_ayla
merge develop feature/shopping-cart
finish feature/shopping-cart

say "feature/payment-gateway (in progress)"
days_ago 6
as_mert
branch feature/payment-gateway develop
commit src/payment/client.txt "Add payment provider client"
commit src/payment/webhook.txt "Handle payment webhooks"

say "release/1.1.0 (in progress)"
days_ago 3
as_ayla
switch develop
commit docs/CHANGELOG.md "Update changelog"
branch release/1.1.0 develop
commit VERSION "Bump version to 1.1.0"

say "back on develop"
days_ago 1
switch develop
commit docs/CONTRIBUTING.md "Describe the branching model"

echo
echo "Done. Branches:"
git branch --format='  %(refname:short)'
echo "Tags: $(git tag | tr '\n' ' ')"
echo
echo "Open this folder in easy-git with \"Repo seç\": $(pwd)"
