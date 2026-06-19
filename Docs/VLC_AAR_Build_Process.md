# VLC 底层到 Unity fat AAR 编译流程

本文记录本项目从 VLC/libVLC 底层修改到 Android fat AAR，再放入 Unity `Assets/Plugins/Android` 的标准流程。

## 整体概览

本项目有两类改动：

- VLC/libVLC 底层 C/C++ 改动：例如 subtitle cue、libass 文本保留。这类改动最终必须进入 `libvlc.so`。
- Android AAR 层 Kotlin/Java 改动：例如 URI 传递、PlaybackServiceBridge、UnitySendMessage。这类改动最终进入 `classes.jar` / dex。

完整链路如下：

```text
VLC 底层源码改动
  -> 在 libvlcjni/libvlc/patches 中保存为 *.patch
  -> get-vlc.sh 克隆/重置 VLC 源码并用 git am 应用 patch
  -> compile.sh / compile-libvlc.sh 编译 native so
  -> native so 落到 jni/obj/local/arm64-v8a 与 jni/libs/arm64-v8a
  -> libvlcjni/libvlc/jni/libs/arm64-v8a/*.so
  -> :libvlcjni:libvlc:assembleDebug 打成 libvlc-debug.aar
  -> :application:vlc-android:assembleDebug embed libvlc-debug.aar
  -> vlc-android-debug.aar
  -> 复制到 Assets/Plugins/Android/vlc-android-debug.aar
  -> Unity 构建 APK 时把 AAR 内 so/classes 打进 APK
```

核心判断标准：

- 改 C/C++ 后，先确认 `libvlc.so` 变了。
- 再确认 `libvlc-debug.aar` 里的 `libvlc.so` 变了。
- 再确认 `vlc-android-debug.aar` 里的 `libvlc.so` 变了。
- 最后确认 Unity APK 里的 `libvlc.so` 变了。

如果只跑 `:application:vlc-android:assembleDebug`，它通常只会重新打包 AAR，不会自动重新编译 VLC C/C++。所以 C/C++ 修改必须先走 native so 编译。

## patch 工作机制

`libvlcjni/libvlc/patches/*.patch` 是 VLC 源码改动的持久化载体。它的作用不是直接参与 Gradle 编译，而是在准备 VLC 源码树时，把项目自定义改动重新应用到官方 VLC tested commit 上。

实际入口是：

```bash
/Users/admin/xr_vlc/vlc-android/libvlcjni/buildsystem/get-vlc.sh
```

`vlc-android/buildsystem/compile.sh` 在没有设置 `VLC_SRC_DIR` 时会调用它：

```text
compile.sh
  -> libvlcjni/buildsystem/get-vlc.sh
      -> clone / reset VLC 到 VLC_TESTED_HASH
      -> git am --message-id libvlc/patches/*.patch
      -> 检查每个 patch 的 Message-Id 是否已经在 git log 中
```

`get-vlc.sh` 有三种典型行为：

1. 首次没有 `libvlcjni/vlc` 目录：
   - clone 官方 VLC；
   - reset 到脚本内固定的 `VLC_TESTED_HASH`；
   - 按文件名顺序执行 `git am --message-id libvlc/patches/*.patch`；
   - patch 变成 VLC 源码树里的真实 git commit。

2. 已经有 `libvlcjni/vlc` 目录，且不传 `-b`：
   - 不重新应用 patch；
   - 只检查每个 patch 文件里的 `Message-Id` 是否能在当前 VLC git log 中找到；
   - 找不到就中断，防止源码树缺 patch 或 patch 不是用 `git am --message-id` 方式应用的。

3. 传 `-b`：
   - bypass source checks；
   - 允许使用当前本地 VLC 源码树继续编译；
   - 适合已经在 `libvlcjni/vlc` 里手动改完并确认代码状态的开发阶段。

常见误区：

- patch 文件不是“编译输入文件”。真正参与 C/C++ 编译的是 `libvlcjni/vlc` 目录下已经被 patch 修改后的源码。
- patch 不会在每次 `assembleDebug` 时自动应用。`assembleDebug` 主要是 Gradle 打包动作。
- 如果 `libvlcjni/vlc` 已存在，新增 patch 后不重置/不应用到源码树，native 编译不会凭空带上这个 patch。
- `Message-Id` 很重要，因为脚本用它判断 patch 是否已经应用。推荐用 `git format-patch` 生成 patch，不要手写 patch 头。

