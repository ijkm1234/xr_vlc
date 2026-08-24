# PICO Unity SDK local dependency

The project intentionally uses a patched local checkout of PICO Unity Integration SDK 3.4.0. Unity resolves the package through the stable local link `.unity-local-packages/com.unity.xr.picoxr`; do not replace it with a direct Git dependency in `Packages/manifest.json`.

Set the local working-copy location and run the setup script before opening or building the Unity project:

```bash
export PICO_UNITY_SDK_DIR="/absolute/path/to/PICO-Unity-Integration-SDK"
./scripts/setup-pico-unity-sdk.sh
```

For a project-local checkout:

```bash
export PICO_UNITY_SDK_DIR="$PWD/.local-dependencies/PICO-Unity-Integration-SDK"
./scripts/setup-pico-unity-sdk.sh
```

The script:

1. initializes the local working copy and shallow-fetches the pinned commit from the official repository;
2. checks out pinned revision `e0740bf309b26a0b9adae33c506514569bfe5d38`;
3. applies `pico-unity-integration-sdk-3.4.0.patch` idempotently;
4. points Unity's stable local package link at the environment-controlled working copy.

The patch exposes `InitializeBuffer()`, preserves the real ExternalSurface dimensions, and keeps the legacy overlay dimension behavior. Layer creation remains centralized in `InitializeBuffer()` so `CreateExternalSurface()` only requests the Android Surface for the existing layer. If the upstream revision changes, update and revalidate the patch before changing the pinned revision.
