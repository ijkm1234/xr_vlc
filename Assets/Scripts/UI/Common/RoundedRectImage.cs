using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 纯代码绘制圆角矩形，不依赖任何 Sprite 资源。
/// 替换 Image 组件挂载到面板上即可。
/// </summary>
[AddComponentMenu("UI/Rounded Rect Image")]
public class RoundedRectImage : Graphic
{
    [Range(0f, 400f)]
    public float cornerRadius = 28f;

    [Range(3, 20)]
    public int cornerSegments = 10;

    [Range(0f, 24f)]
    public float borderWidth = 0f;

    public Color borderColor = Color.clear;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        float clampedBorder = Mathf.Clamp(borderWidth, 0f, Mathf.Min(rect.width, rect.height) * 0.5f);
        if (clampedBorder > 0f && borderColor.a > 0f)
        {
            AddSolidRoundedRect(vh, rect, cornerRadius, cornerSegments, borderColor);
            Rect inner = new Rect(
                rect.xMin + clampedBorder,
                rect.yMin + clampedBorder,
                rect.width - clampedBorder * 2f,
                rect.height - clampedBorder * 2f);
            AddSolidRoundedRect(vh, inner, Mathf.Max(0f, cornerRadius - clampedBorder), cornerSegments, color);
            return;
        }

        AddSolidRoundedRect(vh, rect, cornerRadius, cornerSegments, color);
    }

    private static void AddSolidRoundedRect(VertexHelper vh, Rect rect, float radius, int segments, Color32 fillColor)
    {
        if (rect.width <= 0f || rect.height <= 0f || fillColor.a == 0)
            return;

        float r = Mathf.Min(radius, rect.width * 0.5f, rect.height * 0.5f);
        if (r <= 0.01f)
        {
            AddQuad(vh, rect, fillColor);
            return;
        }

        AddQuad(vh, new Rect(rect.xMin + r, rect.yMin, rect.width - 2f * r, rect.height), fillColor);
        AddQuad(vh, new Rect(rect.xMin, rect.yMin + r, r, rect.height - 2f * r), fillColor);
        AddQuad(vh, new Rect(rect.xMax - r, rect.yMin + r, r, rect.height - 2f * r), fillColor);
        AddCorner(vh, new Vector2(rect.xMin + r, rect.yMin + r), r, 180f, segments, fillColor);
        AddCorner(vh, new Vector2(rect.xMax - r, rect.yMin + r), r, 270f, segments, fillColor);
        AddCorner(vh, new Vector2(rect.xMax - r, rect.yMax - r), r, 0f, segments, fillColor);
        AddCorner(vh, new Vector2(rect.xMin + r, rect.yMax - r), r, 90f, segments, fillColor);
    }

    private static void AddCorner(VertexHelper vh, Vector2 arcCenter, float radius, float startDeg, int segments, Color32 fillColor)
    {
        int centerIndex = AddVert(vh, arcCenter, fillColor);
        int previousIndex = -1;
        int count = Mathf.Max(1, segments);
        for (int i = 0; i <= count; i++)
        {
            float rad = (startDeg + 90f * i / count) * Mathf.Deg2Rad;
            Vector2 point = arcCenter + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
            int currentIndex = AddVert(vh, point, fillColor);
            if (previousIndex >= 0)
                vh.AddTriangle(centerIndex, previousIndex, currentIndex);
            previousIndex = currentIndex;
        }
    }

    private static void AddQuad(VertexHelper vh, Rect rect, Color32 fillColor)
    {
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        int start = vh.currentVertCount;
        AddVert(vh, new Vector2(rect.xMin, rect.yMin), fillColor);
        AddVert(vh, new Vector2(rect.xMin, rect.yMax), fillColor);
        AddVert(vh, new Vector2(rect.xMax, rect.yMax), fillColor);
        AddVert(vh, new Vector2(rect.xMax, rect.yMin), fillColor);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    private static int AddVert(VertexHelper vh, Vector2 position, Color32 fillColor)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = fillColor;
        vertex.position = position;
        vh.AddVert(vertex);
        return vh.currentVertCount - 1;
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        SetVerticesDirty();
    }
#endif
}