本项目当前自定义空间字幕相关 patch：

- `0021-libvlc-media_player-emit-subtitle-cue-events.patch`：在 libVLC 事件层增加 playback-time subtitle cue 事件。
- `0022-spu-preserve-libass-subtitle-cue-text.patch`：在 libass 渲染前保留纯文本字幕，避免 ASS 最终变成 bitmap 后 Unity 拿不到文本。
- `0023-spu-include-subtitle-cue-v2-style-metadata.patch`：扩展 subtitle cue v2 元数据通道，回传 `styleRuns`、`fontAttachments`、`layout`，供 Skia 空间字幕渲染链路使用。
- `0024-contrib-soxr-allow-configuring-with-cmake-4.patch`：为 soxr contrib 增加 CMake 4 兼容参数，避免底层 native 编译在 soxr 配置阶段失败。
- `0025-codec-libass-include-vlc-memstream-header.patch`：补齐 libass cue 元数据辅助函数所需的 `vlc_memstream` 头文件。
- `0026-spu-emit-subtitle-cue-media-timestamps.patch`：在 SPU 输出前用 input `time` 加剩余显示延迟，把 subtitle cue 的 `startMs/endMs` 投影回媒体时间轴，避免 Unity 空间字幕 scheduler 等不到 cue。

## 目录约定

- Unity 项目根目录：`/Users/admin/xr_vlc`
- VLC Android 源码目录：`/Users/admin/xr_vlc/vlc-android`
- libVLC 源码目录：`/Users/admin/xr_vlc/vlc-android/libvlcjni/vlc`
- VLC patch 目录：`/Users/admin/xr_vlc/vlc-android/libvlcjni/libvlc/patches`
- libVLC native so 打包输入目录：`/Users/admin/xr_vlc/vlc-android/libvlcjni/libvlc/jni/libs/arm64-v8a`
- libVLC native so 中间产物目录：`/Users/admin/xr_vlc/vlc-android/libvlcjni/libvlc/jni/obj/local/arm64-v8a`
- libVLC AAR 输出目录：`/Users/admin/xr_vlc/vlc-android/libvlcjni/libvlc/build/outputs/aar`
- AAR 输出路径：`/Users/admin/xr_vlc/vlc-android/application/vlc-android/build/outputs/aar/vlc-android-debug.aar`
- Unity plugin 目标路径：`/Users/admin/xr_vlc/Assets/Plugins/Android/vlc-android-debug.aar`

## 1. 修改 VLC 底层源码

底层源码直接位于：

```bash
cd /Users/admin/xr_vlc/vlc-android/libvlcjni/vlc
```

空间字幕 cue 相关改动目前涉及：

- `include/vlc_subpicture.h`
- `src/misc/subpicture.c`
- `src/input/decoder.c`
- `modules/codec/libass.c`

修改后先在 VLC 源码仓库内查看差异：

```bash
git -C /Users/admin/xr_vlc/vlc-android/libvlcjni/vlc diff -- \
  include/vlc_subpicture.h \
  src/misc/subpicture.c \
  src/input/decoder.c \
  modules/codec/libass.c
```

如需要让 `vlc-android` 后续重新拉取/重建 libVLC 时自动带上这些改动，需要在 VLC 源码仓库生成 commit，再导出到 patch 目录。patch 是“可重放的源码改动记录”，后续 `get-vlc.sh --reset` 或首次拉取 VLC 源码时会用 `git am --message-id` 重放这些 commit。

```bash
cd /Users/admin/xr_vlc/vlc-android/libvlcjni/vlc
git add include/vlc_subpicture.h src/misc/subpicture.c src/input/decoder.c modules/codec/libass.c
git commit -m "spu: preserve libass subtitle cue text"
git format-patch --thread -1 HEAD --stdout > /Users/admin/xr_vlc/vlc-android/libvlcjni/libvlc/patches/0022-spu-preserve-libass-subtitle-cue-text.patch
```

注意：

