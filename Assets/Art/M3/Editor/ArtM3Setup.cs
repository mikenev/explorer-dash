using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Explorer Dash M3 art import: configures the procedural FBX models (generic rigs, clip names/loops, URP palette
/// materials), builds Animator controllers and writes prefabs to Assets/Art/M3/Resources/ArtM3 (loaded at runtime by ArtM3Skin).
/// Menu: Explorer Dash/Art M3/Rebuild Art Prefabs. Batch: -executeMethod ArtM3Setup.BatchRebuild</summary>
public static class ArtM3Setup {
  const string Root = "Assets/Art/M3", Models = Root + "/Models", Mats = Root + "/Materials", Out = Root + "/Resources/ArtM3", Ctl = Root + "/Animators";
  static readonly Dictionary<string, Color> Palette = new() {
      {"Skin", new Color(0.80f,0.56f,0.40f)},
      {"Hair", new Color(0.18f,0.10f,0.06f)},
      {"Helmet", new Color(0.86f,0.66f,0.30f)},
      {"HelmetBand", new Color(0.10f,0.42f,0.42f)},
      {"Teal", new Color(0.07f,0.48f,0.47f)},
      {"Shirt", new Color(0.93f,0.86f,0.68f)},
      {"Ochre", new Color(0.80f,0.52f,0.14f)},
      {"Shorts", new Color(0.45f,0.36f,0.22f)},
      {"Sock", new Color(0.93f,0.88f,0.75f)},
      {"Boot", new Color(0.28f,0.17f,0.10f)},
      {"Satchel", new Color(0.55f,0.30f,0.14f)},
      {"Brass", new Color(0.85f,0.70f,0.30f)},
      {"Stone", new Color(0.50f,0.48f,0.42f)},
      {"StoneDark", new Color(0.33f,0.32f,0.29f)},
      {"Moss", new Color(0.30f,0.45f,0.18f)},
      {"Gold", new Color(1.0f,0.76f,0.15f)},
      {"Gem", new Color(0.10f,0.75f,0.55f)},
      {"Leaf", new Color(0.16f,0.46f,0.18f)},
      {"LeafLight", new Color(0.32f,0.62f,0.22f)},
      {"Vine", new Color(0.20f,0.36f,0.12f)},
      {"Gator", new Color(0.24f,0.36f,0.18f)},
      {"GatorBelly", new Color(0.70f,0.68f,0.45f)},
      {"Mouth", new Color(0.70f,0.25f,0.25f)},
      {"Tooth", new Color(0.95f,0.93f,0.85f)},
      {"Void", new Color(0.03f,0.03f,0.03f)},
      {"Eye", new Color(0.95f,0.85f,0.20f)},
      {"Dust", new Color(0.82f,0.74f,0.58f)},
      {"Fletch", new Color(0.75f,0.18f,0.12f)},
      {"Wood", new Color(0.62f,0.48f,0.30f)},
  };
  static readonly HashSet<string> Looping = new() { "Run", "Idle", "Sway", "Puff" };
  static readonly string[] Static = { "Boulder", "GoldenIdol", "Track_StonePath", "Track_Wall", "Foliage_Bush", "Foliage_Palm", "Foliage_Fern", "Pit", "Dart", "DartEmitter" };

  [MenuItem("Explorer Dash/Art M3/Rebuild Art Prefabs")]
  public static void Rebuild(){
    Directory.CreateDirectory(Mats); Directory.CreateDirectory(Out); Directory.CreateDirectory(Ctl);
    var mats = Palette.ToDictionary(kv => kv.Key, kv => Mat(kv.Key, kv.Value));
    foreach (var path in Directory.GetFiles(Models, "*.fbx")) Configure(path.Replace('\\', '/'), mats);
    AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
    foreach (var n in Static) Prefab(n, null);
    Prefab("Explorer", ExplorerController());
    Prefab("Alligator", SimpleController("Alligator", "Idle", ("Lunge", "Lunge")));
    Prefab("Vine", SimpleController("Vine", "Sway"));
    Prefab("DustPuff", SimpleController("DustPuff", "Puff"));
    AssetDatabase.SaveAssets();
    Debug.Log("ArtM3: rebuilt " + (Static.Length + 4) + " prefabs");
  }
  public static void BatchRebuild(){
    int code = 0;
    try { Rebuild(); Validate(); } catch (System.Exception e){ Debug.LogException(e); code = 1; }
    if (Application.isBatchMode) EditorApplication.Exit(code);
  }

  static Material Mat(string name, Color c){
    var p = $"{Mats}/M3_{name}.mat";
    var m = AssetDatabase.LoadAssetAtPath<Material>(p);
    if (!m){ m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, p); }
    m.SetColor("_BaseColor", c); // project uses Gamma colour space: palette values are used as-is
    bool metal = name == "Gold" || name == "Brass";
    m.SetFloat("_Metallic", metal ? 0.7f : 0f); m.SetFloat("_Smoothness", metal ? 0.6f : 0.1f);
    if (name == "Gem" || name == "Eye") { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 0.4f); }
    EditorUtility.SetDirty(m); return m;
  }

  static void Configure(string path, Dictionary<string, Material> mats){
    var imp = (ModelImporter)AssetImporter.GetAtPath(path);
    imp.globalScale = 1; imp.useFileScale = true; imp.importCameras = false; imp.importLights = false;
    imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
    imp.isReadable = false; imp.meshCompression = ModelImporterMeshCompression.Medium;
    imp.importNormals = ModelImporterNormals.Import; imp.importBlendShapes = false;
    string n = Path.GetFileNameWithoutExtension(path);
    bool animated = n == "Explorer" || n == "Alligator" || n == "Vine" || n == "DustPuff";
    imp.animationType = animated ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
    imp.importAnimation = animated;
    foreach (var kv in mats) imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
    imp.SaveAndReimport();
    if (animated){
      var clips = imp.defaultClipAnimations.Select(c => {
        var shortName = c.name.Contains("|") ? c.name.Substring(c.name.LastIndexOf('|') + 1) : c.name;
        c.name = shortName; c.loopTime = Looping.Contains(shortName); c.lockRootHeightY = false; return c; }).ToArray();
      imp.clipAnimations = clips; imp.SaveAndReimport();
    }
  }

  static AnimationClip Clip(string model, string name) =>
    AssetDatabase.LoadAllAssetsAtPath($"{Models}/{model}.fbx").OfType<AnimationClip>().FirstOrDefault(c => c.name == name && !c.name.StartsWith("__preview"))
    ?? throw new System.Exception($"ArtM3: clip {model}/{name} not found");

  /// <summary>Explorer states: Idle, Run (default while playing), Jump, Slide, Stumble, Caught.
  /// Parameters (driven by ExplorerAnimDriver): Playing, Grounded, Sliding (bool), Stumble, Caught (trigger).</summary>
  static AnimatorController ExplorerController(){
    var path = Ctl + "/Explorer.controller"; AssetDatabase.DeleteAsset(path);
    var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
    foreach (var p in new[]{ "Playing", "Grounded", "Sliding" }) ac.AddParameter(p, AnimatorControllerParameterType.Bool);
    ac.AddParameter("Stumble", AnimatorControllerParameterType.Trigger); ac.AddParameter("Caught", AnimatorControllerParameterType.Trigger);
    var sm = ac.layers[0].stateMachine;
    AnimatorState S(string n, Vector3 pos){ var s = sm.AddState(n, pos); s.motion = Clip("Explorer", n); return s; }
    var idle = S("Idle", new Vector3(0, 0)); var run = S("Run", new Vector3(250, 0)); var jump = S("Jump", new Vector3(250, 120));
    var slide = S("Slide", new Vector3(500, 0)); var stumble = S("Stumble", new Vector3(500, 120)); var caught = S("Caught", new Vector3(250, -120));
    sm.defaultState = idle;
    AnimatorStateTransition T(AnimatorState a, AnimatorState b, float dur, params (AnimatorConditionMode m, float th, string p)[] cs){
      var t = a.AddTransition(b); t.hasExitTime = false; t.duration = dur;
      foreach (var c in cs) t.AddCondition(c.m, c.th, c.p); return t; }
    T(idle, run, 0.1f, (AnimatorConditionMode.If, 0, "Playing"));
    T(run, idle, 0.15f, (AnimatorConditionMode.IfNot, 0, "Playing"));
    T(run, jump, 0.05f, (AnimatorConditionMode.IfNot, 0, "Grounded"), (AnimatorConditionMode.IfNot, 0, "Sliding"));
    T(jump, run, 0.1f, (AnimatorConditionMode.If, 0, "Grounded"));
    T(run, slide, 0.05f, (AnimatorConditionMode.If, 0, "Sliding"));
    T(jump, slide, 0.05f, (AnimatorConditionMode.If, 0, "Sliding"));
    T(slide, run, 0.12f, (AnimatorConditionMode.IfNot, 0, "Sliding"));
    var st = T(stumble, run, 0.15f); st.hasExitTime = true; st.exitTime = 0.9f;
    var any = sm.AddAnyStateTransition(stumble); any.hasExitTime = false; any.duration = 0.05f; any.canTransitionToSelf = false;
    any.AddCondition(AnimatorConditionMode.If, 0, "Stumble");
    var anyC = sm.AddAnyStateTransition(caught); anyC.hasExitTime = false; anyC.duration = 0.05f; anyC.canTransitionToSelf = false;
    anyC.AddCondition(AnimatorConditionMode.If, 0, "Caught");
    T(caught, idle, 0.2f, (AnimatorConditionMode.If, 0, "Playing")).hasExitTime = false; // restart
    return ac;
  }

  static AnimatorController SimpleController(string model, string def, params (string state, string trigger)[] extra){
    var path = $"{Ctl}/{model}.controller"; AssetDatabase.DeleteAsset(path);
    var ac = AnimatorController.CreateAnimatorControllerAtPath(path); var sm = ac.layers[0].stateMachine;
    var d = sm.AddState(def); d.motion = Clip(model, def); sm.defaultState = d;
    foreach (var (state, trig) in extra){
      ac.AddParameter(trig, AnimatorControllerParameterType.Trigger);
      var s = sm.AddState(state); s.motion = Clip(model, state);
      var t = sm.AddAnyStateTransition(s); t.hasExitTime = false; t.duration = 0.05f; t.canTransitionToSelf = false; t.AddCondition(AnimatorConditionMode.If, 0, trig);
      var back = s.AddTransition(d); back.hasExitTime = true; back.exitTime = 1f; back.duration = 0.1f;
    }
    return ac;
  }

  static void Prefab(string n, RuntimeAnimatorController ctl){
    var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/{n}.fbx");
    var go = (GameObject)PrefabUtility.InstantiatePrefab(model); go.name = "M3_" + n;
    foreach (var r in go.GetComponentsInChildren<Renderer>()){
      r.shadowCastingMode = n.StartsWith("Track_") || n == "DustPuff" || n == "Dart" ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
      if (r is SkinnedMeshRenderer smr){ smr.updateWhenOffscreen = false; smr.quality = SkinQuality.Bone1; }
    }
    if (ctl){ if (!go.TryGetComponent<Animator>(out var a)) a = go.AddComponent<Animator>(); a.runtimeAnimatorController = ctl; a.applyRootMotion = false; a.cullingMode = AnimatorCullingMode.CullUpdateTransforms; }
    PrefabUtility.SaveAsPrefabAsset(go, $"{Out}/M3_{n}.prefab"); Object.DestroyImmediate(go);
  }

  /// <summary>Sanity checks for headless runs: prefabs load, renderers use URP materials, clips/states exist, tri counts.</summary>
  public static void Validate(){
    int tris = 0;
    foreach (var n in Static.Concat(new[]{ "Explorer", "Alligator", "Vine", "DustPuff" })){
      var p = Resources.Load<GameObject>("ArtM3/M3_" + n) ?? throw new System.Exception("missing prefab " + n);
      int t = 0; var b = new Bounds(); bool first = true;
      foreach (var r in p.GetComponentsInChildren<Renderer>(true)){
        var mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>().sharedMesh;
        t += mesh.triangles.Length / 3;
        foreach (var m in r.sharedMaterials) if (!m || m.shader.name != "Universal Render Pipeline/Lit") throw new System.Exception($"{n}: non-URP material {(m ? m.name + "/" + m.shader.name : "null")}");
        if (first){ b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
      }
      tris += t;
      var an = p.GetComponent<Animator>();
      string anim = an ? string.Join(",", ((AnimatorController)an.runtimeAnimatorController).layers[0].stateMachine.states.Select(s => s.state.name + (s.state.motion ? "" : "(NO CLIP)"))) : "-";
      Debug.Log($"ArtM3 VALID {n}: tris={t} bounds center={b.center} size={b.size} states={anim}");
      if (anim.Contains("NO CLIP")) throw new System.Exception(n + " has a state without a clip");
    }
    Debug.Log("ArtM3 VALIDATION OK total tris=" + tris);
  }

  /// <summary>Local verification only: WebGL build of the existing Main scene to an out-of-repo folder (never docs/).
  /// -executeMethod ArtM3Setup.BatchPreviewBuild -previewOut /tmp/ed-m3-web</summary>
  public static void BatchPreviewBuild(){
    int code = 1;
    try {
      var args = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(args, "-previewOut");
      var outDir = i >= 0 ? args[i + 1] : "/tmp/ed-m3-web";
      PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled; PlayerSettings.WebGL.threadsSupport = false;
      var r = BuildPipeline.BuildPlayer(new[]{ "Assets/Scenes/Main.unity" }, outDir, BuildTarget.WebGL, BuildOptions.None);
      Debug.Log("ArtM3 PREVIEW BUILD: " + r.summary.result + " size=" + r.summary.totalSize);
      if (r.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded) code = 0;
    } catch (System.Exception e){ Debug.LogException(e); }
    if (Application.isBatchMode) EditorApplication.Exit(code);
  }
}
