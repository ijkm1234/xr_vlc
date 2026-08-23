# Third-Party Notices

This file identifies major third-party software and assets used by XRVLC for
PICO. The original license text, copyright headers, package metadata, and
notices supplied with each component remain controlling. This file does not
replace them or relicense third-party material.

The in-app **About > Third-party libraries** screen contains the license texts
for VLC for Android, libVLC, libvlcjni, bundled codecs, Android dependencies,
fonts, IconPark, Unity, and the PICO SDK.

## Media playback stack

| Component | Copyright / origin | License | Project source |
| --- | --- | --- | --- |
| VLC for Android | VideoLAN and VLC authors | GPL-2.0-or-later; upstream notes that included Android libraries make the distributed application GPLv3 in practice | <https://github.com/ijkm1234/vlc_android_for_xr_vlc> |
| libvlcjni | VideoLAN, VideoLabs, and VLC authors | LGPL-2.1-or-later where stated by the component and file headers | <https://github.com/ijkm1234/libvlcjni_for_xr_vlc> |
| VLC / libVLC | VideoLAN and VLC authors | GPL-2.0-or-later and LGPL-2.1-or-later, depending on the module and file | <https://github.com/ijkm1234/vlc_for_xr_vlc> |
| VLC bundled libraries and codecs | Their respective authors | GPL, LGPL, BSD, MIT, ISC, Xiph, and other licenses listed in the in-app third-party screen | Corresponding source is obtained by the VLC build scripts |

Upstream sources and license information:

- <https://code.videolan.org/videolan/vlc-android>
- <https://code.videolan.org/videolan/libvlcjni>
- <https://code.videolan.org/videolan/vlc>

## Runtime and platform SDKs

| Component | Copyright / origin | License / terms |
| --- | --- | --- |
| Unity Engine, Unity Runtime, and Unity packages | Unity Technologies | Proprietary Unity terms and package-specific licenses: <https://unity.com/legal/editor-terms-of-service/software> |
| PICO Unity Integration SDK 3.4.0 | PICO Technology Co., Ltd. | Proprietary SDK license supplied with the package and applicable PICO developer terms. The package is resolved from the [official repository](https://github.com/Pico-Developer/PICO-Unity-Integration-SDK) at commit `e0740bf309b26a0b9adae33c506514569bfe5d38`. A copy of the package notice is in [`ThirdPartyLicenses/PICO-Unity-Integration-SDK-LICENSE.md`](ThirdPartyLicenses/PICO-Unity-Integration-SDK-LICENSE.md). |

Unity and PICO components are not licensed under the project GPL notice.

## Fonts and graphical assets

| Component | Copyright / origin | License / notice |
| --- | --- | --- |
| IconPark icons | Copyright 2019-present ByteDance Inc. | Apache License 2.0; see [`ThirdPartyLicenses/Apache-2.0.txt`](ThirdPartyLicenses/Apache-2.0.txt) and <https://github.com/bytedance/IconPark> |
| Smiley Sans / 得意黑 | Copyright 2022-2024 atelierAnchor; Reserved Font Names: Smiley, 得意黑 | SIL Open Font License 1.1; see [`ThirdPartyLicenses/OFL-1.1.txt`](ThirdPartyLicenses/OFL-1.1.txt) and <https://github.com/atelier-anchor/smiley-sans> |
| Noto Sans, Noto Serif, Noto CJK, and Noto Emoji | The Noto Project authors and copyright holders identified in the font notices | SIL Open Font License 1.1; license files are stored beside the fonts under `Assets/TextMesh Pro/Fonts/Noto/` |
| Liberation Sans | Red Hat, Inc. and contributors | SIL Open Font License 1.1; `Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt` |

The XRVLC for PICO application icon is independently created artwork. It is not
the official VideoLAN VLC cone logo. VLC names and trademarks are referenced
only to identify the upstream media stack; see [`NOTICE.md`](NOTICE.md).

## Development-only dependencies

Packages used only by the Unity Editor or build tooling, including MCP for
Unity, IDE integrations, the Unity Test Framework, and collaboration tooling,
are governed by their package-specific licenses. They are not intended to be
included in the released player; the final APK/AAB dependency inventory should
be checked for each release.