- patch 文件头里的 `Message-Id` 必须保持合法格式，否则 `vlc-android` 的 patch 检查会失败。
- 如果修改已有 patch，优先用 `git format-patch` 重新生成，避免手写 patch 元数据。
- 生成 patch 后，还要确保当前 `libvlcjni/vlc` 源码树也包含同一份改动。patch 只是未来重建源码树时的重放依据，不会自动修改当前工作区。

需要从 tested hash 重新应用所有 patch 时使用：

```bash
cd /Users/admin/xr_vlc/vlc-android/libvlcjni
./buildsystem/get-vlc.sh --reset
```

开发阶段如果已经确认当前 `libvlcjni/vlc` 就是要编译的源码，可以在后续 native 构建时使用 `-b` 跳过 patch presence 检查。

## 2. 从 VLC 源码到 native so

进入 `vlc-android` 根目录：

```bash
cd /Users/admin/xr_vlc/vlc-android
```

如果改了 VLC C/C++ 底层代码，先重新编译 arm64-v8a 的 libVLC native so：

```bash
./buildsystem/compile.sh -a arm64-v8a -l -b
```

参数含义：

- `-a arm64-v8a`：只构建 PICO/Android 当前使用的 arm64 ABI。
- `-l`：只构建 LibVLC，不构建完整 VLC Android 应用。
- `-b`：允许使用本地修改过的 VLC 源码，避免源码检查把本地 patch 当成异常。

如果希望从 `VLC_TESTED_HASH + patches` 的干净状态重新构建，可以先执行：

```bash
cd /Users/admin/xr_vlc/vlc-android/libvlcjni
./buildsystem/get-vlc.sh --reset
cd /Users/admin/xr_vlc/vlc-android
./buildsystem/compile.sh -a arm64-v8a -l
```

如果已经在 `libvlcjni/vlc` 中有本地开发改动，还没整理成 patch，使用 `-b` 更合适，否则 source check 会因为找不到 patch 的 `Message-Id` 或源码状态不一致而中断。

这一步的输出不是 AAR，而是 native `.so`。构建完成后重点看两个目录：

```bash
ls -lh /Users/admin/xr_vlc/vlc-android/libvlcjni/libvlc/jni/obj/local/arm64-v8a/libvlc.so
ls -lh /Users/admin/xr_vlc/vlc-android/libvlcjni/libvlc/jni/libs/arm64-v8a/libvlc.so
ls -lh /Users/admin/xr_vlc/vlc-android/libvlcjni/libvlc/jni/libs/arm64-v8a/libvlcjni.so
ls -lh /Users/admin/xr_vlc/vlc-android/libvlcjni/libvlc/jni/libs/arm64-v8a/libc++_shared.so
```

区别：

- `jni/obj/local/arm64-v8a` 是 native 构建中间产物目录。
- `jni/libs/arm64-v8a` 是 Gradle `libvlc` 模块真正打包进 AAR 的输入目录。
- 如果 `obj/local` 是新的，但 `jni/libs` 还是旧的，后续 AAR 仍会带旧 so。

校验 `jni/libs` 里的 `libvlc.so` 已包含本次底层改动：

```bash
strings /Users/admin/xr_vlc/vlc-android/libvlcjni/libvlc/jni/libs/arm64-v8a/libvlc.so | \
  rg 'XR_SUBTITLE_CUE|libass extracted'
```

只有这里能搜到新字符串，才说明 C/C++ 改动已经进入 Gradle 后续会打包的 native so。

## 3. 从 native so 到 libVLC AAR

这一节只负责把 `libvlcjni/libvlc/jni/libs/arm64-v8a/*.so` 打进 `libvlc-debug.aar`。它不重新编译 VLC C/C++。

对应 Gradle 文件：

```text
/Users/admin/xr_vlc/vlc-android/libvlcjni/libvlc/build.gradle
```

关键配置：

```gradle
sourceSets {
    main {
        jniLibs.srcDirs = [ 'jni/libs' ]
        jniLibs.srcDirs += "$vlcSrcDirs"
    }
}
```

含义：

