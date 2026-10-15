using UnityEngine;
namespace ExplorerDash {
  /// <summary>A low vine stretched across the whole path at shin height. Jump it; sliding or running into it = stumble.</summary>
  public class Vine : ObstacleBase {
    public static readonly Vector3 HitSize = new(GameConfig.LaneWidth * GameConfig.LaneCount + 1f, 0.35f, 0.3f);
    public const float HitCenterY = 0.45f; // spans 0.275..0.625 m; jump apex is ~1.45 m
    public override bool CanAvoidByJump => true;
    Transform rope; float t0;
    public static Vine Create(Transform parent, Vector3 localPos){
      var g = Root(parent, localPos, "Vine"); var v = g.AddComponent<Vine>();
      var cfg = GameConfig.I; var m = cfg.GetMat(ref cfg.vineMat, new Color(0.2f, 0.55f, 0.15f));
      v.rope = Box(g.transform, new Vector3(0, HitCenterY, 0), new Vector3(0.12f, HitSize.x / 2, 0.12f), m, PrimitiveType.Cylinder).transform;
      v.rope.localRotation = Quaternion.Euler(0, 0, 90);
      for (int s = -1; s <= 1; s += 2) Box(g.transform, new Vector3(s * HitSize.x / 2, 0.6f, 0), new Vector3(0.3f, 1.2f, 0.3f), m); // anchor stumps
      v.t0 = Random.value * 6f;
      var art = ArtM3Skin.Enabled ? ArtM3Skin.Load("Vine") : null;
      if (art){ // ArtM3 vine (own looping Sway animation); gray-box rope/stumps hidden, hit box unchanged
        foreach (var r in g.GetComponentsInChildren<Renderer>()) r.enabled = false;
        var a = Object.Instantiate(art, g.transform, false); a.name = "VineArt";
        foreach (var c in a.GetComponentsInChildren<Collider>()) Destroy(c);
        v.rope = null;
      }
      return v;
    }
    void Update(){
      if (rope) rope.localPosition = new Vector3(0, HitCenterY + Mathf.Sin((Time.time + t0) * 3f) * 0.03f, 0); // gentle sway, stays inside hit box
      CheckHit(transform.position + Vector3.up * HitCenterY, HitSize);
    }
  }
}
