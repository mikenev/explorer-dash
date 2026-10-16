using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ExplorerDash;

/// <summary>Headless setup + WebGL build. Usage:
/// Unity -batchmode -nographics -projectPath . -buildTarget WebGL -executeMethod BuildWeb.Build -logFile build.log
/// (no -quit; this calls EditorApplication.Exit). Also available from the menu: Explorer Dash/...</summary>
public static class BuildWeb {
  const string ScenePath = "Assets/Scenes/Main.unity";
  const string Gen = "Assets/Generated";

  [MenuItem("Explorer Dash/Rebuild Main Scene")]
  public static void SetupScene(){
    Directory.CreateDirectory(Gen); Directory.CreateDirectory("Assets/Scenes");
    EnsureURP();
    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.45f, 0.5f, 0.42f);
    RenderSettings.fog = true; RenderSettings.fogColor = new Color(0.55f, 0.68f, 0.5f); RenderSettings.fogMode = FogMode.Linear;
    RenderSettings.fogStartDistance = 40; RenderSettings.fogEndDistance = 110;

    var cfgGo = new GameObject("GameConfig"); var cfg = cfgGo.AddComponent<GameConfig>();
    cfg.groundMat = Mat("Ground", new Color(0.55f, 0.45f, 0.32f));
    cfg.edgeMat = Mat("Jungle", new Color(0.18f, 0.42f, 0.2f));
    cfg.playerMat = Mat("Explorer", new Color(0.85f, 0.7f, 0.45f));
    cfg.boulderMat = Mat("Boulder", new Color(0.45f, 0.43f, 0.4f));
    cfg.obstacleMat = Mat("Root", new Color(0.35f, 0.2f, 0.1f));
    cfg.idolMat = Mat("Idol", new Color(1f, 0.8f, 0.1f));
    AudioClip Sfx(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/SFX/{n}.ogg");
    cfg.hissClip = Sfx("gator_hiss_snap"); cfg.dartClip = Sfx("dart_whoosh_click"); cfg.pitFallClip = Sfx("pit_fall"); cfg.stumbleClip = Sfx("stumble");
    AudioClip Mus(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/Music/{n}.ogg");
    cfg.jumpClip = Sfx("jump"); cfg.slideClip = Sfx("slide"); cfg.vineClip = Sfx("vine_swish"); cfg.crushedClip = Sfx("crushed");
    cfg.uiClickClip = Sfx("ui_click"); cfg.rumbleLoop = Sfx("boulder_rumble_loop"); // loops are set on the AudioSource (Sfx)
    cfg.runTheme = Mus("run_theme_loop"); cfg.titleSting = Mus("title_sting"); cfg.gameOverSting = Mus("gameover_sting");
    cfg.footsteps = new[]{ Sfx("footstep_01"), Sfx("footstep_02"), Sfx("footstep_03"), Sfx("footstep_04") };
    cfg.pitMat = Mat("Pit", new Color(0.04f, 0.03f, 0.02f));
    cfg.dartMat = Mat("Dart", new Color(0.8f, 0.78f, 0.7f));
    cfg.warnMat = Mat("Warn", new Color(0.9f, 0.75f, 0.2f));
    cfg.gatorMat = Mat("Gator", new Color(0.3f, 0.4f, 0.18f));
    cfg.bushMat = Mat("Bush", new Color(0.12f, 0.5f, 0.12f));
    cfg.vineMat = Mat("Vine", new Color(0.2f, 0.55f, 0.15f));

    var light = new GameObject("Sun").AddComponent<Light>(); light.type = LightType.Directional;
    light.transform.rotation = Quaternion.Euler(50, -30, 0); light.intensity = 1.2f; light.shadows = LightShadows.Soft;

    // Player (gray-box explorer: body capsule + hat + idol in hand)
    var player = new GameObject("Player");
    var body = GameObject.CreatePrimitive(PrimitiveType.Capsule); body.name = "Body"; body.transform.SetParent(player.transform, false);
    Object.DestroyImmediate(body.GetComponent<Collider>()); body.GetComponent<Renderer>().sharedMaterial = cfg.playerMat;
    var hat = GameObject.CreatePrimitive(PrimitiveType.Cylinder); hat.name = "Hat"; hat.transform.SetParent(body.transform, false);
    hat.transform.localPosition = new Vector3(0, 1f, 0); hat.transform.localScale = new Vector3(1.4f, 0.06f, 1.4f);
    Object.DestroyImmediate(hat.GetComponent<Collider>()); hat.GetComponent<Renderer>().sharedMaterial = cfg.obstacleMat;
    var idol = GameObject.CreatePrimitive(PrimitiveType.Sphere); idol.name = "GoldenIdol"; idol.transform.SetParent(body.transform, false);
    idol.transform.localPosition = new Vector3(0.6f, 0.1f, 0.3f); idol.transform.localScale = Vector3.one * 0.35f;
    Object.DestroyImmediate(idol.GetComponent<Collider>()); idol.GetComponent<Renderer>().sharedMaterial = cfg.idolMat;
    var cap = player.AddComponent<CapsuleCollider>(); cap.isTrigger = true; cap.radius = 0.4f;
    var rb = player.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
    var pc = player.AddComponent<PlayerController>();

    var boulderGo = GameObject.CreatePrimitive(PrimitiveType.Sphere); boulderGo.name = "Boulder";
    Object.DestroyImmediate(boulderGo.GetComponent<Collider>());
    boulderGo.transform.localScale = Vector3.one * 6f; boulderGo.GetComponent<Renderer>().sharedMaterial = cfg.boulderMat;
    var boulder = boulderGo.AddComponent<Boulder>(); boulder.player = pc;

    var trackGo = new GameObject("Track"); var track = trackGo.AddComponent<TrackManager>(); track.player = player.transform;
    track.spawner = new GameObject("ObstacleSpawner").AddComponent<ObstacleSpawner>();

    var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
    var cam = camGo.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = RenderSettings.fogColor;
    cam.fieldOfView = 62; cam.farClipPlane = 150; camGo.AddComponent<UniversalAdditionalCameraData>(); camGo.AddComponent<AudioListener>();
    camGo.transform.position = new Vector3(0.9f, 3.2f, -6.5f);
    camGo.AddComponent<CameraFollow>().target = player.transform;

    var gm = new GameObject("GameManager").AddComponent<GameManager>();
    gm.player = pc; gm.boulder = boulder; gm.track = track;

    EditorSceneManager.SaveScene(scene, ScenePath);
    EditorBuildSettings.scenes = new[]{ new EditorBuildSettingsScene(ScenePath, true) };
    AssetDatabase.SaveAssets();
  }

  static void EnsureURP(){
    var path = Gen + "/URP_Asset.asset";
    var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
    if (!asset){
      var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
      AssetDatabase.CreateAsset(rd, Gen + "/URP_Renderer.asset");
      asset = UniversalRenderPipelineAsset.Create(rd);
      AssetDatabase.CreateAsset(asset, path);
      AssetDatabase.SaveAssets();
    }
    GraphicsSettings.defaultRenderPipeline = asset;
    for (int i = 0; i < QualitySettings.names.Length; i++){ QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = asset; }
  }

  static Material Mat(string name, Color c){
    var p = $"{Gen}/{name}.mat";
    var m = AssetDatabase.LoadAssetAtPath<Material>(p);
    if (!m){ m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, p); }
    m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0.15f); EditorUtility.SetDirty(m);
    return m;
  }

  /// <summary>Headless fairness check of the obstacle planner + physics margins. Usage:
  /// Unity -batchmode -nographics -projectPath . -executeMethod BuildWeb.ValidatePatterns -logFile validate.log</summary>
  [MenuItem("Explorer Dash/Validate Obstacle Patterns")]
  public static void ValidatePatterns(){
    int fails = 0; void Fail(string m){ fails++; if (fails < 30) Debug.LogError("VALIDATE FAIL: " + m); }
    var go = new GameObject("tmp"); var sp = go.AddComponent<ObstacleSpawner>();
    var pts = new System.Collections.Generic.List<Vector3>();
    for (float z = 5; z < 20; z += 5) for (int l = 0; l < GameConfig.LaneCount; l++) pts.Add(new Vector3(GameConfig.LaneX(l), 0, z));
    int total = 0; var counts = new System.Collections.Generic.Dictionary<ObstacleKind, int>();
    int earlyRows = 0, lateRows = 0;
    for (int seed = 0; seed < 300; seed++){
      sp.rng = new System.Random(seed); sp.ResetPlan();
      float lastRow = -999, lastFull = -999; int lastSafe = 1; float rowZ = -999; var blocked = new System.Collections.Generic.HashSet<int>();
      for (int t = 4; t < 200; t++){
        foreach (var o in sp.Plan(t * 20f - 40f, pts)){
          total++; counts[o.kind] = counts.TryGetValue(o.kind, out var c) ? c + 1 : 1;
          if (o.z < 300) earlyRows++; else if (o.z > 2000 && o.z < 2300) lateRows++;
          if (o.z != rowZ){
            if (o.z - lastRow < ObstacleSpawner.MinRowGap) Fail($"seed {seed}: rows {lastRow}->{o.z} too close");
            if (o.z - lastFull < ObstacleSpawner.MinFullGap) Fail($"seed {seed}: row {o.z} within {ObstacleSpawner.MinFullGap} m after full-width row {lastFull}");
            if (o.fullWidth && o.z - lastRow < ObstacleSpawner.MinFullGap) Fail($"seed {seed}: full-width {o.z} too close to row {lastRow}");
            if (!o.fullWidth && Mathf.Abs(o.safeLane - lastSafe) > 1) Fail($"seed {seed}: safe lane jumped {lastSafe}->{o.safeLane}");
            lastRow = rowZ = o.z; blocked.Clear(); if (!o.fullWidth) lastSafe = o.safeLane;
          }
          if (o.fullWidth) lastFull = o.z;
          else {
            if (o.lane == o.safeLane) Fail($"seed {seed}: safe lane {o.lane} blocked at {o.z}");
            blocked.Add(o.lane); if (blocked.Count >= GameConfig.LaneCount) Fail($"seed {seed}: all lanes blocked at {o.z}");
          }
        }
      }
    }
    Object.DestroyImmediate(go);
    // Physics margins (PlayerController defaults): worst case is a stumbled runner at start speed.
    float v = 9f, g = 28f, air = 2 * v / g, apex = v * v / (2 * g), slow = 10f * 0.45f;
    if (slow * air < Pit.Length - 0.4f + 0.2f) Fail($"pit not clearable while stumbled: {slow * air:0.00} m airborne");
    if (apex < Vine.HitCenterY + Vine.HitSize.y / 2 + 0.3f) Fail("vine too high to jump");
    if (0.8f >= DartTrap.HitCenterY - DartTrap.HitSize.y / 2) Fail("darts too low to slide under");
    if (apex + 1.8f <= DartTrap.HitCenterY + DartTrap.HitSize.y / 2 && apex >= DartTrap.HitCenterY - DartTrap.HitSize.y / 2) Debug.Log("darts: jump does not clear (slide required) - expected");
    if (apex < Alligator.Height + 0.3f) Fail("alligator too tall to jump");
    if (22f * 0.18f > ObstacleSpawner.MinRowGap - 4f) Fail("lane change too slow for row gap at max speed");
    var sb = new System.Text.StringBuilder(); foreach (var kv in counts) sb.Append($"{kv.Key}={kv.Value} ");
    Debug.Log($"VALIDATE RESULT: {(fails == 0 ? "PASS" : "FAIL")} fails={fails} obstacles={total} [{sb}] rowsObstacles z<300: {earlyRows}, z 2000-2300: {lateRows} (300 seeds)");
    if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 1);
  }

  [MenuItem("Explorer Dash/Build WebGL to docs")]
  public static void Build(){
    int code = 1;
    try {
      SetupScene();
      PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
      PlayerSettings.WebGL.decompressionFallback = true;
      PlayerSettings.WebGL.threadsSupport = false;
      PlayerSettings.productName = "Explorer Dash"; PlayerSettings.companyName = "mikenev";
      var outDir = Path.GetFullPath("docs");
      var r = BuildPipeline.BuildPlayer(new[]{ ScenePath }, outDir, BuildTarget.WebGL, BuildOptions.None);
      Debug.Log("BUILD RESULT: " + r.summary.result + " size=" + r.summary.totalSize);
      if (r.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded){ File.WriteAllText(Path.Combine(outDir, ".nojekyll"), ""); code = 0; }
    } catch (System.Exception e){ Debug.LogException(e); }
    if (Application.isBatchMode) EditorApplication.Exit(code);
  }
}