- `apply plugin: 'com.android.library'`：这个模块会输出 Android library AAR。
- `sourceSets.main.jniLibs.srcDirs = [ 'jni/libs' ]`：Gradle 从 `libvlcjni/libvlc/jni/libs` 收集 native `.so`。
- `jniLibs.srcDirs += "$vlcSrcDirs"`：允许通过 `GRADLE_VLC_SRC_DIRS` 额外传入 so 目录；本项目常规路径不依赖它。
- `libraryVariants.all` 中重命名输出文件：设置 ABI 相关 AAR 名称；当前本地输出也可能是 `libvlc-debug.aar`。
- `clean { delete 'build', 'jni/libs', 'jni/obj' }`：清理会删除 so 输入目录和中间产物目录，所以执行 clean 后必须重新跑 native 编译。

把当前 `jni/libs` 下的 so 打成 libVLC AAR：

```bash
GRADLE_ABI=arm64-v8a java -classpath gradle/wrapper/gradle-wrapper.jar org.gradle.wrapper.GradleWrapperMain \
  :libvlcjni:libvlc:assembleDebug
```

产物位于：

```bash
ls -lh /Users/admin/xr_vlc/vlc-android/libvlcjni/libvlc/build/outputs/aar/*.aar
```

校验 libVLC AAR 内包含 so：

```bash
LIBVLC_AAR="$(find /Users/admin/xr_vlc/vlc-android/libvlcjni/libvlc/build/outputs/aar -maxdepth 1 -type f -name '*.aar' | head -n 1)"
unzip -l "$LIBVLC_AAR" | rg 'jni/arm64-v8a/(libvlc.so|libvlcjni.so|libc\+\+_shared.so)'
```

校验 libVLC AAR 里的 `libvlc.so` 已包含本次底层改动：

```bash
unzip -p "$LIBVLC_AAR" jni/arm64-v8a/libvlc.so | strings | \
  rg 'XR_SUBTITLE_CUE|libass extracted'
```

只有这里能搜到新字符串，才说明底层 C/C++ 修改已经进入 libVLC AAR。

## 4. 从 libVLC AAR 到 vlc-android fat AAR

进入 `vlc-android` 根目录：

```bash
cd /Users/admin/xr_vlc/vlc-android
```

这一节负责把 `application/vlc-android` 自己的 Kotlin/Java 代码、资源、依赖模块，以及本地 `:libvlcjni:libvlc` 模块一起打成 Unity 使用的 fat AAR。

对应 Gradle 文件：

```text
/Users/admin/xr_vlc/vlc-android/application/vlc-android/build.gradle
```

关键配置：

```gradle
apply plugin: 'com.kezong.fat-aar'

fataar {
    transitive = true
    keepR8Verify = false
}

dependencies {
    debugApi project(':libvlcjni:libvlc')
    releaseApi "org.videolan.android:libvlc-all:$rootProject.ext.libvlcVersion"
    debugEmbed project(':libvlcjni:libvlc')
}
```

含义拆开看：

- `debugApi project(':libvlcjni:libvlc')`：debug 编译时使用本地 `libvlc` 模块，因此 Java/Kotlin 能引用本地 libVLC API。
- `debugEmbed project(':libvlcjni:libvlc')`：fat-aar 插件在 debug AAR 输出时把本地 `libvlc` AAR 内容嵌进去，包括 `jni/arm64-v8a/*.so`。
- `releaseApi "org.videolan.android:libvlc-all:..."`：release 默认用 Maven 上的官方 libVLC，不会带本地 C/C++ patch。要验证本地底层改动，使用 debug AAR。
- `apply plugin: 'com.kezong.fat-aar'` 和 `fataar { transitive = true }`：让输出的 `vlc-android-debug.aar` 带上被 embed 的依赖内容，而不是只保留普通依赖声明。
- `packagingOptions.jniLibs.pickFirsts += ['**/*.so']`：遇到重复 so 路径时选择一个，避免打包阶段因为重复 native 库失败。

在 `jni/libs` 和 `libvlc-debug.aar` 已更新后，执行 debug fat AAR 构建：

```bash
java -classpath gradle/wrapper/gradle-wrapper.jar org.gradle.wrapper.GradleWrapperMain \
  :application:vlc-android:assembleDebug
```

