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
    cam.fieldOfView = 62; cam.farClipPlane = 150; camGo.AddComponent<UniversalAdditionalCameraData>();
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
