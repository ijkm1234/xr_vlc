using System;
using NUnit.Framework;
using UnityEngine;

namespace XRVLC.Tests
{
    [TestFixture]
    public class VideoScreenTransformServiceTests
    {
        [Test]
        public void Constructor_KeepsAuthoredDefaultScreenPosition()
        {
            using var fixture = new TransformFixture();

            object service = CreateService(fixture);

            Assert.IsNotNull(service);
            AssertVector3(new Vector3(0f, 2.5f, 15f), fixture.ScreenRoot.transform.position);
        }

        [Test]
        public void AddViewerDistanceOffset_FlatProjectionZoomsAndKeepsDefaultPosition()
        {
            using var fixture = new TransformFixture();
            float zoomDelta = 0f;
            object service = CreateService(fixture, () => VideoProjection.Flat, delta => zoomDelta += delta);

            Invoke(service, "AddViewerDistanceOffset", 25f);

            Assert.That(zoomDelta, Is.EqualTo(25f).Within(0.001f));
            AssertVector3(new Vector3(0f, 2.5f, 15f), fixture.ScreenRoot.transform.position);
        }

        [Test]
        public void UpdateControllerMove_FlatProjectionOrbitsScreenAroundViewerRayDirection()
        {
            using var fixture = new TransformFixture();
            float zoomDelta = 0f;
            object service = CreateService(fixture, () => VideoProjection.Flat, delta => zoomDelta += delta);

            Invoke(service, "BeginControllerMove", Vector3.forward);
            Invoke(service, "UpdateControllerMove", Vector3.right);

            Assert.That(zoomDelta, Is.EqualTo(0f).Within(0.001f));
            AssertVector3(new Vector3(15f, 2.5f, 0f), fixture.ScreenRoot.transform.position);
            AssertVector3(new Vector3(15f, 1f, 0f).normalized, fixture.ScreenRoot.transform.forward);
        }

        [Test]
        public void ResetToDefault_RestoresAuthoredPosition()
        {
            using var fixture = new TransformFixture();
            object service = CreateService(fixture);

            Invoke(service, "BeginControllerMove", Vector3.forward);
            Invoke(service, "UpdateControllerMove", Vector3.right);
            Invoke(service, "ResetToDefault");

            AssertVector3(new Vector3(0f, 2.5f, 15f), fixture.ScreenRoot.transform.position);
        }

        [Test]
        public void Sphere360ProjectionIgnoresDistanceAndControllerMove()
        {
            using var fixture = new TransformFixture();
            float zoomDelta = 0f;
            object service = CreateService(fixture, () => VideoProjection.Sphere360, delta => zoomDelta += delta);

            Invoke(service, "AddViewerDistanceOffset", -5f);
            Invoke(service, "BeginControllerMove", Vector3.zero);
            Invoke(service, "UpdateControllerMove", new Vector3(0f, 0f, -5f));

            Assert.That(zoomDelta, Is.EqualTo(0f).Within(0.001f));
            AssertVector3(new Vector3(0f, 2.5f, 15f), fixture.ScreenRoot.transform.position);
            AssertVector3(new Vector3(0f, 2.5f, 15f), fixture.VideoAnchor.transform.position);
        }

        [Test]
        public void ReturningToFlatProjection_DoesNotMoveAuthoredPosition()
        {
            using var fixture = new TransformFixture();
            VideoProjection projection = VideoProjection.Flat;
            object service = CreateService(fixture, () => projection);

            projection = VideoProjection.Sphere360;
            Invoke(service, "AddViewerDistanceOffset", 1f);
            projection = VideoProjection.Flat;
            Invoke(service, "AddViewerDistanceOffset", 0f);

            AssertVector3(new Vector3(0f, 2.5f, 15f), fixture.ScreenRoot.transform.position);
        }

        [Test]
        public void VideoScreen_DoesNotExposeDefaultDistanceOffset()
        {
            string source = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "Scripts/Services/Screen/VideoScreen.cs"));
            string transformSource = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "Scripts/Services/Screen/VideoScreenTransformService.cs"));

            StringAssert.DoesNotContain("defaultDistanceOffsetMeters", source);
            StringAssert.DoesNotContain("DefaultDistanceOffsetMeters", transformSource);
        }

        private static object CreateService(TransformFixture fixture)
        {
            return CreateService(fixture, () => VideoProjection.Flat);
        }

        private static object CreateService(
            TransformFixture fixture,
            Func<VideoProjection> projectionProvider,
            Action<float> flatZoomDeltaHandler = null)
        {
            Type type = Type.GetType("XRVLC.VideoScreenTransformService, Assembly-CSharp");
            Assert.IsNotNull(type, "VideoScreenTransformService should be available in Assembly-CSharp.");

            Func<Transform> viewerProvider = () => fixture.Viewer.transform;

            return Activator.CreateInstance(
                type,
                fixture.ScreenRoot.transform,
                fixture.VideoAnchor.transform,
                viewerProvider,
                projectionProvider,
                flatZoomDeltaHandler);
        }

        private static void Invoke(object target, string methodName, params object[] args)
        {
            Type type = target.GetType();
            type.GetMethod(methodName).Invoke(target, args);
        }

        private static void AssertVector3(Vector3 expected, Vector3 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.001f));
        }

        private sealed class TransformFixture : IDisposable
        {
            public readonly GameObject ScreenRoot = new GameObject("VideoScreen");
            public readonly GameObject VideoAnchor = new GameObject("VideoAnchor");
            public readonly GameObject Viewer = new GameObject("Viewer");

            public TransformFixture()
            {
                ScreenRoot.transform.position = new Vector3(0f, 2.5f, 15f);
                ScreenRoot.transform.rotation = Quaternion.identity;

                VideoAnchor.transform.SetParent(ScreenRoot.transform, false);

                Viewer.transform.position = new Vector3(0f, 1.5f, 0f);
                Viewer.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Viewer);
                UnityEngine.Object.DestroyImmediate(ScreenRoot);
            }
        }
    }
}