如果遇到 fat-aar exploded intermediates 里的重复资源，可以先清理目标模块再构建：

```bash
java -classpath gradle/wrapper/gradle-wrapper.jar org.gradle.wrapper.GradleWrapperMain \
  :application:vlc-android:clean \
  :application:vlc-android:assembleDebug
```

如果刚重新编译过 `libvlc.so`，但 fat AAR 里的 `libvlc.so` hash 仍是旧的，也使用上面的 `clean + assembleDebug`。fat-aar/Gradle 的 `merged_jni_libs`、`merged_native_libs`、`aar_rebundle` 中间目录有时会保留旧 native 库，单跑 `assembleDebug` 不一定刷新。

构建成功后产物应位于：

```bash
ls -lh /Users/admin/xr_vlc/vlc-android/application/vlc-android/build/outputs/aar/vlc-android-debug.aar
```

校验 fat AAR 里已经包含来自本地 libVLC AAR 的 so：

```bash
unzip -l /Users/admin/xr_vlc/vlc-android/application/vlc-android/build/outputs/aar/vlc-android-debug.aar | \
  rg 'jni/arm64-v8a/(libvlc.so|libvlcjni.so|libc\+\+_shared.so)'
```

再用 hash 确认 fat AAR 里的 `libvlc.so` 和 native 构建输出一致：

```bash
tmpdir="$(mktemp -d)"
unzip -p /Users/admin/xr_vlc/vlc-android/application/vlc-android/build/outputs/aar/vlc-android-debug.aar \
  jni/arm64-v8a/libvlc.so > "$tmpdir/fat-libvlc.so"
shasum -a 256 \
  /Users/admin/xr_vlc/vlc-android/libvlcjni/libvlc/jni/libs/arm64-v8a/libvlc.so \
  "$tmpdir/fat-libvlc.so"
rm -rf "$tmpdir"
```

## 5. 复制 AAR 到 Unity plugin

```bash
cp /Users/admin/xr_vlc/vlc-android/application/vlc-android/build/outputs/aar/vlc-android-debug.aar \
   /Users/admin/xr_vlc/Assets/Plugins/Android/vlc-android-debug.aar
```

确认 Unity plugin 中的 AAR 已更新：

```bash
ls -lh /Users/admin/xr_vlc/Assets/Plugins/Android/vlc-android-debug.aar
```

## 6. 校验 Unity plugin 中的 fat AAR 内容

确认 fat AAR 内包含 arm64 native 库：

```bash
unzip -l /Users/admin/xr_vlc/Assets/Plugins/Android/vlc-android-debug.aar | \
  rg 'jni/arm64-v8a/(libvlc.so|libvlcjni.so|libc\+\+_shared.so)'
```

确认 `libvlc.so` 内包含空间字幕 cue 相关字符串：

```bash
unzip -p /Users/admin/xr_vlc/Assets/Plugins/Android/vlc-android-debug.aar \
  jni/arm64-v8a/libvlc.so | strings | rg 'XR_SUBTITLE_CUE|libass extracted'
```

确认 Android AAR 层包含 Unity 播放桥接修复：

```bash
tmpdir="$(mktemp -d)"
unzip -q /Users/admin/xr_vlc/Assets/Plugins/Android/vlc-android-debug.aar classes.jar -d "$tmpdir"
javap -classpath "$tmpdir/classes.jar" -verbose org.videolan.vlc.media.MediaUtils | \
  rg 'Playback URI|Display URI'
javap -classpath "$tmpdir/classes.jar" -verbose 'org.videolan.vlc.bridge.PlaybackServiceBridge$onSurfacesCreated$1' | \
  rg 'Media mismatch|load'
rm -rf "$tmpdir"
```

预期可以看到：

- `Playback URI`
- `Display URI`
- `Media mismatch. Loading new media into PlaylistManager.`
- `PlaylistManager.load`

## 7. 可选：构建 Unity debug APK

当需要把新的 AAR 打进 APK 时执行：

```bash
/Applications/Unity/Hub/Editor/6000.4.1f1/Unity.app/Contents/MacOS/Unity \
  -quit \
  -batchmode \
  -nographics \
  -projectPath /Users/admin/xr_vlc \
  -executeMethod CommandLineAndroidBuild.BuildDebug \
  -outputPath /Users/admin/xr_vlc/Builds/xr_vlc-debug.apk \
  -logFile -
```

