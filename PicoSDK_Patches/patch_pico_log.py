import sys

file_path = '/Users/admin/Downloads/PICO Unity Integration SDK-3.4.0-20260226/Runtime/Scripts/Features/PXR_CompositionLayer.cs'

with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

old_str = """            else
            {
                if (layerTextures[0] != null)
                {
                    overlayParam.width = (uint)layerTextures[0].width;
                    overlayParam.height = (uint)layerTextures[0].height;
                }
                else if (layerTextures[1] != null)
                {
                    overlayParam.width = (uint)layerTextures[1].width;
                    overlayParam.height = (uint)layerTextures[1].height;
                }
                else
                {
                    overlayParam.width = 1024;
                    overlayParam.height = 1024;
                }
            }"""

new_str = """            else
            {
                if (layerTextures[0] != null)
                {
                    overlayParam.width = (uint)layerTextures[0].width;
                    overlayParam.height = (uint)layerTextures[0].height;
                    PLog.i(TAG, $"[PICO_MOD] Set ExternalSurface resolution from layerTextures[0]: {overlayParam.width}x{overlayParam.height}");
                }
                else if (layerTextures[1] != null)
                {
                    overlayParam.width = (uint)layerTextures[1].width;
                    overlayParam.height = (uint)layerTextures[1].height;
                    PLog.i(TAG, $"[PICO_MOD] Set ExternalSurface resolution from layerTextures[1]: {overlayParam.width}x{overlayParam.height}");
                }
                else
                {
                    overlayParam.width = 1024;
                    overlayParam.height = 1024;
                    PLog.i(TAG, "[PICO_MOD] Set ExternalSurface resolution to fallback 1024x1024");
                }
            }"""

if old_str in content:
    content = content.replace(old_str, new_str)
    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(content)
    print("Patch applied successfully.")
else:
    print("Old string not found in the file. Maybe already patched?")
