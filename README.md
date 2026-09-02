# XRVLC for PICO

XRVLC for PICO 是一款面向 PICO XR 设备的开源媒体播放器。项目使用
Unity 构建 XR 交互层，并集成经过修改的 VLC for Android、libvlcjni 和
VLC/libVLC 媒体栈。

本项目独立开发，与 VideoLAN 没有隶属、赞助或背书关系。

## Source repositories

完整源码分布在以下仓库中：

| Component | Repository |
| --- | --- |
| Unity host | <https://github.com/ijkm1234/xr_vlc> |
| VLC for Android fork | <https://github.com/ijkm1234/vlc_android_for_xr_vlc> |
| libvlcjni fork | <https://github.com/ijkm1234/libvlcjni_for_xr_vlc> |
| VLC/libVLC fork | <https://github.com/ijkm1234/vlc_for_xr_vlc> |

当前 Unity 构建脚本固定使用以下 VLC 依赖：

| Dependency | Pinned version |
| --- | --- |
| VLC for Android fork | tag `v0.0.1` (`485e54231bc178fa0af52fad10bb001738f64f7b`) |
| libvlcjni fork | tag `v0.0.1` (`1e0f2fa5114700381e61e90d796f7edf86a733da`) |
| VLC/libVLC fork | tag `v0.0.1` (`fef678a7fcf79f717fd909a43cf737f415f4fa9e`) |

顶层脚本通过公开 HTTPS 仓库解析并校验 VLC for Android 的标签和提交；
该仓库的构建脚本再以相同方式校验 libvlcjni 与 VLC/libVLC。

## Build and setup

项目使用 Unity `6000.4.1f1`。首次打开 Unity 工程前，准备固定版本的
PICO Unity Integration SDK 3.4.0：

```bash
export PICO_UNITY_SDK_DIR="$PWD/.local-dependencies/PICO-Unity-Integration-SDK"
./scripts/setup-pico-unity-sdk.sh
```

该脚本从 PICO 官方公开仓库获取固定提交、应用项目补丁，并创建 Unity
package 所需的项目内相对链接。PICO SDK 源码和本地链接均不提交到 Git。

在 macOS 上准备 Java 17、Android SDK 34 和 NDK `27.3.13750724`：

```bash
./setup_android_build.sh
```

从公开源码构建 arm64-v8a VLC Android AAR，并复制到 Unity plugin 目录：

```bash
./scripts/build-vlc-android-aar.sh
```

正式 APK 使用 release variant AAR：

```bash
VLC_ANDROID_AAR_VARIANT=release ./scripts/build-vlc-android-aar.sh
```

命令行构建正式签名 APK 或 AAB 时，通过环境变量提供本地 JKS 信息：

```bash
export XRVLC_KEYSTORE_PATH="/absolute/path/to/xrvlc-release.jks"
export XRVLC_KEYSTORE_PASS="<keystore-password>"
export XRVLC_KEY_ALIAS="xrvlc-release"
export XRVLC_KEY_ALIAS_PASS="<alias-password>"

Unity \
  -batchmode -quit \
  -buildTarget Android \
  -projectPath . \
  -executeMethod CommandLineAndroidBuild.BuildRelease \
  -outputPath Builds/xr_vlc-release.apk \
  -logFile -
```

执行前确保项目所需版本的 `Unity` 命令已加入 `PATH`。
`-buildTarget Android` 必须在 Unity 启动时传入，尤其是清空 `Library` 后；否则
PICO 的 Android Manifest 后处理可能不会参与本次构建，导致 APK 缺少 PICO SDK
元数据并在启动时无法创建 Composition Layer。
将输出扩展名改为 `.aab` 即构建 Android App Bundle。正式构建要求插件目录中
只存在 `vlc-android-release.aar`；AAR 构建脚本会自动移除另一 variant。

若同级目录中不存在 `vlc-android`，脚本会从公开 HTTPS 仓库克隆并切换到
固定标签；如果目录已存在，脚本不会自动 pull 或 reset，而会校验标签、提交和
tracked 文件状态。只有明确需要编译本地开发源码时才使用：

```bash
VLC_ANDROID_USE_LOCAL=1 ./scripts/build-vlc-android-aar.sh
```

最终产物位于 `Assets/Plugins/Android/vlc-android-<variant>.aar`。AAR、APK、
下载的依赖源码和构建中间产物均由 `.gitignore` 排除。

## License

项目拥有版权且有权许可的原创源码按
[GNU GPL v2 or later](LICENSE) 提供。VLC、VLC for Android、libvlcjni、
Unity、PICO SDK、字体、图标及其他第三方组件继续适用各自的许可证或
服务条款，不因本仓库的 GPL 声明而被重新许可。

发布二进制时，应提供与该二进制精确对应的源码和构建说明，并同时保留
相关版权、许可证及第三方声明。详细信息见 [NOTICE.md](NOTICE.md) 和
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

XRVLC for PICO 使用独立创作的应用图标，不分发 VideoLAN 官方 VLC 圆锥
标志。VLC、VLC media player 及 VLC 圆锥标志是 VideoLAN 的商标。