产物路径：

```bash
ls -lh /Users/admin/xr_vlc/Builds/xr_vlc-debug.apk
```

校验 APK 中是否包含新的 libVLC 字符串：

```bash
unzip -p /Users/admin/xr_vlc/Builds/xr_vlc-debug.apk lib/arm64-v8a/libvlc.so | \
  strings | rg 'XR_SUBTITLE_CUE|libass extracted'
```

校验 APK dex 中是否包含 AAR 层修复字符串：

```bash
tmpdir="$(mktemp -d)"
unzip -q /Users/admin/xr_vlc/Builds/xr_vlc-debug.apk 'classes*.dex' -d "$tmpdir"
for dex in "$tmpdir"/classes*.dex; do
  strings "$dex" | rg 'Playback URI|Display URI|Media mismatch. Loading new media into PlaylistManager.' || true
done
rm -rf "$tmpdir"
```

## 8. 安装 APK

安装命令：

```bash
/Users/admin/Library/Android/sdk/platform-tools/adb install -r /Users/admin/xr_vlc/Builds/xr_vlc-debug.apk
```

设备侧运行和 PICO 交互由操作者在头显中完成。不要在自动化流程里假定可以通过 adb 点击或启动完成 PICO 侧交互。

## 9. 常见问题

### patch 检查失败

优先检查 patch 头部元数据，特别是 `Message-Id`。建议从 VLC git commit 用 `git format-patch` 重新生成。

### AAR 构建出现重复资源

先执行模块 clean：

```bash
java -classpath gradle/wrapper/gradle-wrapper.jar org.gradle.wrapper.GradleWrapperMain \
  :application:vlc-android:clean \
  :application:vlc-android:assembleDebug
```

### Unity 中仍然没有新逻辑

按顺序确认：

1. `Assets/Plugins/Android/vlc-android-debug.aar` 的修改时间已更新。
2. AAR 内 `libvlc.so` 能搜到 `XR_SUBTITLE_CUE`。
3. APK 内 `libvlc.so` 也能搜到 `XR_SUBTITLE_CUE`。
4. APK dex 能搜到 `Playback URI` / `Display URI` / `Media mismatch. Loading new media into PlaylistManager.`。

如果 AAR 正确但 APK 不正确，说明 Unity 还没有重新打包或使用了缓存产物。

### 改了 C/C++ 但 fat AAR 没变化

按这个顺序排查：

1. `./buildsystem/compile.sh -a arm64-v8a -l -b` 是否成功结束。
2. `libvlcjni/libvlc/jni/libs/arm64-v8a/libvlc.so` 的修改时间是否更新。
3. `libvlcjni/libvlc/build/outputs/aar/*.aar` 内的 `libvlc.so` 是否能搜到新字符串。
4. `application/vlc-android/build/outputs/aar/vlc-android-debug.aar` 内的 `libvlc.so` 是否能搜到新字符串。
5. `Assets/Plugins/Android/vlc-android-debug.aar` 内的 `libvlc.so` 是否能搜到新字符串。

如果第 2 步没更新，说明 native so 没重新生成或没复制到 Gradle 输入目录。如果第 3 步没更新，说明 `libvlc` AAR 没重新打包。如果第 4 或第 5 步没更新，说明 fat AAR 或 Unity plugin 复制链路没更新。

fat AAR 里常见的缓存症状是：

- `libvlcjni/libvlc/build/outputs/aar/libvlc-debug.aar` 已经是新 hash；
- `application/vlc-android/build/intermediates/exploded-aar/.../libvlc.so` 已经是新 hash；
- 但 `application/vlc-android/build/intermediates/merged_jni_libs`、`merged_native_libs` 或 `outputs/aar_rebundle` 仍是旧 hash。

这时执行：

```bash
java -classpath gradle/wrapper/gradle-wrapper.jar org.gradle.wrapper.GradleWrapperMain \
  :application:vlc-android:clean \
  :application:vlc-android:assembleDebug
```

然后重新复制到 Unity plugin，并再次比对 hash。
