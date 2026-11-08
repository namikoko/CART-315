using System.Collections.Generic;
using System.IO;
using Cart315.FirstPerson;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PinkBackrooms.EditorTools
{
    /// Builds the whole bedroom out of Unity primitives in one click.
    /// Everything it makes is a normal GameObject, so the scene can be rearranged by hand afterwards.
    /// Running it again rebuilds the scene from scratch (hand edits to Bedroom.unity would be lost).
    [InitializeOnLoad]
    public static class BedroomBuilder
    {
        const string Root = LookSetup.Root;
        const string ScenePath = Root + "/Scenes/Bedroom.unity";
        const string MatDir = Root + "/Materials";

        // Room size in metres. x: west(-) to east(+), z: south(-) to north(+).
        const float W = 5f, D = 4.5f, H = 2.7f;
        const float HX = W / 2f, HZ = D / 2f;
        const float Band = 1.0f; // height of the dirty lower wallpaper band / chair rail

        static Dictionary<string, Material> _mats;

        static BedroomBuilder()
        {
            // First time the project is opened: build the scene automatically if it doesn't exist yet.
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode || File.Exists(ScenePath) || SessionState.GetBool("pb_autobuilt", false)) return;
                SessionState.SetBool("pb_autobuilt", true);
                Build();
            };
        }

        [MenuItem("Pink Backrooms/2. Build Bedroom Scene", priority = 2)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            LookSetup.ApplyAll();
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(Root + "/Scenes");
            AssetDatabase.Refresh();
            _mats = new Dictionary<string, Material>();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var profile = LookSetup.CreateOrUpdateProfile();
            Environment(profile, out var volume);

            var room = new GameObject("Room").transform;
            var props = new GameObject("Props").transform;
            var lights = new GameObject("Lights").transform;
            var audio = new GameObject("Audio").transform;

            Shell(room, lights, audio, out var knockSource);
            var ambience = new List<AudioSource>();
            ambience.Add(Audio(audio, "Room Drone", Vector3.up * 1.5f, "room_drone.ogg", 0.45f, true, false));

            Bed(props, lights, ambience);
            Nightstands(props, lights, ambience);
            Vanity(props, lights, ambience, out var mirrorLights, out var mirrorGlass);
            Scale(props, lights);
            WalkingPad(props, ambience);
            Weights(props);
            Wardrobe(props);
            Clothes(props);
            Takeout(props);
            Chandelier(props, lights, ambience);
            Decor(room);

            var hud = BuildHUD(out var canvas);
            var player = Player(hud, canvas, out var interactor);

            // Director
            var dirGo = new GameObject("Bedroom Director");
            var dir = dirGo.AddComponent<BedroomDirector>();
            dir.hud = hud;
            dir.player = player;
            dir.interactor = interactor;
            dir.volume = volume;
            dir.mirrorLights = mirrorLights;
            dir.mirrorGlass = mirrorGlass;
            dir.sfx = dirGo.AddComponent<AudioSource>();
            dir.sfx.playOnAwake = false;
            dir.mirrorWakeSound = Clip("thud.wav");
            dir.glitchSound = Clip("glitch.wav");
            dir.knockSource = knockSource;
            dir.knocks = new[] { Clip("knock_01.wav"), Clip("knock_02.wav"), Clip("knock_03.wav") };
            dir.ambienceToSilence = ambience.ToArray();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[Pink Backrooms] Bedroom built: " + ScenePath);
        }

        // ------------------------------------------------------------------ environment

        static void Environment(VolumeProfile profile, out Volume volume)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.075f, 0.05f, 0.062f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.05f, 0.032f, 0.042f);
            RenderSettings.fogDensity = 0.1f;
            RenderSettings.reflectionIntensity = 0.3f;

            var go = new GameObject("Post Processing (Look)");
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        // ------------------------------------------------------------------ room shell

        static void Shell(Transform room, Transform lights, Transform audio, out AudioSource knockSource)
        {
            Box("Floor", room, new Vector3(0, -0.05f, 0), new Vector3(W, 0.1f, D),
                Mat("Floor", "floor_wood", Color.white, new Vector2(W / 0.6f, D / 1.2f)), collider: true);
            Box("Ceiling", room, new Vector3(0, H + 0.05f, 0), new Vector3(W, 0.1f, D),
                Mat("Ceiling", "ceiling", new Color(0.85f, 0.8f, 0.8f), new Vector2(W / 2f, D / 2f)), collider: true);

            // North / south walls (span x), west wall (span z)
            Wall(room, "Wall North", new Vector3(0, 0, HZ + 0.1f), W + 0.4f, 0, H, false);
            Wall(room, "Wall West", new Vector3(-HX - 0.1f, 0, 0), D, 0, H, true);
            Wall(room, "Wall South", new Vector3(0, 0, -HZ - 0.1f), W + 0.4f, 0, H, false);

            // East wall with a boarded window opening (z 0..1.2, y 0.9..2.0)
            float wz0 = 0f, wz1 = 1.2f, wy0 = 0.9f, wy1 = 2.0f;
            float ex = HX + 0.1f;
            Wall(room, "Wall East A", new Vector3(ex, 0, (-HZ + wz0) / 2f), wz0 + HZ, 0, H, true);
            Wall(room, "Wall East B", new Vector3(ex, 0, (wz1 + HZ) / 2f), HZ - wz1, 0, H, true);
            Wall(room, "Wall East Below Window", new Vector3(ex, 0, (wz0 + wz1) / 2f), wz1 - wz0, 0, wy0, true);
            Wall(room, "Wall East Above Window", new Vector3(ex, 0, (wz0 + wz1) / 2f), wz1 - wz0, wy1, H, true);
            Window(room, lights, new Vector3(HX, 0, (wz0 + wz1) / 2f), wz1 - wz0, wy0, wy1);

            Trim(room);
            knockSource = Door(room, lights, audio);
        }

        /// One wall = clean floral wallpaper above the chair rail, stained wallpaper below it.
        static void Wall(Transform parent, string name, Vector3 baseCenter, float width, float y0, float y1, bool alongZ)
        {
            const float tile = 0.8f, thick = 0.2f;
            void Part(string n, float a, float b, string tex)
            {
                if (b - a <= 0.001f) return;
                var size = alongZ ? new Vector3(thick, b - a, width) : new Vector3(width, b - a, thick);
                var tiling = new Vector2(width / tile, tex == "wallpaper_dirty" ? 1f : (b - a) / tile);
                Box(n, parent, new Vector3(baseCenter.x, (a + b) / 2f, baseCenter.z), size,
                    Mat("Wallpaper " + tex, tex, Color.white, tiling), collider: true);
            }
            Part(name + " (lower)", y0, Mathf.Min(y1, Band), "wallpaper_dirty");
            Part(name + " (upper)", Mathf.Max(y0, Band), y1, "wallpaper_floral");
        }

        static void Trim(Transform room)
        {
            var white = Mat("Trim", "paint_white", new Color(0.92f, 0.86f, 0.86f), Vector2.one);
            var t = new GameObject("Trim").transform; t.SetParent(room, false);
            // baseboards, chair rail, crown, on the three plain walls + east wall pieces
            foreach (var (y, h, d) in new[] { (0.06f, 0.12f, 0.025f), (Band, 0.05f, 0.03f), (H - 0.04f, 0.08f, 0.04f) })
            {
                Box("Trim N", t, new Vector3(0, y, HZ - d / 2), new Vector3(W, h, d), white);
                Box("Trim S", t, new Vector3(0, y, -HZ + d / 2), new Vector3(W, h, d), white);
                Box("Trim W", t, new Vector3(-HX + d / 2, y, 0), new Vector3(d, h, D), white);
                Box("Trim E1", t, new Vector3(HX - d / 2, y, -HZ / 2f), new Vector3(d, h, HZ), white);
                if (y < 0.9f || y > 2.0f)
                    Box("Trim E2", t, new Vector3(HX - d / 2, y, 0.6f), new Vector3(d, h, 1.2f), white);
                Box("Trim E3", t, new Vector3(HX - d / 2, y, (1.2f + HZ) / 2f), new Vector3(d, h, HZ - 1.2f), white);
            }
        }

        static void Window(Transform room, Transform lights, Vector3 c, float width, float y0, float y1)
        {
            var g = Group("Boarded Window", room, Vector3.zero);
            var white = Mat("Trim", "paint_white", new Color(0.92f, 0.86f, 0.86f), Vector2.one);
            float h = y1 - y0, cy = (y0 + y1) / 2f;
            // reveal (inside of the wall opening)
            Box("Reveal Top", g, new Vector3(c.x + 0.1f, y1 - 0.01f, c.z), new Vector3(0.2f, 0.02f, width), white);
            Box("Sill", g, new Vector3(c.x + 0.07f, y0 + 0.01f, c.z), new Vector3(0.3f, 0.04f, width + 0.1f), white);
            Box("Reveal L", g, new Vector3(c.x + 0.1f, cy, c.z - width / 2 + 0.01f), new Vector3(0.2f, h, 0.02f), white);
            Box("Reveal R", g, new Vector3(c.x + 0.1f, cy, c.z + width / 2 - 0.01f), new Vector3(0.2f, h, 0.02f), white);
            // vertical slats of a broken blind behind the boards
            var blind = Mat("Blind", "paint_white", new Color(0.7f, 0.68f, 0.7f), Vector2.one);
            for (int i = 0; i < 6; i++)
                Box("Blind Slat", g, new Vector3(c.x + 0.16f, cy + 0.2f + Random.Range(-0.05f, 0.05f), c.z - 0.5f + i * 0.2f),
                    new Vector3(0.01f, h * Random.Range(0.4f, 0.7f), 0.07f), blind, new Vector3(Random.Range(-8, 8), 0, Random.Range(-4, 4)));
            // boards nailed across, with gaps so moonlight cuts through in stripes
            var plank = Mat("Plank", "plank", Color.white, Vector2.one);
            float[] ys = { y0 + 0.12f, y0 + 0.42f, y0 + 0.68f, y1 - 0.12f };
            for (int i = 0; i < ys.Length; i++)
                Box("Board", g, new Vector3(c.x - 0.03f, ys[i], c.z), new Vector3(0.025f, 0.16f, width + 0.3f), plank,
                    new Vector3(i % 2 == 0 ? 4f : -6f, 0, 0), collider: false, castShadows: true);
            // night outside
            Quad("Night", g, new Vector3(c.x + 0.45f, cy, c.z), new Vector2(width * 1.5f, h * 1.5f),
                Mat("Night", null, new Color(0.02f, 0.025f, 0.05f), Vector2.one, new Color(0.03f, 0.05f, 0.12f)), new Vector3(0, -90, 0));

            // sits just outside the boards so the gaps throw stripes of light across the floor
            var moon = Spot(lights, "Moonlight (window)", new Vector3(c.x + 0.32f, 1.95f, c.z), new Vector3(0.9f, 0f, 0.5f),
                new Color(0.55f, 0.65f, 1f), 14f, 6f, 75f, false);
            // soft cold spill just inside the window so the boards read even when the moon cone misses the camera
            Point(lights, "Window Spill", new Vector3(c.x - 0.5f, 1.4f, c.z), new Color(0.5f, 0.6f, 1f), 0.6f, 2.2f, false);
            moon.innerSpotAngle = 30f;
        }

        static AudioSource Door(Transform room, Transform lights, Transform audio)
        {
            var g = Group("Door", room, new Vector3(-1.5f, 0, -HZ));
            var door = Mat("Door", "paint_white", new Color(0.86f, 0.78f, 0.8f), new Vector2(1, 2));
            var trim = Mat("Trim", "paint_white", new Color(0.92f, 0.86f, 0.86f), Vector2.one);
            Box("Door Slab", g, new Vector3(0, 1.03f, 0.025f), new Vector3(0.88f, 2.04f, 0.05f), door, collider: true);
            // panels
            foreach (var (x, y, sy) in new[] { (-0.2f, 1.5f, 0.7f), (0.2f, 1.5f, 0.7f), (-0.2f, 0.55f, 0.7f), (0.2f, 0.55f, 0.7f) })
                Box("Panel", g, new Vector3(x, y, 0.055f), new Vector3(0.3f, sy, 0.012f), door);
            Box("Frame L", g, new Vector3(-0.49f, 1.05f, 0.02f), new Vector3(0.08f, 2.12f, 0.06f), trim);
            Box("Frame R", g, new Vector3(0.49f, 1.05f, 0.02f), new Vector3(0.08f, 2.12f, 0.06f), trim);
            Box("Frame T", g, new Vector3(0, 2.1f, 0.02f), new Vector3(1.06f, 0.08f, 0.06f), trim);
            Prim(PrimitiveType.Sphere, "Knob", g, new Vector3(0.34f, 1.0f, 0.08f), Vector3.one * 0.06f,
                Mat("Brass", null, new Color(0.55f, 0.45f, 0.3f), Vector2.one, null, 0.6f, metallic: 0.8f));
            // cold light leaking under the door
            Box("Light Under Door", g, new Vector3(0, 0.006f, 0.06f), new Vector3(0.84f, 0.01f, 0.05f),
                Mat("Door Gap Light", null, Color.black, Vector2.one, new Color(0.6f, 0.75f, 1f) * 2.5f));
            var gap = Point(lights, "Under Door Light", g.TransformPoint(new Vector3(0, 0.05f, 0.25f)), new Color(0.6f, 0.75f, 1f), 0.6f, 1.4f, false);
            var fl = gap.gameObject.AddComponent<FlickerLight>();
            fl.lights = new[] { gap }; fl.mode = FlickerLight.Mode.Flicker; fl.eventRate = 0.08f;

            var it = Interact(g, "try the door", false, 0.15f,
                "it doesn't open from this side.",
                "something is on the other side. it's always on the other side.",
                "i'm not ready to go out there.");
            it.sound = Clip("thud.wav"); it.soundVolume = 0.4f;

            return Audio(audio, "Knocks (door)", g.TransformPoint(new Vector3(0, 1.3f, -0.4f)), null, 1f, false, true, 12f);
        }

        // ------------------------------------------------------------------ bed + canopy + fairy lights

        static void Bed(Transform props, Transform lights, List<AudioSource> ambience)
        {
            var g = Group("Bed", props, new Vector3(-0.5f, 0, 1.15f));
            var frame = Mat("Bed Frame", "paint_white", new Color(0.93f, 0.88f, 0.86f), Vector2.one);
            var mattress = Mat("Mattress", "mattress", Color.white, new Vector2(1.5f, 2f));
            var sheet = Mat("Sheet Hearts", "sheet_hearts", Color.white, new Vector2(2f, 2f), doubleSided: true);
            var duvet = Mat("Duvet", "fabric_pink_knit", new Color(1f, 0.93f, 0.95f), new Vector2(3f, 3f), doubleSided: true);
            var pillowW = Mat("Pillow White", "fabric_white", Color.white, new Vector2(2, 1));
            var pillowP = Mat("Pillow Pink", "fabric_dots", Color.white, new Vector2(2, 1));

            Box("Headboard", g, new Vector3(0, 0.65f, 1.02f), new Vector3(1.62f, 1.3f, 0.06f), frame, collider: true);
            Prim(PrimitiveType.Cylinder, "Headboard Rail", g, new Vector3(0, 1.31f, 1.02f), new Vector3(0.07f, 0.82f, 0.07f), frame, new Vector3(0, 0, 90));
            foreach (var x in new[] { -0.79f, 0.79f })
            {
                Prim(PrimitiveType.Cylinder, "Post", g, new Vector3(x, 0.45f, -0.99f), new Vector3(0.06f, 0.45f, 0.06f), frame);
                Prim(PrimitiveType.Sphere, "Finial", g, new Vector3(x, 0.93f, -0.99f), Vector3.one * 0.09f, frame);
                Prim(PrimitiveType.Cylinder, "Head Post", g, new Vector3(x, 0.7f, 1.02f), new Vector3(0.06f, 0.7f, 0.06f), frame);
                Box("Side Rail", g, new Vector3(x, 0.3f, 0.01f), new Vector3(0.05f, 0.14f, 2f), frame);
            }
            Box("Footboard", g, new Vector3(0, 0.38f, -0.99f), new Vector3(1.56f, 0.34f, 0.04f), frame);
            Box("Mattress", g, new Vector3(0, 0.46f, 0), new Vector3(1.5f, 0.24f, 1.96f), mattress, collider: true);

            // fitted sheet pulled half off: mattress shows at the foot and one side
            Box("Fitted Sheet (pulled off)", g, new Vector3(0.12f, 0.585f, 0.38f), new Vector3(1.32f, 0.02f, 1.22f), sheet, new Vector3(0, 5, 0));
            Box("Sheet Corner (hanging)", g, new Vector3(0.74f, 0.46f, 0.55f), new Vector3(0.02f, 0.3f, 0.7f), sheet, new Vector3(0, 0, 8));
            // duvet: bunched on the bed, sliding off the left side, pooled on the floor
            Prim(PrimitiveType.Sphere, "Duvet Lump", g, new Vector3(-0.15f, 0.64f, -0.35f), new Vector3(1.0f, 0.2f, 0.85f), duvet, new Vector3(0, -12, 4));
            Prim(PrimitiveType.Sphere, "Duvet Lump 2", g, new Vector3(-0.45f, 0.67f, 0.05f), new Vector3(0.6f, 0.22f, 0.55f), duvet, new Vector3(0, 20, -6));
            Box("Duvet Hanging", g, new Vector3(-0.84f, 0.36f, -0.3f), new Vector3(0.03f, 0.62f, 1.05f), duvet, new Vector3(0, -6, -14));
            Prim(PrimitiveType.Sphere, "Duvet Pool", g, new Vector3(-1.12f, 0.03f, -0.4f), new Vector3(0.6f, 0.09f, 1.2f), duvet, new Vector3(0, -14, 0));
            // pillows
            Prim(PrimitiveType.Sphere, "Pillow", g, new Vector3(-0.32f, 0.66f, 0.76f), new Vector3(0.58f, 0.17f, 0.36f), pillowW, new Vector3(0, 8, 3));
            Prim(PrimitiveType.Sphere, "Pillow 2", g, new Vector3(0.36f, 0.68f, 0.82f), new Vector3(0.55f, 0.17f, 0.34f), pillowP, new Vector3(-10, -6, -8));
            // black hoodie thrown on the bed
            var black = Mat("Fabric Black", "fabric_black", Color.white, new Vector2(2, 2));
            Prim(PrimitiveType.Sphere, "Hoodie", g, new Vector3(0.28f, 0.63f, -0.3f), new Vector3(0.48f, 0.1f, 0.4f), black, new Vector3(0, 30, 0));
            Prim(PrimitiveType.Capsule, "Hoodie Sleeve", g, new Vector3(0.52f, 0.61f, -0.55f), new Vector3(0.1f, 0.22f, 0.08f), black, new Vector3(90, 50, 0));

            Interact(g, "look at the bed", true, 0.05f,
                "i haven't made it in weeks.",
                "i lie here most days. it still doesn't feel like resting.",
                "the sheet keeps slipping off. i let it.");

            Bunny(props, g.TransformPoint(new Vector3(0.45f, 0.78f, 0.62f)));
            Canopy(g);
            FairyLights(g, lights);
        }

        /// An original plush bunny slumped on the pillow.
        static void Bunny(Transform props, Vector3 pos)
        {
            var b = Group("Plush Bunny", props, pos);
            b.localEulerAngles = new Vector3(12, -150, 22);
            var fur = Mat("Plush", "fabric_white", new Color(1f, 0.82f, 0.88f), Vector2.one);
            var dark = Mat("Button Eye", null, new Color(0.05f, 0.04f, 0.05f), Vector2.one, null, 0.6f);
            Prim(PrimitiveType.Sphere, "Body", b, Vector3.zero, new Vector3(0.18f, 0.2f, 0.16f), fur);
            Prim(PrimitiveType.Sphere, "Head", b, new Vector3(0, 0.15f, 0.01f), Vector3.one * 0.15f, fur);
            Prim(PrimitiveType.Capsule, "Ear L", b, new Vector3(-0.04f, 0.26f, 0), new Vector3(0.045f, 0.07f, 0.03f), fur, new Vector3(0, 0, 15));
            Prim(PrimitiveType.Capsule, "Ear R (flopped)", b, new Vector3(0.06f, 0.22f, 0), new Vector3(0.045f, 0.07f, 0.03f), fur, new Vector3(0, 0, -70));
            Prim(PrimitiveType.Sphere, "Eye L", b, new Vector3(-0.03f, 0.16f, -0.065f), Vector3.one * 0.02f, dark);
            Prim(PrimitiveType.Sphere, "Eye R", b, new Vector3(0.03f, 0.16f, -0.065f), Vector3.one * 0.02f, dark);
            Interact(b, "pick up the bunny", false, 0.05f, "she's been here the whole time.", "she doesn't ask me anything.");
        }

        static void Canopy(Transform bed)
        {
            var g = Group("Canopy", bed, new Vector3(0, 0, 1.02f));
            var crown = Mat("Canopy Crown", "fabric_dots", new Color(0.95f, 0.85f, 0.9f), new Vector2(3, 1));
            var lace = Mat("Lace", "fabric_white", new Color(0.78f, 0.74f, 0.8f), new Vector2(1, 4), doubleSided: true);
            Prim(PrimitiveType.Cylinder, "Crown", g, new Vector3(0, 2.36f, 0.05f), new Vector3(0.55f, 0.09f, 0.3f), crown);
            Box("Crown Valance", g, new Vector3(0, 2.25f, -0.07f), new Vector3(0.52f, 0.12f, 0.02f), crown);
            Beam("Lace L", g, new Vector3(-0.18f, 2.3f, -0.05f), new Vector3(-0.92f, 0.85f, -0.18f), 0.22f, 0.012f, lace);
            Beam("Lace L2", g, new Vector3(-0.92f, 0.85f, -0.18f), new Vector3(-0.98f, 0.2f, -0.12f), 0.16f, 0.012f, lace);
            Beam("Lace R (torn)", g, new Vector3(0.18f, 2.3f, -0.05f), new Vector3(0.85f, 1.15f, -0.15f), 0.2f, 0.012f, lace);
        }

        static void FairyLights(Transform bed, Transform lights)
        {
            var g = Group("Fairy Lights", bed, new Vector3(0, 0, 1.08f));
            var wire = Mat("Wire", null, new Color(0.08f, 0.07f, 0.07f), Vector2.one);
            var bulbOn = Mat("Fairy Bulb", null, new Color(1f, 0.8f, 0.85f), Vector2.one, new Color(1f, 0.55f, 0.7f) * 2f);
            var bulbDead = Mat("Fairy Bulb Dead", null, new Color(0.35f, 0.3f, 0.32f), Vector2.one);
            var on = new List<Renderer>();
            const int n = 13;
            Vector3 prev = default;
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                var p = new Vector3(Mathf.Lerp(-1.05f, 1.1f, t), 2.08f - 0.22f * Mathf.Sin(Mathf.PI * t) - (t > 0.7f ? 0.12f * (t - 0.7f) / 0.3f : 0), 0);
                bool dead = i == 3 || i == 8 || i == 9;
                var bulb = Prim(PrimitiveType.Sphere, "Bulb", g, p, Vector3.one * 0.035f, dead ? bulbDead : bulbOn);
                if (!dead) on.Add(bulb.GetComponent<Renderer>());
                if (i > 0) Beam("Wire", g, prev, p, 0.006f, 0.006f, wire);
                prev = p;
            }
            var l = Point(lights, "Fairy Light Glow", g.TransformPoint(new Vector3(0, 1.85f, -0.25f)), new Color(1f, 0.6f, 0.75f), 0.7f, 2.6f, false);
            var fl = g.gameObject.AddComponent<FlickerLight>();
            fl.lights = new[] { l }; fl.bulbs = on.ToArray(); fl.bulbColor = new Color(1f, 0.55f, 0.7f) * 2f;
            fl.mode = FlickerLight.Mode.Flicker; fl.eventRate = 0.15f;
        }

        // ------------------------------------------------------------------ nightstands, lamp, CRT

        static void Nightstands(Transform props, Transform lights, List<AudioSource> ambience)
        {
            var white = Mat("Furniture White", "paint_white", Color.white, Vector2.one);
            var line = Mat("Drawer Line", null, new Color(0.25f, 0.2f, 0.22f), Vector2.one);
            var brass = Mat("Brass", null, new Color(0.55f, 0.45f, 0.3f), Vector2.one, null, 0.6f, metallic: 0.8f);

            Transform Stand(string name, Vector3 pos)
            {
                var s = Group(name, props, pos);
                Box("Body", s, new Vector3(0, 0.28f, 0), new Vector3(0.45f, 0.56f, 0.4f), white, collider: true);
                for (int i = 0; i < 2; i++)
                {
                    Box("Drawer Line", s, new Vector3(0, 0.2f + i * 0.2f, -0.201f), new Vector3(0.4f, 0.008f, 0.004f), line);
                    Prim(PrimitiveType.Sphere, "Pull", s, new Vector3(0, 0.3f + (i - 0.5f) * 0.2f, -0.21f), Vector3.one * 0.025f, brass);
                }
                return s;
            }

            // left: lamp, glass, tissues
            var left = Stand("Nightstand Left", new Vector3(-1.66f, 0, 1.97f));
            var shade = Mat("Lamp Shade", "fabric_pink_knit", new Color(1f, 0.85f, 0.9f), Vector2.one, new Color(1f, 0.45f, 0.6f) * 0.6f);
            Prim(PrimitiveType.Cylinder, "Lamp Base", left, new Vector3(-0.05f, 0.58f, 0.03f), new Vector3(0.1f, 0.02f, 0.1f), white);
            Prim(PrimitiveType.Cylinder, "Lamp Stem", left, new Vector3(-0.05f, 0.72f, 0.03f), new Vector3(0.02f, 0.14f, 0.02f), brass);
            var shadeGo = Prim(PrimitiveType.Cylinder, "Lamp Shade", left, new Vector3(-0.05f, 0.92f, 0.03f), new Vector3(0.24f, 0.09f, 0.24f), shade, new Vector3(4, 0, 6));
            var glass = Mat("Glass", null, new Color(0.7f, 0.75f, 0.8f), Vector2.one, null, 0.8f);
            Prim(PrimitiveType.Cylinder, "Glass of Water (old)", left, new Vector3(0.13f, 0.61f, -0.08f), new Vector3(0.06f, 0.05f, 0.06f), glass);
            Prim(PrimitiveType.Sphere, "Tissue", left, new Vector3(0.12f, 0.58f, 0.08f), new Vector3(0.06f, 0.04f, 0.05f), Mat("Tissue", "fabric_white", Color.white, Vector2.one));
            Prim(PrimitiveType.Sphere, "Tissue", props, new Vector3(-1.4f, 0.02f, 1.62f), new Vector3(0.06f, 0.04f, 0.05f), Mat("Tissue", "fabric_white", Color.white, Vector2.one));
            var lamp = Point(lights, "Bedside Lamp", left.TransformPoint(new Vector3(-0.05f, 0.85f, 0.03f)), new Color(1f, 0.55f, 0.65f), 1.1f, 3.2f, true);
            var fl = left.gameObject.AddComponent<FlickerLight>();
            fl.lights = new[] { lamp }; fl.bulbs = new[] { shadeGo.GetComponent<Renderer>() };
            fl.bulbColor = new Color(1f, 0.45f, 0.6f) * 0.6f; fl.mode = FlickerLight.Mode.Pulse; fl.pulseSpeed = 0.08f;

            // right: CRT TV with static
            var right = Stand("Nightstand Right (TV)", new Vector3(0.66f, 0, 1.97f));
            var tv = Group("CRT TV", right, new Vector3(0, 0.56f, 0));
            tv.localEulerAngles = new Vector3(0, -25, 0);
            var beige = Mat("TV Plastic", "paint_white", new Color(0.85f, 0.8f, 0.72f), Vector2.one);
            Box("TV Body", tv, new Vector3(0, 0.18f, 0.02f), new Vector3(0.42f, 0.34f, 0.38f), beige, collider: true);
            Box("TV Back", tv, new Vector3(0, 0.17f, 0.25f), new Vector3(0.3f, 0.24f, 0.14f), beige);
            var screen = Quad("Screen", tv, new Vector3(0, 0.19f, -0.172f), new Vector2(0.32f, 0.24f),
                Mat("TV Screen", null, Color.white, Vector2.one, Color.white), Vector3.zero);
            Box("Bezel", tv, new Vector3(0, 0.04f, -0.17f), new Vector3(0.42f, 0.05f, 0.01f), beige);
            Beam("Antenna", tv, new Vector3(-0.03f, 0.35f, 0.05f), new Vector3(-0.2f, 0.62f, 0.12f), 0.008f, 0.008f, brass);
            Beam("Antenna", tv, new Vector3(0.03f, 0.35f, 0.05f), new Vector3(0.24f, 0.58f, 0.02f), 0.008f, 0.008f, brass);
            var glow = Point(lights, "TV Glow", tv.TransformPoint(new Vector3(0, 0.2f, -0.45f)), new Color(0.7f, 0.8f, 1f), 1.2f, 3.5f, false);
            var st = tv.gameObject.AddComponent<TVStatic>();
            st.screen = screen.GetComponent<Renderer>(); st.glow = glow;
            ambience.Add(Audio(tv, "TV Static", new Vector3(0, 0.2f, 0), "tv_static.wav", 0.28f, true, true, 6f));
            Interact(tv, "look at the TV", false, 0.05f, "static. it's been static for a while.", "i leave it on so it's not quiet.");
        }

        // ------------------------------------------------------------------ vanity, mirror, measuring tape

        static void Vanity(Transform props, Transform lights, List<AudioSource> ambience, out FlickerLight mirrorLights, out Renderer mirrorGlass)
        {
            var white = Mat("Furniture White", "paint_white", Color.white, Vector2.one);
            var line = Mat("Drawer Line", null, new Color(0.25f, 0.2f, 0.22f), Vector2.one);
            var brass = Mat("Brass", null, new Color(0.55f, 0.45f, 0.3f), Vector2.one, null, 0.6f, metallic: 0.8f);

            // local: width along x, front faces local -z. Yaw -90 turns the front toward the room (+x).
            var v = Group("Vanity", props, new Vector3(-HX + 0.23f, 0, -0.6f));
            v.localEulerAngles = new Vector3(0, -90, 0);
            Box("Top", v, new Vector3(0, 0.74f, 0), new Vector3(1.1f, 0.04f, 0.45f), white, collider: true);
            foreach (var x in new[] { -0.42f, 0.42f })
            {
                Box("Pedestal", v, new Vector3(x, 0.36f, 0), new Vector3(0.26f, 0.72f, 0.43f), white, collider: true);
                for (int i = 0; i < 3; i++)
                {
                    Box("Drawer Line", v, new Vector3(x, 0.17f + i * 0.2f, -0.216f), new Vector3(0.22f, 0.008f, 0.004f), line);
                    Prim(PrimitiveType.Sphere, "Pull", v, new Vector3(x, 0.27f + i * 0.2f, -0.225f), Vector3.one * 0.022f, brass);
                }
            }
            // a drawer left hanging open
            Box("Open Drawer", v, new Vector3(0.42f, 0.5f, -0.33f), new Vector3(0.22f, 0.1f, 0.3f), white);

            Makeup(v, ambience);

            // stool with a cardigan draped over it
            var stool = Group("Stool", v, new Vector3(0, 0, -0.6f));
            var velvet = Mat("Velvet Pink", "fabric_pink_knit", new Color(0.95f, 0.65f, 0.75f), Vector2.one);
            Prim(PrimitiveType.Cylinder, "Seat", stool, new Vector3(0, 0.45f, 0), new Vector3(0.38f, 0.04f, 0.38f), velvet, Vector3.zero, collider: true);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2 + 0.78f;
                Prim(PrimitiveType.Cylinder, "Leg", stool, new Vector3(Mathf.Cos(a) * 0.12f, 0.21f, Mathf.Sin(a) * 0.12f), new Vector3(0.025f, 0.21f, 0.025f), white);
            }
            var lilac = Mat("Fabric Lilac", "fabric_lilac", Color.white, Vector2.one, doubleSided: true);
            Box("Cardigan", stool, new Vector3(0.02f, 0.49f, 0), new Vector3(0.44f, 0.03f, 0.3f), lilac, new Vector3(0, 15, 6));
            Box("Cardigan (hanging)", stool, new Vector3(0.22f, 0.33f, 0.03f), new Vector3(0.03f, 0.3f, 0.28f), lilac, new Vector3(0, 15, 10));

            Interact(v, "look at the vanity", true, 0.02f,
                "i still do my makeup. for who?",
                "everything here is pretty. i keep it pretty so nobody looks closer.");

            MeasuringTape(props, v);

            // mirror on the wall above, with hollywood bulbs that mostly don't work
            var m = Group("Mirror", props, new Vector3(-HX + 0.02f, 0, -0.6f));
            m.localEulerAngles = new Vector3(0, -90, 0);
            Box("Frame", m, new Vector3(0, 1.52f, 0), new Vector3(0.78f, 0.9f, 0.03f), white);
            var glassGo = Quad("Mirror Glass", m, new Vector3(0, 1.52f, -0.017f), new Vector2(0.62f, 0.72f),
                Mat("Mirror Glass", null, new Color(0.16f, 0.12f, 0.14f), Vector2.one, Color.black, 0.95f, metallic: 0.9f, reflections: true), Vector3.zero);
            mirrorGlass = glassGo.GetComponent<Renderer>();
            var bulbMat = Mat("Vanity Bulb", null, new Color(1f, 0.9f, 0.9f), Vector2.one, new Color(1f, 0.7f, 0.8f) * 2f);
            var bulbs = new List<Renderer>();
            for (int i = 0; i < 5; i++) bulbs.Add(Prim(PrimitiveType.Sphere, "Bulb", m, new Vector3(-0.3f + i * 0.15f, 1.98f, -0.03f), Vector3.one * 0.055f, bulbMat).GetComponent<Renderer>());
            for (int i = 0; i < 3; i++)
            {
                bulbs.Add(Prim(PrimitiveType.Sphere, "Bulb", m, new Vector3(-0.41f, 1.25f + i * 0.25f, -0.03f), Vector3.one * 0.055f, bulbMat).GetComponent<Renderer>());
                bulbs.Add(Prim(PrimitiveType.Sphere, "Bulb", m, new Vector3(0.41f, 1.25f + i * 0.25f, -0.03f), Vector3.one * 0.055f, bulbMat).GetComponent<Renderer>());
            }
            // old photos tucked into the frame
            var photo = Mat("Photo", "photo", Color.white, Vector2.one, doubleSided: true);
            Quad("Photo", m, new Vector3(-0.25f, 1.78f, -0.02f), new Vector2(0.1f, 0.13f), photo, new Vector3(0, 0, 12));
            Quad("Photo", m, new Vector3(0.27f, 1.3f, -0.02f), new Vector2(0.1f, 0.13f), photo, new Vector3(0, 0, -8));

            var ml = Point(lights, "Vanity Bulbs", m.TransformPoint(new Vector3(0, 1.55f, -0.4f)), new Color(1f, 0.6f, 0.72f), 0.9f, 3.2f, true);
            mirrorLights = m.gameObject.AddComponent<FlickerLight>();
            mirrorLights.lights = new[] { ml }; mirrorLights.bulbs = bulbs.ToArray();
            mirrorLights.bulbColor = new Color(1f, 0.7f, 0.8f) * 2f;
            mirrorLights.mode = FlickerLight.Mode.Dying; mirrorLights.eventRate = 0.25f;

            var it = Interact(m, "look in the mirror", false, 0.05f);
            it.requiresMirrorAwake = true;
            it.lockedLine = "not yet. i can't look yet.";
            it.triggersEnding = true;
        }

        static void Makeup(Transform v, List<AudioSource> ambience)
        {
            var top = 0.76f;
            var g = Group("Makeup", v, Vector3.zero);
            Box("Palette", g, new Vector3(-0.22f, top + 0.006f, -0.04f), new Vector3(0.18f, 0.012f, 0.09f), Mat("Palette", "palette", Color.white, Vector2.one), new Vector3(0, 12, 0));
            Box("Palette 2 (open)", g, new Vector3(0.05f, top + 0.006f, 0.08f), new Vector3(0.14f, 0.012f, 0.08f), Mat("Palette", "palette", Color.white, Vector2.one), new Vector3(0, -20, 0));
            var lip = new[] { new Color(0.8f, 0.2f, 0.35f), new Color(0.9f, 0.5f, 0.6f), new Color(0.6f, 0.15f, 0.2f), new Color(0.85f, 0.6f, 0.55f) };
            for (int i = 0; i < lip.Length; i++)
            {
                var c = Mat("Lipstick " + i, null, lip[i], Vector2.one, null, 0.5f);
                Prim(PrimitiveType.Cylinder, "Lipstick", g, new Vector3(0.12f + i * 0.04f, top + 0.012f, -0.12f + (i % 2) * 0.05f), new Vector3(0.022f, 0.035f, 0.022f), c, new Vector3(0, i * 40, 90));
            }
            var gold = Mat("Brass", null, new Color(0.55f, 0.45f, 0.3f), Vector2.one, null, 0.6f, metallic: 0.8f);
            Prim(PrimitiveType.Cylinder, "Lipstick (standing)", g, new Vector3(-0.42f, top + 0.035f, 0.1f), new Vector3(0.024f, 0.035f, 0.024f), gold);
            var bottle = Mat("Foundation", null, new Color(0.85f, 0.7f, 0.6f), Vector2.one, null, 0.7f);
            Prim(PrimitiveType.Cylinder, "Foundation", g, new Vector3(-0.35f, top + 0.05f, 0.1f), new Vector3(0.05f, 0.05f, 0.05f), bottle);
            Prim(PrimitiveType.Cylinder, "Foundation (spilled)", g, new Vector3(0.3f, top + 0.025f, 0.05f), new Vector3(0.05f, 0.05f, 0.05f), bottle, new Vector3(90, 30, 0));
            Decal("Spill", g, new Vector3(0.36f, top + 0.002f, -0.02f), new Vector3(90, 0, 0), 0.14f, Mat("Spill", "stain_water", new Color(1f, 0.9f, 0.85f), Vector2.one, clip: true, doubleSided: true));
            var pinkGlass = Mat("Perfume", null, new Color(1f, 0.6f, 0.75f), Vector2.one, new Color(0.3f, 0.05f, 0.12f), 0.9f);
            Box("Perfume", g, new Vector3(-0.47f, top + 0.05f, -0.05f), new Vector3(0.06f, 0.09f, 0.04f), pinkGlass);
            Prim(PrimitiveType.Sphere, "Perfume Cap", g, new Vector3(-0.47f, top + 0.11f, -0.05f), Vector3.one * 0.035f, gold);
            var cup = Mat("Brush Cup", null, new Color(0.95f, 0.7f, 0.8f), Vector2.one, null, 0.4f);
            Prim(PrimitiveType.Cylinder, "Brush Cup", g, new Vector3(-0.1f, top + 0.05f, 0.14f), new Vector3(0.07f, 0.05f, 0.07f), cup);
            var handle = Mat("Brush Handle", null, new Color(0.1f, 0.08f, 0.09f), Vector2.one);
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cylinder, "Brush", g, new Vector3(-0.1f + (i - 1.5f) * 0.01f, top + 0.13f, 0.14f), new Vector3(0.01f, 0.08f, 0.01f), handle, new Vector3((i - 1.5f) * 8, 0, (i - 1.5f) * 10));
            Prim(PrimitiveType.Cylinder, "Brush (dropped)", g, new Vector3(0.2f, top + 0.008f, 0.15f), new Vector3(0.012f, 0.08f, 0.012f), handle, new Vector3(0, 70, 90));
            Prim(PrimitiveType.Cylinder, "Compact", g, new Vector3(-0.08f, top + 0.008f, -0.12f), new Vector3(0.08f, 0.008f, 0.08f), Mat("Compact", null, new Color(0.95f, 0.75f, 0.82f), Vector2.one, null, 0.6f));
            var pad = Mat("Cotton", "fabric_white", Color.white, Vector2.one);
            for (int i = 0; i < 3; i++)
                Prim(PrimitiveType.Cylinder, "Cotton Pad", g, new Vector3(0.45f - i * 0.05f, top + 0.004f, -0.15f + i * 0.03f), new Vector3(0.05f, 0.003f, 0.05f), pad);
            // little bow hair clip
            var bow = Mat("Bow", null, new Color(0.95f, 0.55f, 0.7f), Vector2.one, null, 0.4f);
            Prim(PrimitiveType.Sphere, "Bow L", g, new Vector3(0.48f, top + 0.012f, 0.12f), new Vector3(0.04f, 0.015f, 0.03f), bow, new Vector3(0, 20, 0));
            Prim(PrimitiveType.Sphere, "Bow R", g, new Vector3(0.52f, top + 0.012f, 0.13f), new Vector3(0.04f, 0.015f, 0.03f), bow, new Vector3(0, -20, 0));

            // music box: the Snake Bird song, slowed down and warped
            var mb = Group("Music Box", g, new Vector3(-0.3f, top, -0.12f));
            mb.localEulerAngles = new Vector3(0, 25, 0);
            var mbMat = Mat("Music Box", "fabric_dots", new Color(1f, 0.85f, 0.9f), Vector2.one);
            Box("Box", mb, new Vector3(0, 0.035f, 0), new Vector3(0.11f, 0.07f, 0.08f), mbMat);
            Box("Lid (open)", mb, new Vector3(0, 0.1f, 0.045f), new Vector3(0.11f, 0.07f, 0.008f), mbMat, new Vector3(-15, 0, 0));
            ambience.Add(Audio(mb, "Music Box (warped)", Vector3.up * 0.05f, "musicbox_warped.ogg", 0.32f, true, true, 5f));
        }

        static void MeasuringTape(Transform props, Transform vanity)
        {
            var g = Group("Measuring Tape", props, Vector3.zero);
            var tape = Mat("Tape", "tape", Color.white, new Vector2(1, 1), doubleSided: true);
            var caseMat = Mat("Tape Case", null, new Color(0.95f, 0.6f, 0.72f), Vector2.one, null, 0.5f);
            Vector3 edge = vanity.TransformPoint(new Vector3(0.5f, 0.765f, -0.23f));
            Prim(PrimitiveType.Cylinder, "Tape Case", g, vanity.TransformPoint(new Vector3(0.48f, 0.775f, -0.12f)), new Vector3(0.07f, 0.012f, 0.07f), caseMat);
            Vector3 hang = edge + new Vector3(0.04f, -0.75f, 0.03f);
            Vector3 floor1 = new Vector3(hang.x + 0.25f, 0.003f, hang.z + 0.12f);
            Vector3 floor2 = new Vector3(floor1.x + 0.2f, 0.003f, floor1.z - 0.2f);
            Beam("Tape (hanging)", g, vanity.TransformPoint(new Vector3(0.48f, 0.77f, -0.15f)), edge, 0.018f, 0.002f, tape, tileLength: 0.3f);
            Beam("Tape (hanging)", g, edge, hang, 0.018f, 0.002f, tape, tileLength: 0.3f);
            Beam("Tape (floor)", g, hang, floor1, 0.018f, 0.002f, tape, tileLength: 0.3f);
            Beam("Tape (floor)", g, floor1, floor2, 0.018f, 0.002f, tape, tileLength: 0.3f);
            Interact(g, "look at the measuring tape", true, 0.25f,
                "i already know. i check anyway.",
                "it never says what i need it to say.");
        }

        // ------------------------------------------------------------------ scale, walking pad, weights

        static void Scale(Transform props, Transform lights)
        {
            var g = Group("Scale", props, new Vector3(-1.45f, 0, -0.05f));
            g.localEulerAngles = new Vector3(0, 20, 0);
            Box("Body", g, new Vector3(0, 0.025f, 0), new Vector3(0.32f, 0.05f, 0.32f), Mat("Scale Body", "paint_white", new Color(0.97f, 0.92f, 0.94f), Vector2.one, null, 0.4f), collider: false);
            Box("Glass Top", g, new Vector3(0, 0.052f, 0.02f), new Vector3(0.29f, 0.004f, 0.25f), Mat("Scale Glass", null, new Color(0.25f, 0.18f, 0.22f), Vector2.one, null, 0.85f));
            var disp = Box("Display (blank)", g, new Vector3(0, 0.054f, -0.12f), new Vector3(0.1f, 0.004f, 0.04f),
                Mat("Scale Display", null, Color.black, Vector2.one, new Color(1f, 0.25f, 0.4f) * 2.5f));
            var glow = Point(lights, "Scale Glow", g.TransformPoint(new Vector3(0, 0.15f, -0.12f)), new Color(1f, 0.3f, 0.45f), 0.5f, 0.9f, false);
            var sd = g.gameObject.AddComponent<ScaleDisplay>();
            sd.display = disp.GetComponent<Renderer>(); sd.glow = glow;
            var it = Interact(g, "look at the scale", true, 0.35f,
                "it doesn't show a number anymore. it doesn't need to.",
                "i step on it in my head all day.");
            it.sound = Clip("scale_beep.wav"); it.soundVolume = 0.5f;
            UnityEventTools.AddPersistentListener(it.onInteract, sd.Glitch);
        }

        static void WalkingPad(Transform props, List<AudioSource> ambience)
        {
            var g = Group("Walking Pad", props, new Vector3(1.6f, 0, 0.35f));
            g.localEulerAngles = new Vector3(0, -8, 0);
            var shell = Mat("Pad Shell", null, new Color(0.2f, 0.19f, 0.21f), Vector2.one, null, 0.3f);
            var beltMat = Mat("Pad Belt", "fabric_black", new Color(0.7f, 0.7f, 0.7f), new Vector2(3, 10));
            Box("Base", g, new Vector3(0, 0.04f, 0), new Vector3(0.56f, 0.08f, 1.35f), shell, collider: true);
            var belt = Box("Belt", g, new Vector3(0, 0.082f, -0.02f), new Vector3(0.46f, 0.006f, 1.2f), beltMat);
            Box("Front Cap", g, new Vector3(0, 0.07f, 0.62f), new Vector3(0.56f, 0.06f, 0.12f), shell);
            var disp = Box("Display", g, new Vector3(0, 0.102f, 0.62f), new Vector3(0.16f, 0.004f, 0.05f),
                Mat("Pad Display", null, Color.black, Vector2.one, new Color(1f, 0.4f, 0.6f) * 2f));
            var blink = g.gameObject.AddComponent<ScaleDisplay>();
            blink.display = disp.GetComponent<Renderer>(); blink.color = new Color(1f, 0.4f, 0.6f) * 2f; blink.blinkRate = 0.5f;
            var sb = g.gameObject.AddComponent<ScrollingBelt>();
            sb.belt = belt.GetComponent<Renderer>(); sb.speed = 0.5f;
            ambience.Add(Audio(g, "Walking Pad Motor", new Vector3(0, 0.1f, 0.5f), "walkpad_motor.wav", 0.35f, true, true, 6f));
            Interact(g, "look at the walking pad", true, 0.3f,
                "it's still running. i don't remember turning it on.",
                "it never stops. so i don't either.");
        }

        static void Weights(Transform props)
        {
            var g = Group("Hand Weights", props, new Vector3(1.0f, 0, -0.55f));
            var vinyl = Mat("Weight Vinyl", null, new Color(0.95f, 0.55f, 0.7f), Vector2.one, null, 0.55f);
            var bar = Mat("Weight Bar", null, new Color(0.6f, 0.6f, 0.62f), Vector2.one, null, 0.7f, metallic: 0.8f);
            void Dumbbell(Vector3 p, float yaw)
            {
                var d = Group("Dumbbell", g, p); d.localEulerAngles = new Vector3(0, yaw, 0);
                Prim(PrimitiveType.Cylinder, "Bar", d, Vector3.zero, new Vector3(0.025f, 0.08f, 0.025f), bar, new Vector3(0, 0, 90));
                Prim(PrimitiveType.Cylinder, "End", d, new Vector3(-0.085f, 0, 0), new Vector3(0.075f, 0.025f, 0.075f), vinyl, new Vector3(0, 0, 90));
                Prim(PrimitiveType.Cylinder, "End", d, new Vector3(0.085f, 0, 0), new Vector3(0.075f, 0.025f, 0.075f), vinyl, new Vector3(0, 0, 90));
            }
            Dumbbell(new Vector3(0, 0.038f, 0), 30);
            Dumbbell(new Vector3(0.18f, 0.038f, 0.22f), -70);
            Interact(g, "look at the weights", true, 0.35f,
                "too heavy to lift. too heavy to put away.",
                "i bought them on a good day.");
        }

        // ------------------------------------------------------------------ wardrobe, clothes, takeout

        static void Wardrobe(Transform props)
        {
            var g = Group("Wardrobe", props, new Vector3(1.55f, 0, -HZ + 0.3f));
            var white = Mat("Furniture White", "paint_white", Color.white, Vector2.one);
            var inside = Mat("Wardrobe Inside", null, new Color(0.12f, 0.09f, 0.1f), Vector2.one);
            Box("Back", g, new Vector3(0, 1.0f, -0.28f), new Vector3(1.1f, 2.0f, 0.04f), inside, collider: true);
            Box("Side L", g, new Vector3(-0.53f, 1.0f, 0), new Vector3(0.04f, 2.0f, 0.6f), white, collider: true);
            Box("Side R", g, new Vector3(0.53f, 1.0f, 0), new Vector3(0.04f, 2.0f, 0.6f), white, collider: true);
            Box("Top", g, new Vector3(0, 1.98f, 0), new Vector3(1.1f, 0.04f, 0.6f), white);
            Box("Bottom", g, new Vector3(0, 0.06f, 0), new Vector3(1.1f, 0.12f, 0.6f), white, collider: true);
            Box("Door (shut)", g, new Vector3(0.27f, 1.02f, 0.3f), new Vector3(0.53f, 1.9f, 0.03f), white, collider: true);
            var hinge = Group("Door (open) Hinge", g, new Vector3(-0.54f, 0, 0.3f));
            hinge.localEulerAngles = new Vector3(0, -105, 0);
            Box("Door (open)", hinge, new Vector3(0.27f, 1.02f, 0), new Vector3(0.53f, 1.9f, 0.03f), white, collider: true);
            Prim(PrimitiveType.Cylinder, "Rail", g, new Vector3(0, 1.75f, 0), new Vector3(0.02f, 0.52f, 0.02f), Mat("Weight Bar", null, new Color(0.6f, 0.6f, 0.62f), Vector2.one, null, 0.7f, metallic: 0.8f), new Vector3(0, 0, 90));
            string[] fabrics = { "fabric_pink_knit", "fabric_dots", "fabric_white", "fabric_lilac", "fabric_plaid", "fabric_pink_knit", "fabric_black" };
            for (int i = 0; i < fabrics.Length; i++)
            {
                var m = Mat("Hanging " + fabrics[i], fabrics[i], Color.white, Vector2.one);
                Box("Hanging Clothes", g, new Vector3(-0.45f + i * 0.075f, 1.32f - (i % 3) * 0.05f, 0.0f), new Vector3(0.035f, 0.78f, 0.42f), m, new Vector3(0, Random.Range(-8f, 8f), Random.Range(-3f, 3f)));
            }
        }

        static void Clothes(Transform props)
        {
            // main pile spilling out of the wardrobe
            var pile = Group("Clothes Pile", props, new Vector3(1.3f, 0, -1.3f));
            string[] fabrics = { "fabric_pink_knit", "fabric_black", "fabric_white", "fabric_plaid", "fabric_dots", "fabric_lilac", "fabric_white", "fabric_pink_knit", "fabric_black", "fabric_dots", "fabric_lilac", "fabric_plaid", "fabric_white", "fabric_pink_knit" };
            var rnd = new System.Random(4);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            for (int i = 0; i < fabrics.Length; i++)
            {
                var m = Mat("Cloth " + fabrics[i], fabrics[i], Color.white, Vector2.one, doubleSided: true);
                var p = new Vector3(R(-0.4f, 0.4f) * (1f - i / 20f), 0.03f + i * 0.012f, R(-0.35f, 0.35f) * (1f - i / 20f));
                if (i % 2 == 0)
                    Prim(PrimitiveType.Sphere, "Clothes (bunched)", pile, p + Vector3.up * 0.03f, new Vector3(R(0.3f, 0.5f), R(0.12f, 0.22f), R(0.25f, 0.42f)), m, new Vector3(R(-10, 10), R(0, 360), R(-10, 10)));
                else
                    Box("Clothes (flat)", pile, p, new Vector3(R(0.35f, 0.55f), 0.02f, R(0.3f, 0.5f)), m, new Vector3(R(-6, 6), R(0, 360), R(-6, 6)));
            }
            Interact(pile, "look at the clothes", true, 0.35f,
                "nothing fits the way it's supposed to.",
                "i tried everything on. then i couldn't put any of it back.");

            // jeans and a shirt dropped on the rug
            var drop = Group("Dropped Clothes", props, new Vector3(-0.35f, 0, -0.85f));
            var denim = Mat("Denim", "fabric_plaid", new Color(0.55f, 0.6f, 0.85f), Vector2.one);
            Box("Jeans Leg", drop, new Vector3(0, 0.015f, 0), new Vector3(0.16f, 0.03f, 0.7f), denim, new Vector3(0, 20, 0));
            Box("Jeans Leg", drop, new Vector3(0.12f, 0.02f, -0.02f), new Vector3(0.16f, 0.03f, 0.6f), denim, new Vector3(0, 35, 3));
            var tee = Mat("Cloth fabric_white", "fabric_white", Color.white, Vector2.one, doubleSided: true);
            Box("T-shirt", drop, new Vector3(-0.45f, 0.012f, 0.3f), new Vector3(0.45f, 0.02f, 0.5f), tee, new Vector3(0, -30, 0));
            Box("Sock", drop, new Vector3(0.5f, 0.012f, 0.55f), new Vector3(0.07f, 0.02f, 0.2f), Mat("Cloth fabric_pink_knit", "fabric_pink_knit", Color.white, Vector2.one, doubleSided: true), new Vector3(0, 60, 0));
        }

        static void Takeout(Transform props)
        {
            var g = Group("Takeout Bags", props, new Vector3(-0.35f, 0, -1.7f));
            var kraft = Mat("Kraft Bag", "kraft", Color.white, Vector2.one);
            var plastic = Mat("Plastic Bag", "plastic_bag", Color.white, Vector2.one);
            var box = Mat("Takeout Box", "paint_white", new Color(0.96f, 0.94f, 0.92f), Vector2.one);
            Box("Bag (standing)", g, new Vector3(0, 0.16f, 0), new Vector3(0.26f, 0.32f, 0.16f), kraft, new Vector3(0, 12, 0));
            Box("Bag (folded top)", g, new Vector3(0, 0.34f, 0.03f), new Vector3(0.25f, 0.06f, 0.06f), kraft, new Vector3(-30, 12, 0));
            Box("Bag (tipped)", g, new Vector3(0.38f, 0.08f, 0.15f), new Vector3(0.26f, 0.16f, 0.32f), kraft, new Vector3(0, -35, 0));
            Box("Box (unopened)", g, new Vector3(0.55f, 0.04f, -0.05f), new Vector3(0.2f, 0.08f, 0.16f), box, new Vector3(0, 25, 0));
            Box("Box (unopened)", g, new Vector3(0.6f, 0.12f, -0.03f), new Vector3(0.2f, 0.08f, 0.16f), box, new Vector3(0, 10, 0));
            Prim(PrimitiveType.Sphere, "Plastic Bag", g, new Vector3(-0.32f, 0.08f, 0.12f), new Vector3(0.3f, 0.18f, 0.26f), plastic, new Vector3(0, 40, 10));
            var cupMat = Mat("Cup", null, new Color(0.95f, 0.85f, 0.88f), Vector2.one, null, 0.3f);
            Prim(PrimitiveType.Cylinder, "Cup", g, new Vector3(0.2f, 0.09f, -0.2f), new Vector3(0.09f, 0.09f, 0.09f), cupMat);
            Prim(PrimitiveType.Cylinder, "Straw", g, new Vector3(0.21f, 0.22f, -0.2f), new Vector3(0.008f, 0.07f, 0.008f), Mat("Straw", null, new Color(0.95f, 0.4f, 0.6f), Vector2.one), new Vector3(0, 0, 10));
            Prim(PrimitiveType.Cylinder, "Cup (tipped)", props, new Vector3(0.95f, 0.045f, 1.0f), new Vector3(0.09f, 0.09f, 0.09f), cupMat, new Vector3(90, 40, 0));
            Box("Receipt", g, new Vector3(-0.1f, 0.002f, -0.28f), new Vector3(0.06f, 0.002f, 0.2f), Mat("Receipt", "fabric_white", Color.white, Vector2.one), new Vector3(0, 30, 0));
            Interact(g, "look at the bags", true, 0.3f,
                "i ordered it. i didn't open it. i ordered it again.",
                "the delivery guy is the only person who's knocked in a while.");

            // one more bag by the bed
            var b2 = Group("Takeout Bag (by bed)", props, new Vector3(0.55f, 0, 0.45f));
            Box("Bag (crumpled)", b2, new Vector3(0, 0.1f, 0), new Vector3(0.22f, 0.2f, 0.14f), kraft, new Vector3(8, -50, 12));
        }

        // ------------------------------------------------------------------ chandelier + decor

        static void Chandelier(Transform props, Transform lights, List<AudioSource> ambience)
        {
            var g = Group("Chandelier", props, new Vector3(0.1f, 0, 0.2f));
            var metal = Mat("Chandelier Metal", null, new Color(0.88f, 0.85f, 0.82f), Vector2.one, null, 0.5f, metallic: 0.5f);
            var candle = Mat("Candle", null, new Color(0.95f, 0.93f, 0.9f), Vector2.one);
            var bulbOn = Mat("Chandelier Bulb", null, new Color(1f, 0.9f, 0.9f), Vector2.one, new Color(1f, 0.75f, 0.8f) * 2.5f);
            var bulbDead = Mat("Chandelier Bulb Dead", null, new Color(0.3f, 0.28f, 0.28f), Vector2.one);
            Prim(PrimitiveType.Cylinder, "Chain", g, new Vector3(0, H - 0.15f, 0), new Vector3(0.015f, 0.15f, 0.015f), metal);
            Prim(PrimitiveType.Sphere, "Body", g, new Vector3(0, H - 0.36f, 0), new Vector3(0.09f, 0.14f, 0.09f), metal);
            var on = new List<Renderer>();
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                var tip = new Vector3(Mathf.Cos(a) * 0.24f, H - 0.42f, Mathf.Sin(a) * 0.24f);
                Beam("Arm", g, new Vector3(0, H - 0.4f, 0), tip, 0.012f, 0.012f, metal);
                Prim(PrimitiveType.Cylinder, "Candle Cup", g, tip, new Vector3(0.05f, 0.01f, 0.05f), metal);
                Prim(PrimitiveType.Cylinder, "Candle", g, tip + Vector3.up * 0.045f, new Vector3(0.022f, 0.04f, 0.022f), candle);
                bool dead = i == 1 || i == 3;
                var b = Prim(PrimitiveType.Sphere, "Bulb", g, tip + Vector3.up * 0.105f, new Vector3(0.03f, 0.045f, 0.03f), dead ? bulbDead : bulbOn);
                if (!dead) on.Add(b.GetComponent<Renderer>());
            }
            var l = Point(lights, "Chandelier", g.TransformPoint(new Vector3(0, H - 0.5f, 0)), new Color(1f, 0.78f, 0.82f), 1.3f, 5.5f, true);
            var buzz = Audio(g, "Light Buzz", new Vector3(0, H - 0.4f, 0), "light_buzz.wav", 0.18f, true, true, 5f);
            ambience.Add(buzz);
            var fl = g.gameObject.AddComponent<FlickerLight>();
            fl.lights = new[] { l }; fl.bulbs = on.ToArray(); fl.bulbColor = new Color(1f, 0.75f, 0.8f) * 2.5f;
            fl.buzz = buzz; fl.mode = FlickerLight.Mode.Flicker; fl.eventRate = 0.3f;
        }

        static void Decor(Transform room)
        {
            var g = Group("Decor (stains, peeling, posters)", room, Vector3.zero);
            var water = Mat("Stain Water", "stain_water", Color.white, Vector2.one, clip: true, doubleSided: true);
            var mold = Mat("Stain Mold", "stain_mold", Color.white, Vector2.one, clip: true, doubleSided: true);
            var dark = Mat("Stain Dark", "stain_dark", Color.white, Vector2.one, clip: true, doubleSided: true);
            const float e = 0.006f;
            // walls
            Decal("Water Stain", g, new Vector3(0.9f, 2.1f, HZ - e), Vector3.zero, 1.1f, water);
            Decal("Mold", g, new Vector3(-2.2f, 2.4f, HZ - e), Vector3.zero, 0.45f, mold);
            Decal("Mold", g, new Vector3(-HX + e, 2.4f, 1.6f), new Vector3(0, -90, 0), 0.5f, mold);
            Decal("Water Stain", g, new Vector3(HX - e, 0.5f, 0.6f), new Vector3(0, 90, 0), 0.9f, water);
            Decal("Dark Stain", g, new Vector3(0.1f, 0.45f, -HZ + e), new Vector3(0, 180, 0), 0.8f, dark);
            Decal("Water Stain", g, new Vector3(-HX + e, 0.6f, -1.6f), new Vector3(0, -90, 0), 0.7f, water);
            // ceiling
            Decal("Ceiling Stain", g, new Vector3(-0.8f, H - e, 1.0f), new Vector3(-90, 0, 0), 1.4f, water);
            Decal("Ceiling Mold", g, new Vector3(HX - 0.5f, H - e, -HZ + 0.5f), new Vector3(-90, 0, 0), 1.0f, mold);
            Decal("Ceiling Stain", g, new Vector3(1.2f, H - e, 1.6f), new Vector3(-90, 30, 0), 0.9f, dark);
            // floor
            Decal("Floor Stain", g, new Vector3(0.6f, e, -1.0f), new Vector3(90, 0, 0), 0.6f, dark);

            // peeling wallpaper: a strip curling off the north wall above the canopy, and a ceiling flap hanging down
            var paper = Mat("Wallpaper Peel", "wallpaper_floral", Color.white, Vector2.one, doubleSided: true);
            var back = Mat("Paper Back", "paper_back", Color.white, Vector2.one, doubleSided: true);
            Quad("Peeling Wallpaper", g, new Vector3(-1.75f, 2.35f, HZ - 0.12f), new Vector2(0.5f, 0.6f), paper, new Vector3(-25, 0, 8));
            Quad("Peeling Back", g, new Vector3(-1.75f, 2.35f, HZ - 0.125f), new Vector2(0.5f, 0.6f), back, new Vector3(-25, 180, -8));
            Quad("Ceiling Flap", g, new Vector3(-0.3f, H - 0.22f, -0.6f), new Vector2(0.7f, 0.5f), Mat("Ceiling Flap", "ceiling", new Color(0.85f, 0.8f, 0.8f), Vector2.one, doubleSided: true), new Vector3(-50, 20, 0));
            Quad("Ceiling Hole", g, new Vector3(-0.3f, H - 0.004f, -0.45f), new Vector2(0.7f, 0.5f), Mat("Ceiling Hole", null, new Color(0.03f, 0.02f, 0.02f), Vector2.one, doubleSided: true), new Vector3(-90, 20, 0));

            // rug
            Prim(PrimitiveType.Cylinder, "Rug", g, new Vector3(0.05f, 0.006f, -0.4f), new Vector3(1.8f, 0.006f, 1.5f), Mat("Rug", "rug", Color.white, new Vector2(2, 2)));

            // posters + a framed photo (original designs)
            Quad("Poster", g, new Vector3(HX - e * 2, 1.55f, 1.8f), new Vector2(0.4f, 0.6f), Mat("Poster 0", "poster_0", Color.white, Vector2.one), new Vector3(0, 90, -3));
            Quad("Poster", g, new Vector3(-0.2f, 1.6f, -HZ + e * 2), new Vector2(0.4f, 0.6f), Mat("Poster 1", "poster_1", Color.white, Vector2.one), new Vector3(0, 180, 4));
            var fr = Group("Framed Photo", g, new Vector3(-1.66f, 1.55f, HZ - 0.02f));
            Box("Frame", fr, Vector3.zero, new Vector3(0.32f, 0.4f, 0.03f), Mat("Frame Dark", null, new Color(0.15f, 0.1f, 0.1f), Vector2.one), new Vector3(0, 0, -4));
            Quad("Photo", fr, new Vector3(0, 0, -0.017f), new Vector2(0.24f, 0.32f), Mat("Photo", "photo", Color.white, Vector2.one, doubleSided: true), new Vector3(0, 0, -4));
        }

        // ------------------------------------------------------------------ HUD + player

        static HUD BuildHUD(out Canvas canvas)
        {
            var go = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);
            scaler.matchWidthOrHeight = 0.5f;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Text MakeText(string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color color)
            {
                var t = new GameObject(name, typeof(RectTransform), typeof(Text));
                t.transform.SetParent(go.transform, false);
                var rt = (RectTransform)t.transform;
                rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot;
                rt.anchoredPosition = pos; rt.sizeDelta = size;
                var tx = t.GetComponent<Text>();
                tx.font = font; tx.fontSize = fontSize; tx.alignment = align; tx.color = color;
                tx.horizontalOverflow = HorizontalWrapMode.Wrap; tx.verticalOverflow = VerticalWrapMode.Overflow;
                tx.raycastTarget = false;
                return tx;
            }

            var hud = go.AddComponent<HUD>();
            hud.objective = MakeText("Objective", new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -20), new Vector2(700, 40), 20, TextAnchor.UpperLeft, new Color(1f, 0.97f, 0.98f));
            hud.prompt = MakeText("Prompt", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -42), new Vector2(800, 40), 17, TextAnchor.MiddleCenter, Color.white);
            hud.line = MakeText("Thought Line", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(860, 90), 19, TextAnchor.MiddleCenter, new Color(1f, 0.8f, 0.88f));
            hud.title = MakeText("Title", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 300), 28, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.9f, 0f));

            var dot = new GameObject("Crosshair", typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(go.transform, false);
            var drt = (RectTransform)dot.transform; drt.sizeDelta = new Vector2(3, 3);
            var img = dot.GetComponent<Image>(); img.color = new Color(1, 1, 1, 0.55f); img.raycastTarget = false;

            var fade = new GameObject("Fader", typeof(RectTransform), typeof(Image));
            fade.transform.SetParent(go.transform, false);
            var frt = (RectTransform)fade.transform; frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
            hud.fader = fade.GetComponent<Image>(); hud.fader.color = new Color(0, 0, 0, 0); hud.fader.raycastTarget = false; // the director fades it in from black at Start
            // title draws above the fader
            hud.title.transform.SetAsLastSibling();
            hud.line.transform.SetAsLastSibling();
            return hud;
        }

        static Transform Player(HUD hud, Canvas canvas, out PlayerInteractor interactor)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CART315FirstPerson/Prefabs/CART315 First Person.prefab");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.name = "Player";
            player.transform.position = new Vector3(0.75f, 0.05f, 0.55f);
            var look = new Vector3(-2.4f, 0, -0.75f) - player.transform.position; look.y = 0;
            player.transform.rotation = Quaternion.LookRotation(look);

            // heavy, slow body: no sprinting, no jumping
            var ctrl = player.GetComponent<FPController>();
            var so = new SerializedObject(ctrl);
            var speed = so.FindProperty("_movementSpeed"); if (speed != null) speed.floatValue = 1.9f;
            so.ApplyModifiedPropertiesWithoutUndo();
            var sprint = player.GetComponent<FPSprinting>(); if (sprint) sprint.enabled = false;
            var jump = player.GetComponent<FPJumping>(); if (jump) jump.enabled = false;

            var steps = player.GetComponent<FPFootsteps>();
            if (steps)
            {
                var fso = new SerializedObject(steps);
                var arr = fso.FindProperty("_sounds");
                arr.arraySize = 8;
                for (int i = 0; i < 8; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = Clip($"step_wood_0{i + 1}.wav");
                var step = fso.FindProperty("_stepDistance"); if (step != null) step.floatValue = 1.3f;
                var vol = fso.FindProperty("_volume"); if (vol != null) vol.floatValue = 0.3f;
                fso.ApplyModifiedPropertiesWithoutUndo();
            }

            var cam = player.GetComponentInChildren<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.05f;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;
            data.dithering = true;

            interactor = cam.gameObject.AddComponent<PlayerInteractor>();
            interactor.cam = cam;
            interactor.hud = hud;
            return player.transform;
        }

        // ------------------------------------------------------------------ helpers

        static Transform Group(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat,
            Vector3 euler = default, bool collider = false, bool castShadows = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat,
            Vector3 euler = default, bool collider = false, bool castShadows = true)
            => Prim(PrimitiveType.Cube, name, parent, pos, scale, mat, euler, collider, castShadows);

        static GameObject Quad(string name, Transform parent, Vector3 pos, Vector2 size, Material mat, Vector3 euler)
        {
            var q = Prim(PrimitiveType.Quad, name, parent, pos, new Vector3(size.x, size.y, 1), mat, euler, false, false);
            return q;
        }

        static void Decal(string name, Transform parent, Vector3 pos, Vector3 euler, float size, Material mat)
            => Quad(name, parent, pos, new Vector2(size, size), mat, euler);

        /// A thin box stretched between two local points (wires, drapes, tape). Its length runs along local X.
        static GameObject Beam(string name, Transform parent, Vector3 a, Vector3 b, float width, float thickness, Material mat, float tileLength = 0f)
        {
            var dir = b - a;
            var go = Prim(PrimitiveType.Cube, name, parent, (a + b) / 2f, new Vector3(dir.magnitude, thickness, width), mat);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.right, dir.normalized);
            if (tileLength > 0f)
            {
                // per-beam tiling so the tape's tick marks keep their real spacing
                var r = go.GetComponent<Renderer>();
                var m = Mat(mat.name + $" L{dir.magnitude:0.00}", null, Color.white, Vector2.one, copyFrom: mat);
                m.SetTextureScale("_BaseMap", new Vector2(dir.magnitude / tileLength, 1));
                r.sharedMaterial = m;
            }
            return go;
        }

        static Interactable Interact(Transform root, string verb, bool counts, float pad, params string[] lines)
        {
            var it = root.gameObject.AddComponent<Interactable>();
            it.verb = verb;
            it.countsTowardMirror = counts;
            it.lines = lines;

            var rs = root.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return it;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            b.Expand(new Vector3(0.05f, pad, 0.05f)); // taller boxes make floor items easy to look at
            if (pad > 0.1f) b.center += Vector3.up * pad * 0.5f;
            var bc = root.gameObject.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.center = root.InverseTransformPoint(b.center);
            var s = root.InverseTransformVector(b.size);
            bc.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            return it;
        }

        static Light Point(Transform parent, string name, Vector3 worldPos, Color color, float intensity, float range, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color; l.intensity = intensity; l.range = range;
            l.shadows = shadows ? LightShadows.Hard : LightShadows.None;
            l.shadowBias = 0.02f; l.shadowNormalBias = 0.3f;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            return l;
        }

        static Light Spot(Transform parent, string name, Vector3 worldPos, Vector3 target, Color color, float intensity, float range, float angle, bool shadows)
        {
            var l = Point(parent, name, worldPos, color, intensity, range, shadows);
            l.type = LightType.Spot;
            l.spotAngle = angle;
            l.transform.rotation = Quaternion.LookRotation(target - worldPos);
            return l;
        }

        static AudioSource Audio(Transform parent, string name, Vector3 localPos, string clip, float volume, bool loop, bool spatial, float maxDistance = 8f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var a = go.AddComponent<AudioSource>();
            a.clip = clip != null ? Clip(clip) : null;
            a.volume = volume;
            a.loop = loop;
            a.playOnAwake = loop && a.clip != null;
            a.spatialBlend = spatial ? 1f : 0f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = 0.6f;
            a.maxDistance = maxDistance;
            a.dopplerLevel = 0f;
            return a;
        }

        static AudioClip Clip(string file)
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/" + file);
            if (!c) Debug.LogWarning("[Pink Backrooms] Missing audio: " + file);
            return c;
        }

        static Texture2D Tex(string name)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + name + ".png");
            if (!t) Debug.LogWarning("[Pink Backrooms] Missing texture: " + name);
            return t;
        }

        /// Creates (or reuses) a URP Lit material asset. Flat, matte PS1-style by default:
        /// no specular highlights or environment reflections unless asked for.
        static Material Mat(string name, string tex, Color color, Vector2 tiling, Color? emission = null, float smoothness = 0f,
            bool clip = false, bool doubleSided = false, float metallic = 0f, bool reflections = false, Material copyFrom = null)
        {
            string key = name;
            if (copyFrom == null && tiling != Vector2.one) key += $" {tiling.x:0.##}x{tiling.y:0.##}";
            key = key.Replace("/", "-").Replace(":", "-");
            if (_mats.TryGetValue(key, out var cached)) return cached;

            string path = $"{MatDir}/{key}.mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = m == null;
            if (copyFrom != null)
            {
                if (isNew) m = new Material(copyFrom); else m.CopyPropertiesFromMaterial(copyFrom);
                m.shaderKeywords = copyFrom.shaderKeywords;
            }
            else
            {
                if (isNew) m = new Material(shader); else m.shader = shader;
                m.SetTexture("_BaseMap", tex != null ? Tex(tex) : null);
                m.SetTextureScale("_BaseMap", tiling);
                m.SetColor("_BaseColor", color);
                m.SetFloat("_Smoothness", smoothness);
                m.SetFloat("_Metallic", metallic);

                m.SetFloat("_SpecularHighlights", reflections || smoothness > 0.3f ? 1f : 0f);
                m.SetFloat("_EnvironmentReflections", reflections ? 1f : 0f);
                SetKeyword(m, "_SPECULARHIGHLIGHTS_OFF", !(reflections || smoothness > 0.3f));
                SetKeyword(m, "_ENVIRONMENTREFLECTIONS_OFF", !reflections);

                if (emission.HasValue)
                {
                    m.SetColor("_EmissionColor", emission.Value);
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                else
                {
                    m.SetColor("_EmissionColor", Color.black);
                    m.DisableKeyword("_EMISSION");
                }

                m.SetFloat("_AlphaClip", clip ? 1f : 0f);
                m.SetFloat("_Cutoff", 0.5f);
                SetKeyword(m, "_ALPHATEST_ON", clip);
                m.renderQueue = clip ? (int)RenderQueue.AlphaTest : -1;
                m.SetFloat("_Cull", doubleSided ? 0f : 2f);
            }
            m.name = key;
            if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
            _mats[key] = m;
            return m;
        }

        static void SetKeyword(Material m, string kw, bool on)
        {
            if (on) m.EnableKeyword(kw); else m.DisableKeyword(kw);
        }
    }
}
