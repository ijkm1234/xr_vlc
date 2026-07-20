#!/bin/bash
set -e

ANDROID_SDK_ROOT="$HOME/Library/Android/sdk"
JDK_INSTALL_DIR="$HOME/Library/Java/JavaVirtualMachines"
JDK_HOME="$JDK_INSTALL_DIR/temurin-17.jdk/Contents/Home"

echo "===== Step 1: Install JDK 17 (ARM64, no sudo) ====="

JDK_TAR="OpenJDK17U-jdk_aarch64_mac_hotspot_17.0.15_6.tar.gz"
JDK_URL="https://github.com/adoptium/temurin17-binaries/releases/download/jdk-17.0.15%2B6/${JDK_TAR}"
JDK_TMP="/tmp/${JDK_TAR}"

mkdir -p "$JDK_INSTALL_DIR"

if [ ! -d "$JDK_HOME" ]; then
    echo "Downloading JDK 17 (tar.gz)..."
    curl -L --progress-bar -o "$JDK_TMP" "$JDK_URL"
    echo "Extracting JDK..."
    mkdir -p "$JDK_INSTALL_DIR/temurin-17.jdk/Contents"
    tar -xzf "$JDK_TMP" -C /tmp/
    # tar extracts to jdk-17.0.15+6
    JDK_EXTRACTED=$(find /tmp -maxdepth 1 -name "jdk-17*" -type d | head -1)
    mv "$JDK_EXTRACTED" "$JDK_INSTALL_DIR/temurin-17.jdk/Contents/Home"
    rm -f "$JDK_TMP"
    echo "JDK 17 installed at $JDK_HOME"
else
    echo "JDK 17 already installed."
fi

export JAVA_HOME="$JDK_HOME"
export PATH="$JAVA_HOME/bin:$PATH"
echo "Java version:"
java -version

echo ""
echo "===== Step 2: Download Android Command Line Tools ====="

CMDLINE_ZIP="commandlinetools-mac-12266719_latest.zip"
CMDLINE_URL="https://dl.google.com/android/repository/${CMDLINE_ZIP}"
CMDLINE_TMP="/tmp/${CMDLINE_ZIP}"

mkdir -p "$ANDROID_SDK_ROOT/cmdline-tools"

if [ ! -d "$ANDROID_SDK_ROOT/cmdline-tools/latest" ]; then
    echo "Downloading Android command line tools..."
    curl -L --progress-bar -o "$CMDLINE_TMP" "$CMDLINE_URL"
    echo "Extracting..."
    unzip -q "$CMDLINE_TMP" -d /tmp/cmdline-tools-extract
    mv /tmp/cmdline-tools-extract/cmdline-tools "$ANDROID_SDK_ROOT/cmdline-tools/latest"
    rm -f "$CMDLINE_TMP"
    echo "Command line tools installed."
else
    echo "Command line tools already installed."
fi

export ANDROID_HOME="$ANDROID_SDK_ROOT"
export PATH="$ANDROID_SDK_ROOT/cmdline-tools/latest/bin:$ANDROID_SDK_ROOT/platform-tools:$PATH"

echo ""
echo "===== Step 3: Install Android SDK packages ====="

yes | sdkmanager --licenses > /dev/null 2>&1 || true

echo "Installing: platform-tools, platforms;android-34, build-tools;34.0.0, ndk;21.4.7075529"
sdkmanager \
    "platform-tools" \
    "platforms;android-34" \
    "build-tools;34.0.0" \
    "ndk;21.4.7075529"

echo ""
echo "===== Step 4: Update local.properties ====="

LOCAL_PROPS="/Users/admin/vlc-android/local.properties"
cat > "$LOCAL_PROPS" << EOF
sdk.dir=$ANDROID_SDK_ROOT
android.ndkFullVersion=21.4.7075529
EOF
echo "Updated: $LOCAL_PROPS"

echo ""
echo "===== Step 5: Set environment variables (add to ~/.zshrc) ====="

ZSHRC="$HOME/.zshrc"
if ! grep -q "temurin-17" "$ZSHRC" 2>/dev/null; then
    cat >> "$ZSHRC" << 'ENVEOF'

# Android / Java build env
export JAVA_HOME="$HOME/Library/Java/JavaVirtualMachines/temurin-17.jdk/Contents/Home"
export ANDROID_HOME="$HOME/Library/Android/sdk"
export PATH="$JAVA_HOME/bin:$ANDROID_HOME/cmdline-tools/latest/bin:$ANDROID_HOME/platform-tools:$PATH"
ENVEOF
    echo "Added env vars to ~/.zshrc"
else
    echo "Env vars already in ~/.zshrc"
fi

echo ""
echo "===== Setup Complete! ====="
echo ""
echo "Now build the AAR:"
echo "  cd /Users/admin/vlc-android"
echo "  export JAVA_HOME=$JDK_HOME"
echo "  ./gradlew :application:vlc-android:assembleDebug"
echo ""
echo "AAR output:"
echo "  application/vlc-android/build/outputs/aar/vlc-android-debug.aar"
