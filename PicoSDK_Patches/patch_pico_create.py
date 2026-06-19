import sys

file_path = '/Users/admin/Downloads/PICO Unity Integration SDK-3.4.0-20260226/Runtime/Scripts/Features/PXR_CompositionLayer.cs'

with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

old_str = """        public void CreateExternalSurface(PXR_CompositionLayer overlayInstance)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (IntPtr.Zero != overlayInstance.externalAndroidSurfaceObject)
            {
                return;
            }

            PXR_Plugin.Render.UPxr_GetLayerAndroidSurface(overlayInstance.overlayIndex, 0, ref overlayInstance.externalAndroidSurfaceObject);
            PLog.i(TAG, string.Format("CreateExternalSurface: Overlay Type:{0}, LayerDepth:{1}, SurfaceObject:{2}", overlayInstance.overlayType, overlayInstance.overlayIndex, overlayInstance.externalAndroidSurfaceObject));
            
            if (IntPtr.Zero == overlayInstance.externalAndroidSurfaceObject || null == overlayInstance.externalAndroidSurfaceObjectCreated)
            {
                return;
            }
            
            overlayInstance.externalAndroidSurfaceObjectCreated();
#endif
        }"""

new_str = """        public void CreateExternalSurface(PXR_CompositionLayer overlayInstance)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (IntPtr.Zero != overlayInstance.externalAndroidSurfaceObject)
            {
                return;
            }

            // [PICO_MOD] Force layer creation BEFORE getting the surface so PICO doesn't lazy-init a 1024x1024 surface.
            PLog.i(TAG, $"[PICO_MOD] Forcing UPxr_CreateLayerParam before UPxr_GetLayerAndroidSurface. Size: {overlayInstance.overlayParam.width}x{overlayInstance.overlayParam.height}");
            PXR_Plugin.Render.UPxr_CreateLayerParam(overlayInstance.overlayParam);

            PXR_Plugin.Render.UPxr_GetLayerAndroidSurface(overlayInstance.overlayIndex, 0, ref overlayInstance.externalAndroidSurfaceObject);
            PLog.i(TAG, string.Format("CreateExternalSurface: Overlay Type:{0}, LayerDepth:{1}, SurfaceObject:{2}", overlayInstance.overlayType, overlayInstance.overlayIndex, overlayInstance.externalAndroidSurfaceObject));
            
            if (IntPtr.Zero == overlayInstance.externalAndroidSurfaceObject || null == overlayInstance.externalAndroidSurfaceObjectCreated)
            {
                return;
            }
            
            overlayInstance.externalAndroidSurfaceObjectCreated();
#endif
        }"""

if old_str in content:
    content = content.replace(old_str, new_str)
    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(content)
    print("Patch applied successfully.")
else:
    print("Old string not found in the file. Maybe already patched?")
