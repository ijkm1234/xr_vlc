#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
vlc_root="$project_root/vlc-android"
local_properties="$vlc_root/local.properties"
native_gradle_version="8.6"
aar_gradle_version="8.13"

read_property() {
    sed -n "s/^$1=//p" "$local_properties" | head -n 1
}

if [[ ! -f "$local_properties" ]]; then
    echo "Missing $local_properties. Configure the Android SDK and NDK first." >&2
    exit 1
fi

android_sdk="$(read_property 'sdk\.dir')"
android_ndk="$(read_property 'android\.ndkPath')"
if [[ -z "$android_sdk" || -z "$android_ndk" || ! -d "$android_sdk" || ! -d "$android_ndk" ]]; then
    echo "local.properties must contain existing sdk.dir and android.ndkPath values." >&2
    exit 1
fi

java_home="${JAVA_HOME:-}"
if [[ ! -x "$java_home/bin/java" ]]; then
    java_home="$(/usr/libexec/java_home -v 17 2>/dev/null || true)"
fi
if [[ ! -x "$java_home/bin/java" ]]; then
    echo "A usable Java 17 home is required for the Gradle build." >&2
    exit 1
fi
export GRADLE_OPTS="${GRADLE_OPTS:-} -Dorg.gradle.java.home=$java_home"

if [[ -z "${GRADLE_HOME:-}" ]]; then
    cached_gradle="$(find "$HOME/.gradle/wrapper/dists/gradle-${native_gradle_version}-all" -type d -name "gradle-${native_gradle_version}" -print -quit 2>/dev/null || true)"
    if [[ -n "$cached_gradle" ]]; then
        export GRADLE_HOME="$cached_gradle"
    fi
fi

export ANDROID_SDK="$android_sdk"
export ANDROID_NDK="$android_ndk"

cd "$vlc_root"
./buildsystem/compile.sh -a arm64-v8a -l -b

# compile.sh regenerates its wrapper for the Gradle 8.6 native build. The
# libvlcjni Android Gradle Plugin requires Gradle 8.13 for the AAR packaging
# phase, so switch only the generated wrapper distribution before invoking it.
wrapper_properties="$vlc_root/gradle/wrapper/gradle-wrapper.properties"
if [[ ! -f "$wrapper_properties" ]]; then
    echo "Missing Gradle wrapper properties after native compilation." >&2
    exit 1
fi
perl -0pi -e "s|distributionUrl=.*|distributionUrl=https\\://services.gradle.org/distributions/gradle-${aar_gradle_version}-bin.zip|" "$wrapper_properties"
GRADLE_ABI=arm64-v8a ./gradlew \
    :application:vlc-android:clean \
    :application:vlc-android:assembleDebug

aar_source="$vlc_root/application/vlc-android/build/outputs/aar/vlc-android-debug.aar"
aar_target="$project_root/Assets/Plugins/Android/vlc-android-debug.aar"
for native_lib in libvlc.so libvlcjni.so libc++_shared.so; do
    unzip -l "$aar_source" | grep -q "jni/arm64-v8a/$native_lib" || {
        echo "Missing $native_lib in $aar_source" >&2
        exit 1
    }
done

cp "$aar_source" "$aar_target"
cmp -s "$aar_source" "$aar_target"
echo "Built and installed $aar_target"
