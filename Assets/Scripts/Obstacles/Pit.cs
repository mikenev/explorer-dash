using UnityEngine;
namespace ExplorerDash {
  /// <summary>A gap in the ground across one lane or the whole path. Jump it. Being on the ground over it = instant death.</summary>
  public class Pit : ObstacleBase {
    public const float Length = 3.0f;   // along the run (matches the ArtM3 pit, 2.5 x 3 m); centre must be over the inner 2.6 m to fall
    public override bool CanAvoidByJump => true;
    float halfWidth;
    public static Pit Create(Transform parent, Vector3 localPos, int laneSpan){
      var g = Root(parent, localPos, "Pit");
      var pit = g.AddComponent<Pit>();
      var cfg = GameConfig.I;
      float w = laneSpan >= GameConfig.LaneCount ? GameConfig.LaneWidth * GameConfig.LaneCount + 1f : GameConfig.LaneWidth;
      pit.halfWidth = w / 2;
      // Cut a real gap in the tile's ground, with a dark pit floor/walls below it.
      var tile = parent.GetComponentInParent<TrackTile>();
      if (tile) tile.CutGap(localPos.z, Length, localPos.x - w / 2, localPos.x + w / 2);
      var art = ArtM3Skin.Enabled ? ArtM3Skin.Load("Pit") : null;
      if (art){ // one-lane prefab; tile it across the path for full-width pits
        int n = laneSpan >= GameConfig.LaneCount ? GameConfig.LaneCount : 1;
        for (int i = 0; i < n; i++){
          var a = Object.Instantiate(art, g.transform, false);
          a.transform.localPosition = new Vector3(n == 1 ? 0 : GameConfig.LaneX(i), 0, 0);
          foreach (var c in a.GetComponentsInChildren<Collider>()) Destroy(c);
        }
      } else { // gray-box fallback
        var dark = cfg.GetMat(ref cfg.pitMat, new Color(0.04f, 0.03f, 0.02f));
        Box(g.transform, new Vector3(0, -3f, 0), new Vector3(w, 0.1f, Length), dark);
        for (int s = -1; s <= 1; s += 2) Box(g.transform, new Vector3(0, -1.5f, s * Length / 2), new Vector3(w, 3f, 0.05f), dark);
        for (int s = -1; s <= 1; s += 2)
          Box(g.transform, new Vector3(0, 0.02f, s * (Length / 2 + 0.08f)), new Vector3(w, 0.04f, 0.16f), cfg.GetMat(ref cfg.warnMat, new Color(0.9f, 0.75f, 0.2f)));
      }
      return pit;
    }
    public override void OnPlayerHit(PlayerController player){ player.FallIntoPit(); }
    void Update(){
      if (hitDone || !Playing) return;
      var p = Player; var pp = p.transform.position; var c = transform.position;
      // Fall only when the explorer's centre is over the gap and he is on the ground (not mid-jump).
      if (p.FeetY <= 0.01f && Mathf.Abs(pp.z - c.z) < Length / 2 - 0.2f && Mathf.Abs(pp.x - c.x) < halfWidth - 0.15f){ hitDone = true; OnPlayerHit(p); }
    }
  }
}
