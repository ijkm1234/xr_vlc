using UnityEngine;

namespace XRVLC
{
    internal readonly struct UnderlayAlphaHoleEntry
    {
        public UnderlayAlphaHoleEntry(Mesh mesh, Matrix4x4 matrix)
        {
            Mesh = mesh;
            Matrix = matrix;
        }

        public Mesh Mesh { get; }
        public Matrix4x4 Matrix { get; }
    }

    internal static class UnderlayAlphaHoleRegistry
    {
        private struct RegisteredHole
        {
            public Transform SourceTransform;
            public Mesh Mesh;
        }

        private static readonly System.Collections.Generic.List<RegisteredHole> s_Holes = new();

        public static void SetHole(Transform sourceTransform, Mesh mesh)
        {
            if (sourceTransform == null || mesh == null)
                return;

            for (int i = 0; i < s_Holes.Count; i++)
            {
                if (s_Holes[i].SourceTransform != sourceTransform)
                    continue;

                s_Holes[i] = new RegisteredHole
                {
                    SourceTransform = sourceTransform,
                    Mesh = mesh
                };
                return;
            }

            s_Holes.Add(new RegisteredHole
            {
                SourceTransform = sourceTransform,
                Mesh = mesh
            });
        }

        public static void SetFlatHole(Transform sourceTransform, Mesh sourceMesh)
        {
            SetHole(sourceTransform, sourceMesh);
        }

        public static void Disable(Transform sourceTransform = null)
        {
            if (sourceTransform == null)
            {
                s_Holes.Clear();
                return;
            }

            for (int i = s_Holes.Count - 1; i >= 0; i--)
            {
                Transform holeSource = s_Holes[i].SourceTransform;
                if (holeSource == null || holeSource == sourceTransform || holeSource.IsChildOf(sourceTransform))
                    s_Holes.RemoveAt(i);
            }
        }

        public static void CollectActiveHoles(System.Collections.Generic.List<UnderlayAlphaHoleEntry> target)
        {
            if (target == null)
                return;

            target.Clear();
            for (int i = s_Holes.Count - 1; i >= 0; i--)
            {
                RegisteredHole hole = s_Holes[i];
                if (hole.SourceTransform == null || hole.Mesh == null)
                {
                    s_Holes.RemoveAt(i);
                    continue;
                }

                if (!hole.SourceTransform.gameObject.activeInHierarchy)
                    continue;

                target.Add(new UnderlayAlphaHoleEntry(hole.Mesh, hole.SourceTransform.localToWorldMatrix));
            }
        }
    }
}
