using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Cart315.FirstPerson.Editor
{
    public static class ControllerTools
    {
        public const string Root = "Assets/CART315FirstPerson";
        public const string PrefabPath = Root + "/Prefabs/CART315 First Person.prefab";
        public const string ScenePath = Root + "/Demo/Controller Playground.unity";

        // Maintainer-only CLI entry point for regenerating the authored demo in a disposable copy.
        public static void RecreateGeneratedAssets()
        {
            CreatePrefab();
            CreateDemo();
            Prepare();
        }

        public static void PackageAndBuild()
        {
            Prepare();
            CapturePreview();
            BuildWindows();
        }

        public static void CapturePreview()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            var target = new RenderTexture(1280, 720, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Deliverables");
            File.WriteAllBytes("Deliverables/Playground.png", image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
        }

        // CLI entry point. Creates only missing assets; never overwrites student edits.
        public static void Prepare()
        {
            Directory.CreateDirectory(Root + "/Prefabs");
            Directory.CreateDirectory(Root + "/Demo");
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) CreatePrefab();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) CreateDemo();
            AssetDatabase.SaveAssets();
            Validate();
            Export();
        }

        [MenuItem("CART315/Open Controller Playground")]
        public static void OpenDemo()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("CART315/Add First Person Player")]
        public static void AddPlayer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(player, "Add First Person Player");
            Selection.activeGameObject = player;
        }

        private static void CreatePrefab()
        {
            var player = new GameObject("CART315 First Person");
            player.SetActive(false);
            player.layer = 2; // Built-in Ignore Raycast; collides normally, excludes our ground probes.
            var capsule = player.AddComponent<CharacterController>();
            capsule.height = 1.8f;
            capsule.radius = 0.3f;
            capsule.center = new Vector3(0, 0.9f, 0);
            capsule.slopeLimit = 45;
            capsule.stepOffset = 0.3f;
            capsule.skinWidth = 0.03f;
            capsule.minMoveDistance = 0;
            var input = player.AddComponent<FPInputManager>();
            var inputSettings = new SerializedObject(input);
            inputSettings.FindProperty("_actions").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(Root + "/Input/CART315Controls.inputactions");
            inputSettings.ApplyModifiedPropertiesWithoutUndo();
            var controller = player.AddComponent<FPController>();
            var pivot = new GameObject("Camera Pivot").transform;
            pivot.SetParent(player.transform, false);
            pivot.localPosition = new Vector3(0, 1.6f, 0);
            pivot.gameObject.layer = 2;
            var cameraObject = new GameObject("Player Camera");
            cameraObject.transform.SetParent(pivot, false);
            cameraObject.layer = 2;
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 200;
            camera.fieldOfView = 70;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.045f, 0.065f, 0.1f);
            cameraObject.AddComponent<AudioListener>();
            var settings = new SerializedObject(controller);
            settings.FindProperty("_cameraHolderTransform").objectReferenceValue = pivot;
            settings.FindProperty("_mouseSensitivity").floatValue = 0.1f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            player.AddComponent<FPSprinting>();
            player.AddComponent<FPJumping>();
            player.AddComponent<FPHeadBobbing>();
            var audio = player.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0;
            player.AddComponent<FPFootsteps>();
            player.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(player, PrefabPath);
            UnityEngine.Object.DestroyImmediate(player);
        }

        private static Material Material(string name, Color color)
        {
            string path = Root + "/Demo/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("CART315/Demo Grid"));
            material.SetColor("_Color", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void CreateDemo()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var floor = Material("Slate", new Color(0.15f, 0.21f, 0.28f));
            var amber = Material("Amber", new Color(0.85f, 0.42f, 0.12f));
            var blue = Material("Blue", new Color(0.12f, 0.48f, 0.67f));
            var light = Material("Stone", new Color(0.42f, 0.52f, 0.56f));
            Box("Ground — 24 x 24 metres", new Vector3(0, -0.25f, 0), new Vector3(24, 0.5f, 24), floor);
            Box("North boundary", new Vector3(0, 1.5f, 12), new Vector3(24, 3, 0.4f), floor);
            Box("South boundary", new Vector3(0, 1.5f, -12), new Vector3(24, 3, 0.4f), floor);
            Box("East boundary", new Vector3(12, 1.5f, 0), new Vector3(0.4f, 3, 24), floor);
            Box("West boundary", new Vector3(-12, 1.5f, 0), new Vector3(0.4f, 3, 24), floor);
            for (int i = 0; i < 6; i++)
            {
                float height = (i + 1) * 0.2f;
                Box("Stair " + (i + 1) + " — 20 cm", new Vector3(-5, height / 2, -1 + i * 0.7f),
                    new Vector3(2.8f, height, 0.7f), amber);
            }
            Box("Stair landing", new Vector3(-5, 0.6f, 4), new Vector3(2.8f, 1.2f, 3), amber);
            var ramp = Box("Walkable ramp — 20 degrees", new Vector3(1, 0.9f, 2), new Vector3(2.8f, 0.25f, 6), blue);
            ramp.transform.rotation = Quaternion.Euler(-20, 0, 0);
            var steep = Box("Steep ramp — 60 degrees", new Vector3(6, 1.5f, 4), new Vector3(2.8f, 0.25f, 4), light);
            steep.transform.rotation = Quaternion.Euler(-60, 0, 0);
            Box("Low ceiling — jump underneath", new Vector3(5, 2.2f, -3), new Vector3(3, 0.3f, 3), blue);
            Box("Ceiling support left", new Vector3(3.6f, 1, -3), new Vector3(0.2f, 2, 3), light);
            Box("Ceiling support right", new Vector3(6.4f, 1, -3), new Vector3(0.2f, 2, 3), light);
            Box("Wall — movement feedback should stop", new Vector3(-5, 1.2f, -5), new Vector3(3, 2.4f, 0.4f), light);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            player.transform.position = new Vector3(0, 0.15f, -8);
            new GameObject("Playground Instructions").AddComponent<PlaygroundInstructions>();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static GameObject Box(string name, Vector3 position, Vector3 size, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.position = position;
            obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            return obj;
        }

        [MenuItem("CART315/Validate Controller Assets")]
        public static void Validate()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (player == null || player.GetComponent<FPController>() == null) throw new Exception("Player prefab missing.");
            if (player.GetComponentsInChildren<Camera>(true).Length != 1) throw new Exception("Expected one camera.");
            if (player.GetComponentsInChildren<AudioListener>(true).Length != 1) throw new Exception("Expected one audio listener.");
            foreach (var t in player.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) != 0) throw new Exception("Missing script.");
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(Root + "/Input/CART315Controls.inputactions");
            foreach (string action in new[] { "Move", "Look", "Jump", "Sprint", "CaptureCursor", "ReleaseCursor" })
                if (input.FindAction("Player/" + action) == null) throw new Exception("Missing action " + action);
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Root + "/Demo" }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material.shader == null || material.shader.name != "CART315/Demo Grid")
                    throw new Exception("Demo material has a missing or incorrect shader: " + material.name);
            }
            Debug.Log("CART315 asset validation passed.");
        }

        [MenuItem("CART315/Export Student Package")]
        public static void Export()
        {
            Directory.CreateDirectory("Deliverables");
            // Tests stay in the instructor project; students need no Test Framework dependency.
            var paths = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith(Root + "/", StringComparison.Ordinal)
                && !AssetDatabase.IsValidFolder(p) && !p.StartsWith(Root + "/Tests/", StringComparison.Ordinal)).ToArray();
            AssetDatabase.ExportPackage(paths, "Deliverables/CART315-FirstPerson.unitypackage", ExportPackageOptions.Default);
            Debug.Log("CART315 student package exported.");
        }

        public static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64, "Builds/Windows/CART315 Controller.exe");
        }

        public static void BuildWebGL()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new Exception("Install WebGL Build Support for Unity 6000.3.23f1 in Unity Hub.");
            // Uncompressed output works on a simple local server and on classroom hosts.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            Build(BuildTarget.WebGL, "Builds/WebGL");
        }

        private static void Build(BuildTarget target, string location)
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = location,
                target = target, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Build failed: " + report.summary.result);
            Debug.Log("CART315 build passed: " + target);
        }
    }
}
