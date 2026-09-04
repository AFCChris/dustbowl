using System;
using System.Linq;
using Dustbowl.Course;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dustbowl.Editor
{
    public static class Stage2CourseSetup
    {
        private const string Root = "Assets/Dustbowl";
        private const string ScenePath = Root + "/Scenes/Dustbowl_BehaviourLab.unity";
        private const string DefinitionPath = Root + "/Courses/DustbowlFlats_TabletopReference.asset";
        private const string MeshPath = Root + "/Courses/DustbowlFlats_TabletopReferenceMesh.asset";
        private const string MaterialPath = Root + "/Materials/BehaviourLabGround.mat";

        [MenuItem("Dustbowl/Stage 2/Configure course terrain contract")]
        public static void Configure()
        {
            CourseDefinition definition = CreateDefinition();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject root = scene.GetRootGameObjects().Single(gameObject => gameObject.name == "Dustbowl_BehaviourLab");

            DestroyNamed(root.transform, "GroundReference");
            DestroyNamed(root.transform, "DustbowlFlats_ReferenceSegment");
            DestroyNamed(root.transform, "SurfaceProbe_DevelopmentOnly");

            var surfaceObject = new GameObject("DustbowlFlats_ReferenceSegment");
            surfaceObject.transform.SetParent(root.transform, false);
            CourseSurface surface = surfaceObject.AddComponent<CourseSurface>();
            surface.Configure(definition, null);
            Mesh persistentMesh = CreateOrReplaceMesh(CourseSurfaceMeshBuilder.Build(surface));
            surface.Configure(definition, persistentMesh);
            surfaceObject.GetComponent<MeshRenderer>().sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            surfaceObject.AddComponent<CourseSurfaceDebugGizmos>().Configure(surface);

            PositionReferenceObjects(root.transform, surface);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Stage1ProjectSetup.Verify();
            Verify();
            Debug.Log("Dustbowl Stage 2 course/terrain contract configured and verified.");
        }

        [MenuItem("Dustbowl/Stage 2/Verify course terrain contract")]
        public static void Verify()
        {
            CourseDefinition definition = AssetDatabase.LoadAssetAtPath<CourseDefinition>(DefinitionPath);
            if (definition == null || definition.CourseId != DustbowlFlatsReferenceSegment.CourseId)
            {
                throw new InvalidOperationException("The Stage 2 authoritative course definition is missing.");
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            CourseSurface surface = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CourseSurface>(true))
                .SingleOrDefault(candidate =>
                    candidate.SurfaceModel == CourseSurfaceModel.DustbowlFlatsReference);
            if (surface == null || surface.Definition != definition || surface.GeneratedMesh == null)
            {
                throw new InvalidOperationException("The behaviour lab course surface is incomplete.");
            }

            MeshFilter filter = surface.GetComponent<MeshFilter>();
            MeshCollider collider = surface.GetComponent<MeshCollider>();
            if (filter.sharedMesh != surface.GeneratedMesh || collider.sharedMesh != surface.GeneratedMesh)
            {
                throw new InvalidOperationException("Rendering and collision must share the generated authoritative mesh.");
            }

            Transform rootTransform = surface.transform.parent;
            Transform anchors = FindDirectChild(rootTransform, "CameraModeAnchors");
            string[] requiredAnchors = { "Chase", "Close", "Overhead" };
            if (anchors == null || requiredAnchors.Any(name => FindDirectChild(anchors, name) == null))
            {
                throw new InvalidOperationException("Chase, close and overhead camera anchors must remain first-class.");
            }

            if (FindDirectChild(rootTransform, "SurfaceProbe_DevelopmentOnly") == null)
            {
                throw new InvalidOperationException("The development-only surface probe is missing.");
            }

            if (scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WheelCollider>(true)).Any())
            {
                throw new InvalidOperationException("Stage 2 must not contain WheelColliders.");
            }

            if (scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Rigidbody>(true)).Any())
            {
                throw new InvalidOperationException("Stage 2 must not contain a bike physics body.");
            }

            VerifyTaggedReferenceSamples(surface);
            Debug.Log("Dustbowl Stage 2 verification passed.");
        }

        private static CourseDefinition CreateDefinition()
        {
            CourseDefinition definition = AssetDatabase.LoadAssetAtPath<CourseDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<CourseDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            DustbowlFlatsReferenceSegment.Populate(definition);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static Mesh CreateOrReplaceMesh(Mesh generated)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, MeshPath);
                return generated;
            }

            EditorUtility.CopySerialized(generated, existing);
            UnityEngine.Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void PositionReferenceObjects(Transform root, CourseSurface surface)
        {
            if (!surface.Definition.TrySampleLine(390f, out CourseLinePoint middleLine))
            {
                throw new InvalidOperationException("Could not sample the representative course segment.");
            }

            CourseSample middle = surface.SampleCourse(middleLine.center);
            Vector3 middlePoint = middle.frame.center;

            var probeObject = new GameObject("SurfaceProbe_DevelopmentOnly");
            probeObject.transform.SetParent(root, false);
            probeObject.transform.position = middlePoint + Vector3.up * 5f;
            probeObject.AddComponent<CourseSurfaceProbe>().Configure(surface);

            Transform bikeMount = FindDirectChild(root, "FutureBikeMount_NoController");
            if (bikeMount != null && surface.Definition.TrySampleLine(342f, out CourseLinePoint entryLine))
            {
                CourseSample entry = surface.SampleCourse(entryLine.center);
                bikeMount.position = entry.frame.center + entry.frame.surfaceNormal * 0.55f;
                bikeMount.rotation = Quaternion.LookRotation(entry.frame.forward, entry.frame.surfaceNormal);
            }

            Transform anchors = FindDirectChild(root, "CameraModeAnchors");
            if (anchors != null)
            {
                PositionAnchor(anchors, "Chase", middlePoint + new Vector3(8f, 5f, -10f), middlePoint);
                PositionAnchor(anchors, "Close", middlePoint + new Vector3(4f, 2.5f, -5f), middlePoint);
                PositionAnchor(anchors, "Overhead", middlePoint + new Vector3(0f, 28f, 0f), middlePoint);
            }

            Transform cameraTransform = FindDirectChild(root, "Main Camera");
            if (cameraTransform != null)
            {
                cameraTransform.position = middlePoint + new Vector3(34f, 24f, -38f);
                cameraTransform.LookAt(middlePoint);
            }
        }

        private static void PositionAnchor(Transform parent, string name, Vector3 position, Vector3 target)
        {
            Transform anchor = FindDirectChild(parent, name);
            if (anchor == null)
            {
                return;
            }

            anchor.position = position;
            anchor.LookAt(target);
        }

        private static void VerifyTaggedReferenceSamples(CourseSurface surface)
        {
            (Vector3 position, float expectedHeight)[] samples =
            {
                (new Vector3(263.612136f, 0f, -39.842854f), -22.015057f),
                (new Vector3(257.486854f, 0f, -0.679860f), -23.322540f),
                (new Vector3(256.886998f, 0f, 1.948194f), -22.615634f),
                (new Vector3(255.625154f, 0f, 7.281729f), -20.243986f),
                (new Vector3(248.318839f, 0f, 34.688055f), -26.548943f)
            };

            foreach ((Vector3 position, float expectedHeight) in samples)
            {
                float actual = surface.SampleHeight(position);
                if (Mathf.Abs(actual - expectedHeight) > 0.025f)
                {
                    throw new InvalidOperationException(
                        $"Tagged web height mismatch at {position}: expected {expectedHeight:F6}, got {actual:F6}.");
                }
            }
        }

        private static void DestroyNamed(Transform parent, string name)
        {
            Transform existing = FindDirectChild(parent, name);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
