using System;
using UnityEngine;

namespace XRVLC
{
    /// <summary>
    /// 渲染表面抽象接口。
    /// 负责与底层渲染管线（如 PICO 硬件合成层、原生 MeshRenderer）对接。
    /// </summary>
    public interface IRenderSurface
    {
        /// <summary>
        /// 用于软硬解切换：要求底层屏幕彻底自我销毁并以新模式重建。
        /// proj/stereo 必须在 InitializeBuffer 前传入，否则 layerShape/layerLayout 会固化为默认值。
        /// </summary>
        void RebuildLayer(bool asHardwareSurface, uint videoWidth = 0, uint videoHeight = 0,
                          VideoProjection proj = VideoProjection.Flat, StereoMode stereo = StereoMode.Mono,
                          FlatVideoCurveMode curveMode = FlatVideoCurveMode.None,
                          bool useTextureAlphaBlending = false);

        /// <summary>
        /// 用于 VR 视频：改变底层的几何体形状和 3D 分屏模式
        /// </summary>
        void ChangeLayer(VideoProjection projection, StereoMode stereo,
                         FlatVideoCurveMode curveMode = FlatVideoCurveMode.None);

        /// <summary>
        /// 检查硬件 Surface 是否已经准备就绪
        /// </summary>
        bool IsHardwareSurfaceReady();

        /// <summary>
        /// 获取底层硬件 Surface 的 JNI 指针（用于硬解零拷贝）
        /// </summary>
        IntPtr GetHardwareSurfaceHandle();

        /// <summary>
        /// 销毁底层渲染图层
        /// </summary>
        void DestroyLayer();

        /// <summary>
        /// 接收并显示由 CPU 渲染好的纹理（用于软解内存拷贝）
        /// </summary>
        void SetSoftwareTexture(RenderTexture texture);
    }
}
