using UnityEngine;
namespace ExplorerDash {
  /// <summary>Darts fired from holes in the side walls at head height, across the whole path. Slide under them.
  /// Telegraph: ~1.5 s before the player arrives the wall holes puff dust and click; ~0.9 s before, darts start streaking across
  /// and keep firing until the player has passed. Jumping does not clear them (band 1.0..1.6 m, jump apex 1.45 + body).</summary>
  public class DartTrap : ObstacleBase {
    public const float WarnTime = 1.5f, FireTime = 0.9f;
    public static readonly Vector3 HitSize = new(GameConfig.LaneWidth * GameConfig.LaneCount + 1f, 0.6f, 0.6f);
    public const float HitCenterY = 1.3f; // 1.0..1.6 m: sliding height is 0.8 m
    public override bool CanAvoidBySlide => true;
    enum S { Idle, Warn, Fire, Done } S state;
    Transform[] puffs = new Transform[2]; Transform[] darts = new Transform[3]; float timer;
    public static DartTrap Create(Transform parent, Vector3 localPos){
      var g = Root(parent, localPos, "DartTrap"); var d = g.AddComponent<DartTrap>();
      var cfg = GameConfig.I; float hw = HitSize.x / 2;
      var warn = cfg.GetMat(ref cfg.warnMat, new Color(0.9f, 0.75f, 0.2f));
      for (int s = 0; s < 2; s++){
        float x = (s == 0 ? -1 : 1) * (hw + 0.05f);
        Box(g.transform, new Vector3(x, HitCenterY, 0), new Vector3(0.1f, 0.35f, 0.35f), warn); // carved hole plate
        var p = Sphere(g.transform, new Vector3(x, HitCenterY, 0), Vector3.zero, cfg.GetMat(ref cfg.dartMat, new Color(0.8f, 0.78f, 0.7f)));
        d.puffs[s] = p.transform;
      }
      for (int i = 0; i < d.darts.Length; i++){
        var dt = Box(g.transform, new Vector3(0, HitCenterY, 0), new Vector3(0.06f, 0.35f, 0.06f), cfg.GetMat(ref cfg.dartMat, new Color(0.8f, 0.78f, 0.7f)), PrimitiveType.Cylinder);
        dt.transform.localRotation = Quaternion.Euler(0, 0, 90); dt.SetActive(false); d.darts[i] = dt.transform;
      }
      if (ArtM3Skin.Enabled && ArtM3Skin.Load("Dart")) d.SkinArt(g, hw);
      return d;
    }
    /// <summary>ArtM3 visuals: carved emitter masks on both wall faces, dust-puff meshes in place of the gray-box puff spheres
    /// (still pulsed via localScale by Update) and dart meshes in place of the cylinders (still positioned by Update).
    /// Visual only: timing, hit box and CheckHit are unchanged.</summary>
    void SkinArt(GameObject g, float hw){
      foreach (var r in g.GetComponentsInChildren<Renderer>()) r.enabled = false;
      for (int s = 0; s < 2; s++){
        float side = s == 0 ? -1 : 1; var yaw = Quaternion.Euler(0, -side * 90f, 0); // art faces +Z -> turn to face the path
        Art("DartEmitter", g.transform, new Vector3(side * hw, HitCenterY, 0), yaw);
        var puff = Art("DustPuff", g.transform, new Vector3(side * (hw - 0.1f), HitCenterY, 0), yaw);
        puff.localScale = Vector3.zero; Destroy(puffs[s].gameObject); puffs[s] = puff;
      }
      for (int i = 0; i < darts.Length; i++){
        float dir = i % 2 == 0 ? 1 : -1; // matches the travel direction set in Update
        var a = Art("Dart", g.transform, darts[i].localPosition, Quaternion.Euler(0, dir * 90f, 0));
        a.gameObject.SetActive(false); Destroy(darts[i].gameObject); darts[i] = a;
      }
    }
    static Transform Art(string n, Transform parent, Vector3 pos, Quaternion rot){
      var a = Object.Instantiate(ArtM3Skin.Load(n), parent, false); a.name = n + "Art";
      foreach (var c in a.GetComponentsInChildren<Collider>()) Destroy(c);
      a.transform.localPosition = pos; a.transform.localRotation = rot; return a.transform;
    }
    void Update(){
      if (!Playing) return;
      float ttr = TimeToReach(); float hw = HitSize.x / 2;
      if (state == S.Idle && ttr < WarnTime){ state = S.Warn; timer = 0; Sfx.Click(); }
      if (state == S.Warn && ttr < FireTime){ state = S.Fire; foreach (var d in darts) d.gameObject.SetActive(true); Sfx.Click(); }
      timer += Time.deltaTime;
      if (state == S.Warn || state == S.Fire){
        float puff = state == S.Warn ? 0.25f + 0.35f * Mathf.PingPong(timer * 4f, 1f) : 0.3f;
        foreach (var p in puffs) p.localScale = Vector3.one * puff;
      }
      if (state == S.Fire){
        for (int i = 0; i < darts.Length; i++){ // streaks alternating from both walls
          float u = Mathf.Repeat(timer * 3f + i / (float)darts.Length, 1f); float dir = i % 2 == 0 ? 1 : -1;
          darts[i].localPosition = new Vector3(dir * Mathf.Lerp(-hw, hw, u), HitCenterY + (i - 1) * 0.12f, (i - 1) * 0.15f);
        }
        CheckHit(transform.position + Vector3.up * HitCenterY, HitSize);
        if (ttr < -0.4f){ state = S.Done; foreach (var d in darts) d.gameObject.SetActive(false); foreach (var p in puffs) p.localScale = Vector3.zero; }
      }
    }
  }
}
