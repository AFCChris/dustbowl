using System.IO;
using System.Linq;
using Dustbowl.Course;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dustbowl.Tests
{
    public sealed class CourseSurfaceTests
    {
        private const string DefinitionPath = "Assets/Dustbowl/Courses/DustbowlFlats_TabletopReference.asset";
        private const string ScenePath = "Assets/Dustbowl/Scenes/Dustbowl_BehaviourLab.unity";

        [Test]
        public void TaggedWebReferenceHeightsRemainWithinFloatTolerance()
        {
            CourseSurface surface = CreateTemporarySurface(out GameObject host);
            try
            {
                Assert.That(surface.SampleHeight(new Vector3(263.612136f, 0f, -39.842854f)),
                    Is.EqualTo(-22.015057f).Within(0.025f));
                Assert.That(surface.SampleHeight(new Vector3(257.486854f, 0f, -0.679860f)),
                    Is.EqualTo(-23.322540f).Within(0.025f));
                Assert.That(surface.SampleHeight(new Vector3(256.886998f, 0f, 1.948194f)),
                    Is.EqualTo(-22.615634f).Within(0.025f));
                Assert.That(surface.SampleHeight(new Vector3(255.625154f, 0f, 7.281729f)),
                    Is.EqualTo(-20.243986f).Within(0.025f));
                Assert.That(surface.SampleHeight(new Vector3(248.318839f, 0f, 34.688055f)),
                    Is.EqualTo(-26.548943f).Within(0.025f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SampledNormalsAreNormalized()
        {
            CourseSurface surface = CreateTemporarySurface(out GameObject host);
            try
            {
                CourseDefinition definition = surface.Definition;
                for (float along = definition.StartAlong; along <= definition.EndAlong; along += 7f)
                {
                    Assert.That(definition.TrySampleLine(along, out CourseLinePoint line), Is.True);
                    Vector3 normal = surface.SampleNormal(line.center);
                    Assert.That(normal.magnitude, Is.EqualTo(1f).Within(0.00001f));
                    Assert.That(normal.y, Is.GreaterThan(0f));
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AlongAndSignedLateralSamplesAreConsistent()
        {
            CourseSurface surface = CreateTemporarySurface(out GameObject host);
            try
            {
                Assert.That(surface.Definition.TrySampleLine(390f, out CourseLinePoint line), Is.True);
                CourseSample center = surface.SampleCourse(line.center);
                Assert.That(center.along, Is.EqualTo(390f).Within(0.02f));
                Assert.That(center.signedLateralDistance, Is.EqualTo(0f).Within(0.001f));

                Vector3 horizontalRight = new(center.frame.right.x, 0f, center.frame.right.z);
                horizontalRight.Normalize();
                CourseSample right = surface.SampleCourse(line.center + horizontalRight * 6f);
                CourseSample left = surface.SampleCourse(line.center - horizontalRight * 6f);
                Assert.That(right.signedLateralDistance, Is.EqualTo(6f).Within(0.04f));
                Assert.That(left.signedLateralDistance, Is.EqualTo(-6f).Within(0.04f));
                Assert.That(right.along, Is.EqualTo(center.along).Within(0.04f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SurfaceBandsFollowTheSharedWidthContract()
        {
            CourseSurface surface = CreateTemporarySurface(out GameObject host);
            try
            {
                surface.Definition.TrySampleLine(390f, out CourseLinePoint line);
                CourseSample center = surface.SampleCourse(line.center);
                Vector3 right = new(center.frame.right.x, 0f, center.frame.right.z);
                right.Normalize();

                Assert.That(surface.SampleGround(line.center).surfaceType, Is.EqualTo(SurfaceType.PackedTrack));
                Assert.That(surface.SampleGround(line.center + right * 11f).surfaceType, Is.EqualTo(SurfaceType.Berm));
                Assert.That(surface.SampleGround(line.center + right * 21f).surfaceType, Is.EqualTo(SurfaceType.LooseSand));
                Assert.That(surface.SampleGround(line.center + right * 30f).surfaceType, Is.EqualTo(SurfaceType.NaturalSand));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void TabletopFeatureMetadataIsQueryable()
        {
            CourseDefinition definition = LoadDefinition();
            Assert.That(definition.TryGetFeature(390f, 0f, out CourseFeature feature), Is.True);
            Assert.That(feature.id, Is.EqualTo("flats-section-4-tabletop"));
            Assert.That(feature.kind, Is.EqualTo(CourseFeatureKind.Tabletop));
            Assert.That(feature.startAlong, Is.EqualTo(373.722349f).Within(0.0001f));
            Assert.That(feature.length, Is.EqualTo(36f));
            Assert.That(feature.height, Is.EqualTo(3.6f));
        }

        [Test]
        public void MeshGenerationIsDeterministic()
        {
            CourseSurface surface = CreateTemporarySurface(out GameObject host);
            Mesh first = null;
            Mesh second = null;
            try
            {
                first = CourseSurfaceMeshBuilder.Build(surface);
                second = CourseSurfaceMeshBuilder.Build(surface);
                Assert.That(first.vertexCount, Is.EqualTo(second.vertexCount));
                Assert.That(first.triangles, Is.EqualTo(second.triangles));
                Assert.That(CourseSurfaceMeshBuilder.DeterministicSignature(first),
                    Is.EqualTo(CourseSurfaceMeshBuilder.DeterministicSignature(second)));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RenderAndColliderUseOneMeshBuiltFromTheSamplingAuthority()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            CourseSurface surface = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CourseSurface>(true))
                .Single();
            Mesh mesh = surface.GeneratedMesh;

            Assert.That(surface.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
            Assert.That(surface.GetComponent<MeshCollider>().sharedMesh, Is.SameAs(mesh));
            Vector3[] vertices = mesh.vertices;
            for (int index = 0; index < vertices.Length; index += 97)
            {
                Assert.That(vertices[index].y, Is.EqualTo(surface.SampleHeight(vertices[index])).Within(0.0001f));
            }
        }

        [Test]
        public void BehaviourLabContainsNoVehiclePhysicsFramework()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WheelCollider>(true)),
                Is.Empty);
            Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Rigidbody>(true)),
                Is.Empty);

            string manifest = File.ReadAllText("Packages/manifest.json");
            StringAssert.DoesNotContain("com.unity.modules.vehicles", manifest);
        }

        private static CourseSurface CreateTemporarySurface(out GameObject host)
        {
            host = new GameObject("TemporaryCourseSurfaceTest");
            CourseSurface surface = host.AddComponent<CourseSurface>();
            surface.Configure(LoadDefinition(), null);
            return surface;
        }

        private static CourseDefinition LoadDefinition()
        {
            CourseDefinition definition = AssetDatabase.LoadAssetAtPath<CourseDefinition>(DefinitionPath);
            Assert.That(definition, Is.Not.Null);
            return definition;
        }
    }
}
