#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
repository_url="https://github.com/Pico-Developer/PICO-Unity-Integration-SDK.git"
revision="e0740bf309b26a0b9adae33c506514569bfe5d38"
patch_file="$project_root/PicoSDK_Patches/pico-unity-integration-sdk-3.4.0.patch"
package_link_dir="$project_root/.unity-local-packages"
package_link="$package_link_dir/com.unity.xr.picoxr"

if [[ -z "${PICO_UNITY_SDK_DIR:-}" ]]; then
    echo "PICO_UNITY_SDK_DIR is required and must point to the local SDK working copy." >&2
    echo "Example: export PICO_UNITY_SDK_DIR=\"$project_root/.local-dependencies/PICO-Unity-Integration-SDK\"" >&2
    exit 2
fi

case "$PICO_UNITY_SDK_DIR" in
    /*) sdk_input="$PICO_UNITY_SDK_DIR" ;;
    *) sdk_input="$project_root/$PICO_UNITY_SDK_DIR" ;;
esac

sdk_parent="$(dirname "$sdk_input")"
sdk_name="$(basename "$sdk_input")"
mkdir -p "$sdk_parent"
sdk_dir="$(cd "$sdk_parent" && pwd)/$sdk_name"

if [[ "$sdk_dir" == "/" || "$sdk_dir" == "$project_root" ]]; then
    echo "Refusing unsafe PICO_UNITY_SDK_DIR: $sdk_dir" >&2
    exit 2
fi

if [[ ! -e "$sdk_dir" ]]; then
    echo "Initializing PICO Unity Integration SDK working copy in $sdk_dir"
    mkdir -p "$sdk_dir"
    git -C "$sdk_dir" init
    git -C "$sdk_dir" remote add origin "$repository_url"
elif [[ ! -d "$sdk_dir/.git" ]]; then
    echo "PICO_UNITY_SDK_DIR exists but is not a Git working copy: $sdk_dir" >&2
    exit 2
fi

origin_url="$(git -C "$sdk_dir" remote get-url origin 2>/dev/null || true)"
if [[ "$origin_url" != "$repository_url" ]]; then
    echo "Unexpected origin for PICO_UNITY_SDK_DIR: $origin_url" >&2
    echo "Expected: $repository_url" >&2
    exit 2
fi

echo "Fetching pinned PICO SDK revision $revision"
git -C "$sdk_dir" fetch --depth 1 --no-tags --prune origin "$revision"

current_revision="$(git -C "$sdk_dir" rev-parse HEAD 2>/dev/null || true)"
if [[ "$current_revision" != "$revision" ]]; then
    if [[ -n "$(git -C "$sdk_dir" status --porcelain)" ]]; then
        echo "Cannot switch SDK revisions because the working copy has local changes:" >&2
        git -C "$sdk_dir" status --short >&2
        exit 2
    fi
    git -C "$sdk_dir" checkout --detach "$revision"
fi

if git -C "$sdk_dir" apply --reverse --check "$patch_file" >/dev/null 2>&1; then
    echo "PICO SDK patch is already applied"
elif [[ -n "$(git -C "$sdk_dir" status --porcelain)" ]]; then
    echo "Cannot apply the PICO SDK patch because the working copy has unexpected changes:" >&2
    git -C "$sdk_dir" status --short >&2
    exit 2
elif git -C "$sdk_dir" apply --check "$patch_file"; then
    git -C "$sdk_dir" apply "$patch_file"
    echo "Applied $patch_file"
else
    echo "The PICO SDK patch does not apply cleanly to revision $revision" >&2
    exit 2
fi

package_name="$(sed -n 's/^[[:space:]]*\"name\":[[:space:]]*\"\([^\"]*\)\".*/\1/p' "$sdk_dir/package.json" | head -n 1)"
if [[ "$package_name" != "com.unity.xr.picoxr" ]]; then
    echo "Unexpected package name in $sdk_dir/package.json: $package_name" >&2
    exit 2
fi

mkdir -p "$package_link_dir"
if [[ -e "$package_link" && ! -L "$package_link" ]]; then
    echo "Local package link path exists and is not a symbolic link: $package_link" >&2
    exit 2
fi
ln -sfn "$sdk_dir" "$package_link"

echo "PICO SDK local package is ready"
echo "  revision: $revision"
echo "  working copy: $sdk_dir"
echo "  Unity package: $package_link"
