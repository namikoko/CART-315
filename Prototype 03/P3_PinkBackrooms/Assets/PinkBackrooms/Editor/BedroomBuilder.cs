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

        [MenuItem("My Ana/2. Build Bedroom Scene", priority = 2)]
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
            ambience.Add(Audio(audio, "Room Drone", Vector3.up * 1.5f, "room_drone.ogg", 0.14f, true, false)); // quiet: just pressure in the ears

            Bed(props, lights, ambience);
            Nightstands(props, lights, ambience);
            Vanity(props, lights, ambience, out var mirrorLights, out var mirrorGlass);
            Scale(props, lights);
            WalkingPad(props, ambience);
            Weights(props);
            Wardrobe(props);
            Clothes(props);
            Takeout(props);
            Clutter(props, lights);
            HerThings(props, lights, ambience);
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
            dir.heartbeat = Audio(dirGo.transform, "Heartbeat", Vector3.zero, "heartbeat.wav", 0.16f, true, false);

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

            // East wall: no window. Nothing in here lets the outside in.
            Wall(room, "Wall East", new Vector3(HX + 0.1f, 0, 0), D, 0, H, true);

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
            // baseboards, chair rail and crown on all four walls
            foreach (var (y, h, d) in new[] { (0.06f, 0.12f, 0.025f), (Band, 0.05f, 0.03f), (H - 0.04f, 0.08f, 0.04f) })
            {
                Box("Trim N", t, new Vector3(0, y, HZ - d / 2), new Vector3(W, h, d), white);
                Box("Trim S", t, new Vector3(0, y, -HZ + d / 2), new Vector3(W, h, d), white);
                Box("Trim W", t, new Vector3(-HX + d / 2, y, 0), new Vector3(d, h, D), white);
                Box("Trim E", t, new Vector3(HX - d / 2, y, 0), new Vector3(d, h, D), white);
            }
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
                "too tired to go out there. too embarrassed.",
                "when i do go out i look perfect. nobody would know.");
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
                "i live here. most of the time.",
                "the sheet slipped off weeks ago. i never put it back.",
                "i'm tired all the time. lying here doesn't fix it.");

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
            Prim(PrimitiveType.Sphere, "Tissue", left, new Vector3(0.12f, 0.58f, 0.08f), new Vector3(0.06f, 0.04f, 0.05f), Mat("Tissue", "fabric_white", Color.white, Vector2.one));
            Prim(PrimitiveType.Sphere, "Tissue", props, new Vector3(-1.4f, 0.02f, 1.62f), new Vector3(0.06f, 0.04f, 0.05f), Mat("Tissue", "fabric_white", Color.white, Vector2.one));
            var lamp = Point(lights, "Bedside Lamp", left.TransformPoint(new Vector3(-0.05f, 0.85f, 0.03f)), new Color(1f, 0.55f, 0.65f), 1.1f, 3.2f, true);
            var fl = left.gameObject.AddComponent<FlickerLight>();
            fl.lights = new[] { lamp }; fl.bulbs = new[] { shadeGo.GetComponent<Renderer>() };
            fl.bulbColor = new Color(1f, 0.45f, 0.6f) * 0.6f; fl.mode = FlickerLight.Mode.Pulse; fl.pulseSpeed = 0.08f;

            // right: a second lamp, phone charger, and a little bluetooth speaker playing affirmations on loop
            var right = Stand("Nightstand Right", new Vector3(0.66f, 0, 1.97f));
            Prim(PrimitiveType.Cylinder, "Lamp Base", right, new Vector3(0.08f, 0.58f, 0.05f), new Vector3(0.1f, 0.02f, 0.1f), white);
            Prim(PrimitiveType.Cylinder, "Lamp Stem", right, new Vector3(0.08f, 0.72f, 0.05f), new Vector3(0.02f, 0.14f, 0.02f), brass);
            var shade2 = Prim(PrimitiveType.Cylinder, "Lamp Shade (tilted)", right, new Vector3(0.09f, 0.92f, 0.05f), new Vector3(0.24f, 0.09f, 0.24f), shade, new Vector3(-8, 0, -10));
            var lamp2 = Point(lights, "Bedside Lamp 2", right.TransformPoint(new Vector3(0.08f, 0.85f, 0.05f)), new Color(1f, 0.6f, 0.68f), 0.8f, 2.8f, true);
            var fl2 = right.gameObject.AddComponent<FlickerLight>();
            fl2.lights = new[] { lamp2 }; fl2.bulbs = new[] { shade2.GetComponent<Renderer>() };
            fl2.bulbColor = new Color(1f, 0.45f, 0.6f) * 0.6f; fl2.mode = FlickerLight.Mode.Flicker; fl2.eventRate = 0.12f;

            var spk = Group("Bluetooth Speaker", right, new Vector3(-0.1f, 0.56f, -0.06f));
            spk.localEulerAngles = new Vector3(0, -20, 0);
            var spkMat = Mat("Speaker Fabric", "fabric_pink_knit", new Color(0.95f, 0.75f, 0.82f), new Vector2(2, 1));
            var spkCap = Mat("Speaker Cap", null, new Color(0.92f, 0.88f, 0.9f), Vector2.one, null, 0.5f);
            Prim(PrimitiveType.Capsule, "Speaker Body", spk, new Vector3(0, 0.045f, 0), new Vector3(0.09f, 0.08f, 0.09f), spkMat, new Vector3(0, 0, 90));
            Prim(PrimitiveType.Cylinder, "Speaker End", spk, new Vector3(-0.085f, 0.045f, 0), new Vector3(0.085f, 0.006f, 0.085f), spkCap, new Vector3(0, 0, 90));
            Prim(PrimitiveType.Cylinder, "Speaker End", spk, new Vector3(0.085f, 0.045f, 0), new Vector3(0.085f, 0.006f, 0.085f), spkCap, new Vector3(0, 0, 90));
            var led = Prim(PrimitiveType.Sphere, "Speaker LED", spk, new Vector3(0, 0.09f, -0.02f), Vector3.one * 0.012f,
                Mat("Speaker LED", null, Color.black, Vector2.one, new Color(0.4f, 0.6f, 1f) * 3f));
            var ledGlow = Point(lights, "Speaker LED Glow", spk.TransformPoint(new Vector3(0, 0.12f, -0.03f)), new Color(0.45f, 0.6f, 1f), 0.25f, 0.6f, false);
            var pulse = spk.gameObject.AddComponent<FlickerLight>();
            pulse.lights = new[] { ledGlow }; pulse.bulbs = new[] { led.GetComponent<Renderer>() };
            pulse.bulbColor = new Color(0.4f, 0.6f, 1f) * 3f; pulse.mode = FlickerLight.Mode.Pulse; pulse.pulseSpeed = 0.5f;
            var aff = Audio(spk, "Affirmations (speaker)", new Vector3(0, 0.05f, 0), "affirmations_speaker.ogg", 0.55f, true, true, 6f);
            aff.minDistance = 1.0f;      // soft: you hear it properly when you're near the bed, half-heard from the door
            aff.spatialBlend = 0.75f;    // still sounds like it's coming from the nightstand, but never drops out
            ambience.Add(aff);
            Interact(spk, "listen to the speaker", true, 0.1f,
                "i fall asleep to it every night.",
                "hoping it hypnotizes me into a better attitude.",
                "everything i want wants me more. right.");
            // phone charger cable trailing off the edge
            var cable = Mat("Cable", null, new Color(0.92f, 0.9f, 0.9f), Vector2.one);
            Beam("Charger Cable", right, new Vector3(0.12f, 0.565f, -0.12f), new Vector3(0.2f, 0.565f, -0.2f), 0.006f, 0.006f, cable);
            Beam("Charger Cable", right, new Vector3(0.2f, 0.565f, -0.2f), new Vector3(0.24f, 0.25f, -0.24f), 0.006f, 0.006f, cable);
            Beam("Charger Cable", right, new Vector3(0.24f, 0.25f, -0.24f), new Vector3(0.1f, 0.005f, -0.4f), 0.006f, 0.006f, cable);
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
                "when i go out i do everything. makeup, best outfit.",
                "i want to look normal. like i have it under control. i don't.",
                "i haven't showered in over a week. so i drown myself in perfume.");

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
            it.lockedLine = "not yet. i check every angle every day. not yet.";
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
                "i measure my body. every part.",
                "looking for any difference. any progress.",
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
                "when i wake up. after the bathroom. midday. before bed.",
                "every day. i write it all down.",
                "it doesn't need to show the number. i already know it.");
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
                "i work out so much. so the scale stays down.",
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
                "my heart pounds out of my chest sometimes.",
                "anxious. tired. i keep going anyway.");
        }

        // ------------------------------------------------------------------ wardrobe, clothes, takeout

        static void Wardrobe(Transform props)
        {
            // Closed. Whatever's in there stays in there; the clothes that escaped are piled in front.
            var g = Group("Wardrobe", props, new Vector3(1.55f, 0, -HZ + 0.3f));
            var white = Mat("Furniture White", "paint_white", Color.white, Vector2.one);
            var line = Mat("Drawer Line", null, new Color(0.25f, 0.2f, 0.22f), Vector2.one);
            var brass = Mat("Brass", null, new Color(0.55f, 0.45f, 0.3f), Vector2.one, null, 0.6f, metallic: 0.8f);
            Box("Body", g, new Vector3(0, 1.0f, 0), new Vector3(1.1f, 2.0f, 0.6f), white, collider: true);
            Box("Crown", g, new Vector3(0, 2.02f, 0.01f), new Vector3(1.16f, 0.06f, 0.64f), white);
            Box("Base", g, new Vector3(0, 0.05f, 0.01f), new Vector3(1.14f, 0.1f, 0.62f), white);
            // two doors, a seam down the middle, raised panels, knobs
            Box("Door Seam", g, new Vector3(0, 1.08f, 0.301f), new Vector3(0.008f, 1.8f, 0.004f), line);
            foreach (var x in new[] { -0.27f, 0.27f })
            {
                Box("Panel", g, new Vector3(x, 1.45f, 0.305f), new Vector3(0.38f, 0.75f, 0.012f), white);
                Box("Panel", g, new Vector3(x, 0.6f, 0.305f), new Vector3(0.38f, 0.65f, 0.012f), white);
                Prim(PrimitiveType.Sphere, "Knob", g, new Vector3(x * 0.15f, 1.05f, 0.32f), Vector3.one * 0.035f, brass);
            }
            // one sleeve caught in the door
            Box("Sleeve (caught in door)", g, new Vector3(0.02f, 0.75f, 0.33f), new Vector3(0.07f, 0.45f, 0.02f),
                Mat("Cloth fabric_pink_knit", "fabric_pink_knit", Color.white, Vector2.one, doubleSided: true), new Vector3(8, 0, 6));
            Interact(g, "open the wardrobe", false, 0.05f,
                "i don't open it anymore.",
                "everything in there was bought for a different body.");
        }

        static void Clothes(Transform props)
        {
            // main pile spilling out of the wardrobe
            var pile = Group("Clothes Pile", props, new Vector3(1.55f, 0, -1.2f));
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
                "i clean. it comes back. it always comes back.",
                "when i get up i'm already tired. i never have the energy.",
                "i can never keep anything neat.");

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
                "sometimes i don't eat for so long that i cave.",
                "i order takeout and i stuff my face. it's embarrassing.",
                "then it starts over.");

            // one more bag by the bed
            var b2 = Group("Takeout Bag (by bed)", props, new Vector3(0.55f, 0, 0.45f));
            Box("Bag (crumpled)", b2, new Vector3(0, 0.1f, 0), new Vector3(0.22f, 0.2f, 0.14f), kraft, new Vector3(8, -50, 12));
        }

        // ------------------------------------------------------------------ clutter

        /// Everyday mess around the edges of the room. Kept off the walking path so the key objects still read.
        static void Clutter(Transform props, Transform lights)
        {
            var g = Group("Clutter", props, Vector3.zero);
            var rnd = new System.Random(21);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var white = Mat("Furniture White", "paint_white", Color.white, Vector2.one);
            var tissue = Mat("Tissue", "fabric_white", Color.white, Vector2.one);
            var paper = Mat("Receipt", "fabric_white", Color.white, Vector2.one);
            var bottle = Mat("Water Bottle", null, new Color(0.7f, 0.82f, 0.9f), Vector2.one, null, 0.85f);
            var cap = Mat("Bottle Cap", null, new Color(0.3f, 0.5f, 0.9f), Vector2.one, null, 0.4f);
            var can = Mat("Can Pink", null, new Color(0.95f, 0.45f, 0.65f), Vector2.one, null, 0.7f, metallic: 0.6f);
            var can2 = Mat("Can Silver", null, new Color(0.75f, 0.75f, 0.8f), Vector2.one, null, 0.7f, metallic: 0.7f);
            var cupMat = Mat("Cup", null, new Color(0.95f, 0.85f, 0.88f), Vector2.one, null, 0.3f);
            var metal = Mat("Weight Bar", null, new Color(0.6f, 0.6f, 0.62f), Vector2.one, null, 0.7f, metallic: 0.8f);

            // tissues and receipts drifting around the bed and nightstands
            Vector3[] tissueSpots = { new(-1.25f, 0, 1.75f), new(-1.5f, 0, 1.3f), new(-1.1f, 0, 0.3f), new(0.45f, 0, 1.4f), new(0.95f, 0, 1.7f), new(-1.9f, 0, 1.6f), new(0.35f, 0, 0.1f) };
            foreach (var p in tissueSpots)
                Prim(PrimitiveType.Sphere, "Tissue", g, p + new Vector3(R(-0.1f, 0.1f), 0.02f, R(-0.1f, 0.1f)), new Vector3(R(0.05f, 0.08f), 0.04f, R(0.05f, 0.07f)), tissue, new Vector3(0, R(0, 360), 0));
            for (int i = 0; i < 6; i++)
                Box("Paper", g, new Vector3(R(-2.2f, 2.2f), 0.002f, R(-2.0f, -1.3f)), new Vector3(R(0.08f, 0.2f), 0.002f, R(0.12f, 0.28f)), paper, new Vector3(0, R(0, 360), 0));

            // bottles, cans and cups
            void Bottle(Vector3 p, bool down)
            {
                var b = Group("Water Bottle", g, p); b.localEulerAngles = down ? new Vector3(90, R(0, 360), 0) : Vector3.zero;
                Prim(PrimitiveType.Cylinder, "Bottle", b, new Vector3(0, 0.11f, 0), new Vector3(0.065f, 0.11f, 0.065f), bottle);
                Prim(PrimitiveType.Cylinder, "Cap", b, new Vector3(0, 0.235f, 0), new Vector3(0.03f, 0.015f, 0.03f), cap);
            }
            Bottle(new Vector3(0.4f, 0.035f, 0.75f), true);
            Bottle(new Vector3(-1.3f, 0f, 1.95f), false);
            Bottle(new Vector3(1.05f, 0f, 1.85f), false);
            Bottle(new Vector3(-2.15f, 0.035f, 0.3f), true);
            Prim(PrimitiveType.Cylinder, "Can", g, new Vector3(1.05f, 0.033f, 1.3f), new Vector3(0.066f, 0.06f, 0.066f), can, new Vector3(90, 30, 0));
            Prim(PrimitiveType.Cylinder, "Can", g, new Vector3(-1.0f, 0.06f, -1.85f), new Vector3(0.066f, 0.06f, 0.066f), can2);
            Prim(PrimitiveType.Cylinder, "Can (crushed)", g, new Vector3(1.9f, 0.02f, 0.95f), new Vector3(0.07f, 0.02f, 0.07f), can, new Vector3(0, 0, 8));

            // laundry basket overflowing in the corner
            var basket = Group("Laundry Basket", g, new Vector3(2.12f, 0, 1.78f));
            basket.localEulerAngles = new Vector3(0, 15, 0);
            var wicker = Mat("Basket", "plank", new Color(0.95f, 0.85f, 0.88f), new Vector2(2, 1));
            Prim(PrimitiveType.Cylinder, "Basket", basket, new Vector3(0, 0.22f, 0), new Vector3(0.48f, 0.22f, 0.4f), wicker, Vector3.zero, collider: true);
            string[] f = { "fabric_white", "fabric_pink_knit", "fabric_black", "fabric_dots", "fabric_lilac", "fabric_plaid" };
            for (int i = 0; i < 16; i++)
                Prim(PrimitiveType.Sphere, "Laundry", basket, new Vector3(R(-0.2f, 0.2f), 0.42f + i * 0.035f, R(-0.16f, 0.16f)), new Vector3(R(0.18f, 0.3f), R(0.08f, 0.14f), R(0.15f, 0.25f)), Mat("Cloth " + f[i % f.Length], f[i % f.Length], Color.white, Vector2.one, doubleSided: true), new Vector3(R(-15, 15), R(0, 360), R(-15, 15)));
            Box("Laundry (spilling)", basket, new Vector3(-0.32f, 0.2f, 0.05f), new Vector3(0.03f, 0.35f, 0.25f), Mat("Cloth fabric_dots", "fabric_dots", Color.white, Vector2.one, doubleSided: true), new Vector3(0, 0, 25));
            Box("Laundry (floor)", g, new Vector3(1.75f, 0.012f, 1.6f), new Vector3(0.35f, 0.02f, 0.3f), Mat("Cloth fabric_pink_knit", "fabric_pink_knit", Color.white, Vector2.one, doubleSided: true), new Vector3(0, 40, 0));
            for (int i = 0; i < 6; i++)
                Prim(PrimitiveType.Sphere, "Laundry (spilled over)", g, new Vector3(R(1.55f, 2.3f), 0.04f, R(1.2f, 1.6f)), new Vector3(R(0.2f, 0.32f), R(0.06f, 0.11f), R(0.18f, 0.28f)), Mat("Cloth " + f[i % f.Length], f[i % f.Length], Color.white, Vector2.one, doubleSided: true), new Vector3(R(-8, 8), R(0, 360), R(-8, 8)));
            Interact(basket, "look at the hamper", false, 0.2f, "i don't remember the last time i did laundry.");

            // shoes kicked off by the door
            void Shoe(Vector3 p, float yaw, Color c, bool tipped)
            {
                var sh = Group("Shoe", g, p); sh.localEulerAngles = new Vector3(0, yaw, tipped ? 80 : 0);
                var m = Mat("Shoe " + ColorUtility.ToHtmlStringRGB(c), "fabric_white", c, Vector2.one, null, 0.2f);
                Box("Sole", sh, new Vector3(0, 0.015f, 0), new Vector3(0.1f, 0.03f, 0.26f), Mat("Sole", null, new Color(0.95f, 0.93f, 0.92f), Vector2.one));
                Prim(PrimitiveType.Sphere, "Upper", sh, new Vector3(0, 0.05f, -0.02f), new Vector3(0.1f, 0.08f, 0.22f), m);
            }
            Shoe(new Vector3(-0.85f, 0, -1.95f), 20, new Color(1f, 0.8f, 0.86f), false);
            Shoe(new Vector3(-0.65f, 0, -1.75f), 95, new Color(1f, 0.8f, 0.86f), true);
            Shoe(new Vector3(0.75f, 0, -1.95f), -40, new Color(0.95f, 0.95f, 0.97f), false);
            Shoe(new Vector3(-2.2f, 0, -1.95f), 60, new Color(0.3f, 0.28f, 0.3f), true);

            // backpack slumped against the wall by the door
            var bag = Group("Backpack", g, new Vector3(-0.85f, 0, -2.08f));
            var bp = Mat("Backpack", "fabric_lilac", new Color(0.9f, 0.85f, 0.95f), Vector2.one);
            Prim(PrimitiveType.Capsule, "Body", bag, new Vector3(0, 0.2f, 0), new Vector3(0.32f, 0.2f, 0.18f), bp, new Vector3(-12, 0, 0), collider: true);
            Box("Pocket", bag, new Vector3(0, 0.14f, 0.1f), new Vector3(0.22f, 0.14f, 0.05f), bp, new Vector3(-12, 0, 0));
            Prim(PrimitiveType.Sphere, "Charm", bag, new Vector3(0.12f, 0.32f, 0.11f), Vector3.one * 0.04f, Mat("Bow", null, new Color(0.95f, 0.55f, 0.7f), Vector2.one, null, 0.4f));

            // pink shopping bags by the vanity
            var shop = Mat("Shopping Bag", "paint_white", new Color(1f, 0.72f, 0.82f), Vector2.one, null, 0.3f);
            var shop2 = Mat("Shopping Bag White", "paint_white", new Color(0.97f, 0.95f, 0.95f), Vector2.one, null, 0.3f);
            foreach (var (p, yaw, m, tip) in new[] { (new Vector3(-2.15f, 0, -1.55f), 20f, shop, 0f), (new Vector3(-1.85f, 0, -1.85f), -30f, shop2, 0f), (new Vector3(-1.75f, 0, -1.45f), 70f, shop, 85f) })
            {
                var sb = Group("Shopping Bag", g, p); sb.localEulerAngles = new Vector3(tip, yaw, 0);
                Box("Bag", sb, new Vector3(0, 0.17f, 0), new Vector3(0.3f, 0.34f, 0.12f), m);
                Beam("Handle", sb, new Vector3(-0.07f, 0.34f, 0), new Vector3(0, 0.42f, 0), 0.01f, 0.01f, m);
                Beam("Handle", sb, new Vector3(0, 0.42f, 0), new Vector3(0.07f, 0.34f, 0), 0.01f, 0.01f, m);
                Box("Tissue Paper", sb, new Vector3(0, 0.35f, 0), new Vector3(0.22f, 0.06f, 0.08f), tissue, new Vector3(0, 0, 8));
            }

            // books and magazines stacked by the bed, one open face down
            var books = Group("Books", g, new Vector3(1.05f, 0, 1.55f));
            Color[] bc = { new(0.8f, 0.5f, 0.6f), new(0.9f, 0.85f, 0.8f), new(0.55f, 0.5f, 0.7f), new(0.95f, 0.7f, 0.75f) };
            for (int i = 0; i < 4; i++)
                Box("Book", books, new Vector3(R(-0.02f, 0.02f), 0.02f + i * 0.04f, R(-0.02f, 0.02f)), new Vector3(R(0.16f, 0.22f), 0.035f, R(0.22f, 0.28f)), Mat("Book " + i, "paint_white", bc[i], Vector2.one), new Vector3(0, R(-15, 15), 0));
            Box("Book (open)", g, new Vector3(0.65f, 0.025f, 1.1f), new Vector3(0.34f, 0.03f, 0.24f), Mat("Book 2", "paint_white", bc[2], Vector2.one), new Vector3(0, 25, 4));

            // hangers dropped in front of the wardrobe
            for (int i = 0; i < 3; i++)
            {
                var h = Group("Hanger (floor)", g, new Vector3(1.1f + i * 0.18f, 0.008f, -1.75f + R(-0.1f, 0.1f)));
                h.localEulerAngles = new Vector3(0, R(0, 360), 0);
                Beam("Hanger", h, new Vector3(-0.17f, 0, 0), new Vector3(0, 0, 0.08f), 0.008f, 0.008f, metal);
                Beam("Hanger", h, new Vector3(0, 0, 0.08f), new Vector3(0.17f, 0, 0), 0.008f, 0.008f, metal);
                Beam("Hanger", h, new Vector3(-0.17f, 0, 0), new Vector3(0.17f, 0, 0), 0.008f, 0.008f, metal);
            }

            // a pillow and a plush that fell off the bed
            Prim(PrimitiveType.Sphere, "Pillow (fallen)", g, new Vector3(0.48f, 0.08f, 0.85f), new Vector3(0.5f, 0.15f, 0.34f), Mat("Pillow Pink", "fabric_dots", Color.white, new Vector2(2, 1)), new Vector3(0, 30, 12));
            var plush = Group("Plush (face down)", g, new Vector3(-1.35f, 0.07f, 0.65f));
            plush.localEulerAngles = new Vector3(80, 40, 0);
            var fur = Mat("Plush Lilac", "fabric_white", new Color(0.85f, 0.8f, 0.95f), Vector2.one);
            Prim(PrimitiveType.Sphere, "Body", plush, Vector3.zero, new Vector3(0.16f, 0.18f, 0.14f), fur);
            Prim(PrimitiveType.Sphere, "Head", plush, new Vector3(0, 0.13f, 0), Vector3.one * 0.13f, fur);
            Prim(PrimitiveType.Sphere, "Ear", plush, new Vector3(-0.05f, 0.2f, 0), Vector3.one * 0.05f, fur);
            Prim(PrimitiveType.Sphere, "Ear", plush, new Vector3(0.05f, 0.2f, 0), Vector3.one * 0.05f, fur);

            // phone on the bed, screen lighting up the sheets
            var phone = Group("Phone", g, new Vector3(-0.3f, 0.6f, 0.95f));
            phone.localEulerAngles = new Vector3(0, 35, 0);
            Box("Phone", phone, Vector3.zero, new Vector3(0.075f, 0.01f, 0.15f), Mat("Phone Case", null, new Color(0.95f, 0.7f, 0.8f), Vector2.one, null, 0.5f));
            Box("Screen", phone, new Vector3(0, 0.0055f, 0), new Vector3(0.065f, 0.002f, 0.14f), Mat("Phone Screen", null, Color.black, Vector2.one, new Color(0.7f, 0.75f, 1f) * 1.2f));
            Point(lights, "Phone Glow", phone.TransformPoint(new Vector3(0, 0.12f, 0)), new Color(0.65f, 0.72f, 1f), 0.3f, 0.7f, false);

            // scrunchies and a hair brush on the floor by the vanity
            var scrunch = Mat("Scrunchie", "fabric_pink_knit", Color.white, Vector2.one);
            Prim(PrimitiveType.Sphere, "Scrunchie", g, new Vector3(-1.95f, 0.015f, 0.05f), new Vector3(0.07f, 0.03f, 0.07f), scrunch);
            Prim(PrimitiveType.Sphere, "Scrunchie", g, new Vector3(-1.2f, 0.015f, -0.35f), new Vector3(0.07f, 0.03f, 0.07f), Mat("Scrunchie Black", "fabric_black", Color.white, Vector2.one));
            var brush = Group("Hair Brush", g, new Vector3(-2.0f, 0.02f, -1.15f)); brush.localEulerAngles = new Vector3(0, 50, 0);
            Box("Head", brush, new Vector3(0, 0, 0.06f), new Vector3(0.07f, 0.025f, 0.1f), Mat("Brush Pink", null, new Color(0.95f, 0.65f, 0.75f), Vector2.one, null, 0.4f));
            Box("Handle", brush, new Vector3(0, 0, -0.06f), new Vector3(0.03f, 0.02f, 0.13f), Mat("Brush Pink", null, new Color(0.95f, 0.65f, 0.75f), Vector2.one, null, 0.4f));
        }

        // ------------------------------------------------------------------ her things

        /// The specific, personal things: the laptop, the meds, the weed, the cups, the stain, the trash.
        static void HerThings(Transform props, Transform lights, List<AudioSource> ambience)
        {
            var g = Group("Her Things", props, Vector3.zero);
            var rnd = new System.Random(77);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var white = Mat("Furniture White", "paint_white", Color.white, Vector2.one);
            var leftStand = new Vector3(-1.66f, 0, 1.97f);
            var rightStand = new Vector3(0.66f, 0, 1.97f);
            const float standTop = 0.56f;

            // --- period stain on the bare part of the mattress
            Decal("Period Stain", g, new Vector3(-0.15f, 0.583f, 0.5f), new Vector3(90, 25, 0), 0.32f,
                Mat("Stain Blood", "stain_blood", Color.white, Vector2.one, clip: true, doubleSided: true));

            // --- macbook open on the bed, the only screen in the room
            var mac = Group("Laptop", g, new Vector3(-0.62f, 0.6f, 1.28f));
            mac.localEulerAngles = new Vector3(0, 12, -3);
            var alu = Mat("Laptop Body", null, new Color(0.88f, 0.78f, 0.8f), Vector2.one, null, 0.55f, metallic: 0.6f);
            Box("Base", mac, new Vector3(0, 0.008f, 0), new Vector3(0.31f, 0.015f, 0.22f), alu);
            Box("Keyboard", mac, new Vector3(0, 0.0165f, 0.02f), new Vector3(0.27f, 0.002f, 0.1f), Mat("Keys", null, new Color(0.15f, 0.13f, 0.15f), Vector2.one));
            var lid = Group("Lid", mac, new Vector3(0, 0.015f, 0.11f));
            lid.localEulerAngles = new Vector3(-75, 0, 0); // open, leaning back
            Box("Lid", lid, new Vector3(0, 0, 0.105f), new Vector3(0.31f, 0.008f, 0.21f), alu);
            var scrMat = Mat("Laptop Screen", "laptop_screen", Color.white, Vector2.one, Color.white * 1.3f);
            scrMat.SetTexture("_EmissionMap", Tex("laptop_screen"));
            EditorUtility.SetDirty(scrMat);
            Quad("Screen", lid, new Vector3(0, 0.0045f, 0.105f), new Vector2(0.28f, 0.18f), scrMat, new Vector3(90, 0, 0));
            Point(lights, "Laptop Glow", mac.TransformPoint(new Vector3(0, 0.15f, -0.25f)), new Color(0.85f, 0.7f, 1f), 0.6f, 1.6f, false);
            Interact(mac, "look at the laptop", true, 0.1f,
                "it's my tv. my computer. my everything.",
                "i'm on it all the flipping time.",
                "i'm just a girl.");

            // --- left nightstand: prescription bottles and tea
            var ln = Group("Meds and Tea", g, leftStand + new Vector3(0, standTop, 0));
            var amber = Mat("Pill Bottle", null, new Color(0.95f, 0.55f, 0.15f), Vector2.one, new Color(0.25f, 0.1f, 0f), 0.85f);
            var capW = Mat("Pill Cap", null, new Color(0.96f, 0.96f, 0.96f), Vector2.one, null, 0.3f);
            var label = Mat("Pill Label", "fabric_white", Color.white, Vector2.one);
            void PillBottle(Vector3 p, float yaw, bool tipped)
            {
                var b = Group("Prescription Bottle", ln, p); b.localEulerAngles = new Vector3(tipped ? 90 : 0, yaw, 0);
                Prim(PrimitiveType.Cylinder, "Bottle", b, new Vector3(0, 0.045f, 0), new Vector3(0.045f, 0.045f, 0.045f), amber);
                Prim(PrimitiveType.Cylinder, "Label", b, new Vector3(0, 0.045f, 0), new Vector3(0.0465f, 0.025f, 0.0465f), label);
                Prim(PrimitiveType.Cylinder, "Cap", b, new Vector3(0, 0.095f, 0), new Vector3(0.05f, 0.012f, 0.05f), capW);
            }
            PillBottle(new Vector3(0.15f, 0, -0.1f), 20, false);   // vyvanse
            PillBottle(new Vector3(0.08f, 0.025f, -0.15f), 70, true); // fluoxetine
            var cup = Mat("Tea Cup", null, new Color(0.97f, 0.88f, 0.92f), Vector2.one, null, 0.4f);
            Prim(PrimitiveType.Cylinder, "Tea Cup", ln, new Vector3(-0.15f, 0.045f, -0.12f), new Vector3(0.08f, 0.045f, 0.08f), cup);
            Prim(PrimitiveType.Cylinder, "Tea", ln, new Vector3(-0.15f, 0.085f, -0.12f), new Vector3(0.07f, 0.002f, 0.07f), Mat("Tea", null, new Color(0.45f, 0.25f, 0.12f), Vector2.one, null, 0.8f));
            Beam("Tea Bag String", ln, new Vector3(-0.13f, 0.09f, -0.12f), new Vector3(-0.1f, 0.0f, -0.17f), 0.003f, 0.003f, Mat("String", null, Color.white, Vector2.one));
            Box("Tea Bag Tag", ln, new Vector3(-0.1f, 0.005f, -0.18f), new Vector3(0.025f, 0.004f, 0.03f), Mat("Tag", null, new Color(0.95f, 0.7f, 0.8f), Vector2.one));
            Interact(ln, "look at the bottles", true, 0.12f,
                "vyvanse. fluoxetine.",
                "one for the binging. one for the depression.",
                "my heart beats out of my chest sometimes.");

            // --- big pink tumbler on the floor by the bed
            var tum = Group("Pink Tumbler", g, new Vector3(-1.3f, 0, 1.6f));
            var pink = Mat("Tumbler Pink", null, new Color(0.98f, 0.6f, 0.75f), Vector2.one, null, 0.45f, metallic: 0.3f);
            Prim(PrimitiveType.Cylinder, "Cup", tum, new Vector3(0, 0.17f, 0), new Vector3(0.1f, 0.12f, 0.1f), pink, Vector3.zero, collider: true);
            Prim(PrimitiveType.Cylinder, "Base", tum, new Vector3(0, 0.03f, 0), new Vector3(0.075f, 0.03f, 0.075f), pink);
            Prim(PrimitiveType.Cylinder, "Lid", tum, new Vector3(0, 0.3f, 0), new Vector3(0.105f, 0.012f, 0.105f), Mat("Tumbler Lid", null, new Color(0.95f, 0.92f, 0.94f), Vector2.one, null, 0.4f));
            Prim(PrimitiveType.Cylinder, "Straw", tum, new Vector3(0.01f, 0.38f, 0), new Vector3(0.012f, 0.09f, 0.012f), Mat("Straw", null, new Color(0.95f, 0.4f, 0.6f), Vector2.one), new Vector3(0, 0, 6));
            Beam("Handle", tum, new Vector3(0.055f, 0.25f, 0), new Vector3(0.11f, 0.2f, 0), 0.025f, 0.02f, pink);
            Beam("Handle", tum, new Vector3(0.11f, 0.2f, 0), new Vector3(0.11f, 0.1f, 0), 0.025f, 0.02f, pink);
            Beam("Handle", tum, new Vector3(0.11f, 0.1f, 0), new Vector3(0.055f, 0.07f, 0), 0.025f, 0.02f, pink);
            Interact(tum, "look at the tumbler", true, 0.2f,
                "i drink from it all day.",
                "water, water, water. it's never enough.");

            // --- right nightstand: grinder with residue, dab pen
            var rn = Group("Weed", g, rightStand + new Vector3(0, standTop, 0));
            var metalPink = Mat("Grinder", null, new Color(0.9f, 0.6f, 0.72f), Vector2.one, null, 0.6f, metallic: 0.7f);
            Prim(PrimitiveType.Cylinder, "Grinder", rn, new Vector3(-0.15f, 0.02f, 0.13f), new Vector3(0.065f, 0.02f, 0.065f), metalPink);
            Prim(PrimitiveType.Cylinder, "Grinder Lid (off)", rn, new Vector3(-0.07f, 0.008f, 0.16f), new Vector3(0.065f, 0.008f, 0.065f), metalPink, new Vector3(0, 0, 6));
            var weed = Mat("Residue", null, new Color(0.35f, 0.42f, 0.2f), Vector2.one);
            var weed2 = Mat("Residue Dark", null, new Color(0.25f, 0.28f, 0.14f), Vector2.one);
            for (int i = 0; i < 18; i++)
                Box("Residue", rn, new Vector3(R(-0.2f, -0.02f), 0.002f, R(0.05f, 0.2f)), Vector3.one * R(0.006f, 0.012f), i % 2 == 0 ? weed : weed2, new Vector3(0, R(0, 90), 0));
            var pen = Group("Dab Pen", rn, new Vector3(0.02f, 0.008f, -0.15f)); pen.localEulerAngles = new Vector3(0, 70, 90);
            Prim(PrimitiveType.Cylinder, "Pen Body", pen, Vector3.zero, new Vector3(0.016f, 0.05f, 0.016f), Mat("Pen Body", null, new Color(0.2f, 0.18f, 0.22f), Vector2.one, null, 0.6f, metallic: 0.5f));
            Prim(PrimitiveType.Cylinder, "Cartridge", pen, new Vector3(0, 0.065f, 0), new Vector3(0.014f, 0.016f, 0.014f), Mat("Cartridge", null, new Color(0.85f, 0.6f, 0.2f), Vector2.one, new Color(0.15f, 0.08f, 0f), 0.9f));
            Prim(PrimitiveType.Sphere, "LED", pen, new Vector3(0, -0.05f, 0), Vector3.one * 0.008f, Mat("Pen LED", null, Color.black, Vector2.one, new Color(1f, 0.3f, 0.6f) * 3f));
            Interact(rn, "look at the grinder", false, 0.1f, "it helps me sleep. it helps me not feel anything.");

            // --- takeout cups filled with pee, and incense to cover the smell
            var cupRed = Mat("Fast Food Cup", null, new Color(0.85f, 0.2f, 0.2f), Vector2.one, null, 0.4f);
            var cupWhite = Mat("Fast Food Cup White", null, new Color(0.96f, 0.95f, 0.93f), Vector2.one, null, 0.4f);
            var lidMat = Mat("Cup Lid", null, new Color(0.95f, 0.95f, 0.95f), Vector2.one, null, 0.5f);
            var pee = Mat("Pee", null, new Color(0.85f, 0.7f, 0.15f), Vector2.one, new Color(0.08f, 0.06f, 0f), 0.9f);
            var cups = Group("Cups", g, new Vector3(-1.8f, 0, 1.45f));
            foreach (var (p, lidOn) in new[] { (new Vector3(0, 0, 0), false), (new Vector3(-0.14f, 0, 0.12f), true), (new Vector3(0.1f, 0, 0.16f), false) })
            {
                var c = Group("Takeout Cup", cups, p);
                Prim(PrimitiveType.Cylinder, "Cup", c, new Vector3(0, 0.09f, 0), new Vector3(0.09f, 0.09f, 0.09f), cupRed);
                Prim(PrimitiveType.Cylinder, "Band", c, new Vector3(0, 0.12f, 0), new Vector3(0.092f, 0.025f, 0.092f), cupWhite);
                if (lidOn)
                {
                    Prim(PrimitiveType.Cylinder, "Lid", c, new Vector3(0, 0.185f, 0), new Vector3(0.095f, 0.006f, 0.095f), lidMat);
                    Prim(PrimitiveType.Cylinder, "Straw", c, new Vector3(0.01f, 0.24f, 0), new Vector3(0.008f, 0.07f, 0.008f), lidMat, new Vector3(0, 0, 8));
                }
                else Prim(PrimitiveType.Cylinder, "Liquid", c, new Vector3(0, 0.16f, 0), new Vector3(0.082f, 0.002f, 0.082f), pee);
            }
            Interact(cups, "look at the cups", true, 0.2f,
                "sometimes i'm too tired and too embarrassed to leave the room.",
                "so i pee in cups. it stinks up the room.",
                "so i burn incense.");

            var inc = Group("Incense", g, new Vector3(-HX + 0.23f, 0.76f, -0.6f) + new Vector3(0.15f, 0, 0.27f));
            inc.localEulerAngles = new Vector3(0, 30, 0);
            Box("Holder", inc, new Vector3(0, 0.006f, 0), new Vector3(0.03f, 0.012f, 0.24f), Mat("Incense Holder", null, new Color(0.55f, 0.38f, 0.28f), Vector2.one));
            Beam("Stick", inc, new Vector3(0, 0.012f, -0.1f), new Vector3(0, 0.14f, 0.08f), 0.004f, 0.004f, Mat("Incense Stick", null, new Color(0.3f, 0.15f, 0.1f), Vector2.one));
            var ember = Prim(PrimitiveType.Sphere, "Ember", inc, new Vector3(0, 0.142f, 0.083f), Vector3.one * 0.008f, Mat("Ember", null, Color.black, Vector2.one, new Color(1f, 0.35f, 0.1f) * 4f));
            Box("Ash", inc, new Vector3(0, 0.013f, 0.06f), new Vector3(0.02f, 0.002f, 0.05f), Mat("Ash", null, new Color(0.6f, 0.58f, 0.56f), Vector2.one));
            Smoke(inc, ember.transform.localPosition);
            Interact(inc, "look at the incense", false, 0.15f, "it covers the smell. mostly.");

            // --- diet soda cans with straws, meal replacement shakes
            var can = Mat("Diet Can", null, new Color(0.82f, 0.82f, 0.86f), Vector2.one, null, 0.7f, metallic: 0.7f);
            var canBand = Mat("Diet Can Band", null, new Color(0.95f, 0.5f, 0.65f), Vector2.one, null, 0.6f, metallic: 0.5f);
            var straw = Mat("Straw", null, new Color(0.95f, 0.4f, 0.6f), Vector2.one);
            var sodas = Group("Diet Sodas", g, Vector3.zero);
            foreach (var p in new[] { new Vector3(0.45f, 0, 0.05f), new Vector3(0.56f, 0, 0.12f), new Vector3(-1.15f, 0, 0.2f), new Vector3(0.9f, 0, 1.72f) })
            {
                var c = Group("Diet Soda", sodas, p);
                Prim(PrimitiveType.Cylinder, "Can", c, new Vector3(0, 0.06f, 0), new Vector3(0.066f, 0.06f, 0.066f), can);
                Prim(PrimitiveType.Cylinder, "Band", c, new Vector3(0, 0.07f, 0), new Vector3(0.067f, 0.02f, 0.067f), canBand);
                Prim(PrimitiveType.Cylinder, "Straw", c, new Vector3(0.01f, 0.16f, 0), new Vector3(0.008f, 0.06f, 0.008f), straw, new Vector3(0, 0, R(-12, 12)));
            }
            var shakeMat = Mat("Shake Bottle", null, new Color(0.97f, 0.96f, 0.95f), Vector2.one, null, 0.4f);
            var shakeLabel = Mat("Shake Label", null, new Color(0.9f, 0.62f, 0.72f), Vector2.one, null, 0.3f);
            foreach (var (p, tip) in new[] { (new Vector3(1.15f, 0, 0.95f), 0f), (new Vector3(1.22f, 0.04f, 1.08f), 90f) })
            {
                var b = Group("Meal Replacement Shake", sodas, p); b.localEulerAngles = new Vector3(tip, R(0, 360), 0);
                Prim(PrimitiveType.Cylinder, "Bottle", b, new Vector3(0, 0.09f, 0), new Vector3(0.07f, 0.09f, 0.07f), shakeMat);
                Prim(PrimitiveType.Cylinder, "Label", b, new Vector3(0, 0.08f, 0), new Vector3(0.072f, 0.04f, 0.072f), shakeLabel);
                Prim(PrimitiveType.Cylinder, "Cap", b, new Vector3(0, 0.19f, 0), new Vector3(0.05f, 0.012f, 0.05f), shakeLabel);
            }
            Interact(sodas, "look at the cans", true, 0.25f,
                "diet soda. a straw in every one.",
                "shakes instead of meals. sometimes.");

            // --- trash bag by the door, dirty dishes on the floor
            var bag = Group("Trash Bag", g, new Vector3(0.62f, 0, -2.02f));
            var plastic = Mat("Trash Bag", null, new Color(0.06f, 0.06f, 0.07f), Vector2.one, null, 0.75f);
            Prim(PrimitiveType.Sphere, "Bag", bag, new Vector3(0, 0.22f, 0), new Vector3(0.48f, 0.44f, 0.42f), plastic, new Vector3(0, 0, 5), collider: true);
            Prim(PrimitiveType.Sphere, "Bag Lump", bag, new Vector3(0.12f, 0.12f, 0.1f), new Vector3(0.3f, 0.24f, 0.3f), plastic);
            Prim(PrimitiveType.Capsule, "Knot", bag, new Vector3(-0.02f, 0.47f, 0), new Vector3(0.06f, 0.06f, 0.06f), plastic, new Vector3(0, 0, 30));
            Box("Spilled Box", bag, new Vector3(0.3f, 0.03f, 0.12f), new Vector3(0.15f, 0.06f, 0.12f), Mat("Takeout Box", "paint_white", new Color(0.96f, 0.94f, 0.92f), Vector2.one), new Vector3(0, 30, 10));
            Interact(bag, "look at the trash", false, 0.15f, "i only eat in my room. it piles up. sometimes i bag it.");

            var dishes = Group("Dirty Dishes", g, new Vector3(-0.25f, 0, -1.25f));
            var plate = Mat("Plate", null, new Color(0.96f, 0.94f, 0.94f), Vector2.one, null, 0.6f);
            var grime = Mat("Food Grime", null, new Color(0.55f, 0.35f, 0.2f), Vector2.one);
            for (int i = 0; i < 3; i++)
                Prim(PrimitiveType.Cylinder, "Plate", dishes, new Vector3(R(-0.02f, 0.02f), 0.008f + i * 0.014f, R(-0.02f, 0.02f)), new Vector3(0.24f, 0.006f, 0.24f), plate);
            Prim(PrimitiveType.Sphere, "Crust", dishes, new Vector3(0.04f, 0.045f, 0.02f), new Vector3(0.08f, 0.012f, 0.06f), grime);
            Prim(PrimitiveType.Cylinder, "Bowl", dishes, new Vector3(0.25f, 0.035f, 0.1f), new Vector3(0.15f, 0.035f, 0.15f), plate);
            Prim(PrimitiveType.Cylinder, "Bowl Grime", dishes, new Vector3(0.25f, 0.068f, 0.1f), new Vector3(0.12f, 0.002f, 0.12f), grime);
            Beam("Spoon", dishes, new Vector3(0.22f, 0.07f, 0.1f), new Vector3(0.38f, 0.0f, 0.2f), 0.015f, 0.004f, Mat("Weight Bar", null, new Color(0.6f, 0.6f, 0.62f), Vector2.one, null, 0.7f, metallic: 0.8f));
            Beam("Fork", dishes, new Vector3(-0.15f, 0.004f, 0.12f), new Vector3(-0.02f, 0.004f, 0.25f), 0.012f, 0.004f, Mat("Weight Bar", null, new Color(0.6f, 0.6f, 0.62f), Vector2.one, null, 0.7f, metallic: 0.8f));

            // --- more perfume on the vanity
            var vanity = new Vector3(-HX + 0.23f, 0.76f, -0.6f);
            foreach (var (o, col) in new[] { (new Vector3(-0.15f, 0, 0f), new Color(0.95f, 0.75f, 0.85f)), (new Vector3(-0.1f, 0, 0.07f), new Color(0.85f, 0.75f, 0.95f)) })
            {
                var pf = Group("Perfume", g, vanity + o);
                Box("Bottle", pf, new Vector3(0, 0.05f, 0), new Vector3(0.05f, 0.1f, 0.04f), Mat("Perfume " + ColorUtility.ToHtmlStringRGB(col), null, col, Vector2.one, new Color(0.15f, 0.05f, 0.1f), 0.9f));
                Prim(PrimitiveType.Sphere, "Cap", pf, new Vector3(0, 0.115f, 0), Vector3.one * 0.03f, Mat("Brass", null, new Color(0.55f, 0.45f, 0.3f), Vector2.one, null, 0.6f, metallic: 0.8f));
            }
        }

        /// A thin wisp of incense smoke. Uses URP's built-in particle material; skipped if it can't be found.
        static void Smoke(Transform parent, Vector3 localPos)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
            if (!mat) return;
            var go = new GameObject("Smoke");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localEulerAngles = new Vector3(-90, 0, 0);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 3.5f; main.startSpeed = 0.06f; main.startSize = 0.03f;
            main.startColor = new Color(0.85f, 0.8f, 0.85f, 0.18f); main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 10f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 4f; sh.radius = 0.002f;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.4f, 1, 2.5f));
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.03f; noise.frequency = 0.6f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                         new[] { new GradientAlphaKey(0f, 0), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1) });
            col.color = grad;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
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
            var buzz = Audio(g, "Light Buzz", new Vector3(0, H - 0.4f, 0), "light_buzz.wav", 0.1f, true, true, 5f);
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
