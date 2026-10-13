using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AU.EditorTools
{
    /// <summary>
    /// One-click (or batchmode) generation of everything: textures, materials,
    /// mech prefab, the district scene with lighting and post-processing.
    ///   Unity -batchmode -projectPath . -executeMethod AU.EditorTools.SceneBuilder.BuildAll -quit
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/AU/Scenes/District.unity";
        const string Gen = "Assets/AU/Generated";

        [MenuItem("AU/Build Everything")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Gen);
            Directory.CreateDirectory("Assets/AU/Scenes");
            Directory.CreateDirectory("Assets/AU/Resources");
            Mats.Reset();
            EnsureLayers();
            EnsureSSAO();

            var assets = BuildGameAssets();
            MechImporter.Build(assets);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var world = new GameObject("World").transform;
            CityBuilder.Build(world);
            BuildLighting(world);
            BuildVolumes(world);

            var game = new GameObject("Game");
            game.AddComponent<Bootstrap>();
            game.AddComponent<Game>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[AU] BuildAll complete");
        }

        static void EnsureLayers()
        {
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            void Set(int i, string n) { var p = layers.GetArrayElementAtIndex(i); if (string.IsNullOrEmpty(p.stringValue)) p.stringValue = n; }
            Set(6, "Mech");
            Set(7, "Projectile");
            tm.ApplyModifiedProperties();
        }

        /// <summary>Screen-space ambient occlusion on the PC renderer: grounds the mechs and street props.</summary>
        static void EnsureSSAO()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            if (!data) return;
            var ssao = data.rendererFeatures.Find(f => f is ScreenSpaceAmbientOcclusion) as ScreenSpaceAmbientOcclusion;
            if (!ssao)
            {
                ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                ssao.name = "SSAO";
                AssetDatabase.AddObjectToAsset(ssao, data);
                data.rendererFeatures.Add(ssao);
                var so = new SerializedObject(data);
                var map = so.FindProperty("m_RendererFeatureMap");
                map.arraySize = data.rendererFeatures.Count;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out _, out long id);
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
                so.ApplyModifiedProperties();
            }
            var fs = new SerializedObject(ssao);
            void F(string n, float v) { var p = fs.FindProperty("m_Settings." + n); if (p != null) p.floatValue = v; }
            F("Intensity", 1.6f);
            F("Radius", 0.55f);
            F("DirectLightingStrength", 0.35f);
            F("Falloff", 60f);
            fs.ApplyModifiedProperties();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }

        static GameAssets BuildGameAssets()
        {
            const string path = "Assets/AU/Resources/GameAssets.asset";
            var a = AssetDatabase.LoadAssetAtPath<GameAssets>(path);
            if (!a) { a = ScriptableObject.CreateInstance<GameAssets>(); AssetDatabase.CreateAsset(a, path); }
            var dot = TextureGen.SoftDot();
            var streak = TextureGen.RainStreak();
            a.softDot = dot;
            a.rain = Mats.Particle("FX_Rain", streak, new Color(0.7f, 0.75f, 0.9f, 0.5f), false);
            a.splash = Mats.Particle("FX_Splash", dot, new Color(0.8f, 0.85f, 1f, 0.4f), false);
            a.steam = Mats.Particle("FX_Steam", dot, new Color(0.7f, 0.7f, 0.78f, 0.25f), false);
            a.spark = Mats.Particle("FX_Spark", dot, new Color(1f, 0.75f, 0.4f, 1f), true);
            a.smoke = Mats.Particle("FX_Smoke", dot, new Color(0.12f, 0.12f, 0.14f, 0.6f), false);
            a.fire = Mats.Particle("FX_Fire", dot, new Color(1f, 0.5f, 0.15f, 1f), true);
            a.explosion = Mats.Glow("FX_Explosion", new Color(1f, 0.55f, 0.2f) * 6f, 1.2f, 0f);
            a.tracer = Mats.Neon("FX_Tracer", new Color(1f, 0.75f, 0.35f) * 10f);
            a.tracerEnemy = Mats.Neon("FX_TracerEnemy", new Color(1f, 0.2f, 0.15f) * 10f);
            a.thruster = Mats.Glow("FX_Thruster", new Color(0.3f, 0.75f, 1f) * 2f, 0.8f, 0.85f);
            a.thrusterCore = Mats.Neon("FX_ThrusterCore", new Color(0.8f, 0.95f, 1f) * 5f);
            a.gate = Mats.Neon("FX_Gate", new Color(0f, 0.85f, 1f) * 6f);
            a.gateBeam = Mats.Glow("FX_GateBeam", new Color(0f, 0.7f, 1f) * 0.6f, 1.5f, 0f);
            a.markerGlow = Mats.Glow("FX_Marker", new Color(1f, 0.6f, 0.1f) * 0.5f, 1.5f, 0f);
            EditorUtility.SetDirty(a);
            return a;
        }

        static void BuildLighting(Transform world)
        {
            // one directional light: the sun by day, the moon by night (driven by DayNight)
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(world, false);
            sunGo.transform.rotation = Quaternion.Euler(38, -30, 0);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.84f);
            sun.intensity = 1.8f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.9f;

            var sky = Mats.Custom("Sky", Shader.Find("AU/Sky"));
            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.6f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.46f, 0.47f, 0.47f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.22f, 0.2f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.00032f;
            RenderSettings.fogColor = new Color(0.64f, 0.73f, 0.84f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1f;

            // long sunny views: push shadows out and use four cascades
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset rp)
            {
                rp.shadowDistance = 420f;
                rp.shadowCascadeCount = 4;
                rp.cascade4Split = new Vector3(0.04f, 0.13f, 0.36f);
                rp.mainLightShadowmapResolution = 4096;
                EditorUtility.SetDirty(rp);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (!a) continue;
                a.shadowDistance = Mathf.Max(a.shadowDistance, 300f);
                EditorUtility.SetDirty(a);
            }
        }

        static VolumeProfile NewProfile(string name)
        {
            string path = Gen + "/" + name + ".asset";
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var c = profile.Add<T>(false);
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, profile);
            return c;
        }

        static void BuildVolumes(Transform world)
        {
            // night: the rain-soaked neon grade
            var night = NewProfile("PostNight");
            var bloom = Add<Bloom>(night);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.85f);
            bloom.scatter.Override(0.72f);
            bloom.highQualityFiltering.Override(true);
            Add<Tonemapping>(night).mode.Override(TonemappingMode.ACES);
            var ca = Add<ColorAdjustments>(night);
            ca.postExposure.Override(0.35f);
            ca.contrast.Override(16f);
            ca.saturation.Override(12f);
            var wb = Add<WhiteBalance>(night);
            wb.temperature.Override(-10f);
            wb.tint.Override(6f);
            var st = Add<SplitToning>(night);
            st.shadows.Override(new Color(0.25f, 0.4f, 0.55f));
            st.highlights.Override(new Color(1f, 0.72f, 0.5f));
            st.balance.Override(15f);
            var vg = Add<Vignette>(night);
            vg.intensity.Override(0.3f);
            vg.smoothness.Override(0.45f);
            Add<ChromaticAberration>(night).intensity.Override(0.08f);
            var grain = Add<FilmGrain>(night);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.15f);
            var mb = Add<MotionBlur>(night);
            mb.mode.Override(MotionBlurMode.CameraOnly);
            mb.intensity.Override(0.12f);
            mb.quality.Override(MotionBlurQuality.Medium);
            Add<LensDistortion>(night).intensity.Override(0f);
            EditorUtility.SetDirty(night);

            // day: warm, clear, high dynamic range sunlight
            var day = NewProfile("PostDay");
            var db = Add<Bloom>(day);
            db.threshold.Override(1.1f);
            db.intensity.Override(0.35f);
            db.scatter.Override(0.65f);
            db.highQualityFiltering.Override(true);
            Add<Tonemapping>(day).mode.Override(TonemappingMode.ACES);
            var dca = Add<ColorAdjustments>(day);
            dca.postExposure.Override(0.2f);
            dca.contrast.Override(18f);
            dca.saturation.Override(16f);
            var dwb = Add<WhiteBalance>(day);
            dwb.temperature.Override(6f);
            var dst = Add<SplitToning>(day);
            dst.shadows.Override(new Color(0.35f, 0.45f, 0.6f));
            dst.highlights.Override(new Color(1f, 0.85f, 0.65f));
            dst.balance.Override(10f);
            var dvg = Add<Vignette>(day);
            dvg.intensity.Override(0.2f);
            dvg.smoothness.Override(0.5f);
            var dgr = Add<FilmGrain>(day);
            dgr.type.Override(FilmGrainLookup.Thin1);
            dgr.intensity.Override(0.06f);
            var dmb = Add<MotionBlur>(day);
            dmb.mode.Override(MotionBlurMode.CameraOnly);
            dmb.intensity.Override(0.12f);
            dmb.quality.Override(MotionBlurQuality.Medium);
            Add<LensDistortion>(day).intensity.Override(0f);
            Add<ChromaticAberration>(day).intensity.Override(0.04f);
            EditorUtility.SetDirty(day);

            foreach (var (name, profile, weight) in new[] { ("DayVolume", day, 1f), ("NightVolume", night, 0f) })
            {
                var vgo = new GameObject(name);
                vgo.transform.SetParent(world, false);
                var vol = vgo.AddComponent<Volume>();
                vol.isGlobal = true;
                vol.priority = 0;
                vol.weight = weight;
                vol.sharedProfile = profile;
            }
        }

        // ------------------------------------------------------------------ player builds
        public const string PlayerPath = "Build/ArmoredUnderground.exe";

        [MenuItem("AU/Build Windows Player")]
        public static void BuildPlayer()
        {
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = PlayerPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            PlayerSettings.productName = "Armored Underground";
            PlayerSettings.companyName = "Vyndir";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[AU] Player build: {report.summary.result} {report.summary.totalSize / (1024 * 1024)}MB errors={report.summary.totalErrors}");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(2);
        }

        public static void BuildAllAndPlayer()
        {
            BuildAll();
            BuildPlayer();
        }
    }
}
