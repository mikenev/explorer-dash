using UnityEngine;
namespace ExplorerDash {
  /// <summary>An alligator hidden in a bush at the edge of the path. Telegraph: ~1.6 s before the player arrives the bush shakes
  /// and hisses; ~0.85 s before, the gator lunges (0.25 s) into its target lane and snaps there until the player has passed.
  /// Dodge by being in another lane or jumping over it (0.6 m tall). Hit = stumble.</summary>
  public class Alligator : ObstacleBase {
    public const float WarnTime = 1.6f, LungeTime = 0.85f, LungeDuration = 0.25f, Height = 0.6f;
    public static readonly Vector3 HitSize = new(GameConfig.LaneWidth * 0.8f, Height, 2.2f);
    public override bool CanAvoidByJump => true;
    public int targetLane;
    enum S { Hidden, Shake, Lunge, Out, Retreat } S state;
    Transform bush, gator, jaw; Animator anim; float timer, bushX, outX, side, bushY = 0.8f;
    public static Alligator Create(Transform parent, Vector3 localPos, int lane){
      // localPos is the lane spawn point; the bush sits on the nearer wall (centre lane: random side).
      var g = Root(parent, new Vector3(0, 0, localPos.z), "Alligator"); var a = g.AddComponent<Alligator>();
      a.targetLane = lane;
      a.side = lane == 0 ? -1 : lane == GameConfig.LaneCount - 1 ? 1 : (Random.value < 0.5f ? -1 : 1);
      a.bushX = a.side * (GameConfig.LaneWidth * GameConfig.LaneCount / 2 + 1.3f); a.outX = GameConfig.LaneX(lane);
      var cfg = GameConfig.I;
      var bushArt = ArtM3Skin.Enabled ? ArtM3Skin.Load("Foliage_Bush") : null;
      if (bushArt){
        a.bush = Root(g.transform, new Vector3(a.bushX, 0, 0), "Bush").transform; a.bushY = 0;
        var b = Object.Instantiate(bushArt, a.bush, false); b.transform.localScale = Vector3.one * 1.6f;
      } else a.bush = Box(g.transform, new Vector3(a.bushX, 0.8f, 0), new Vector3(2f, 1.6f, 2.6f), cfg.GetMat(ref cfg.bushMat, new Color(0.12f, 0.5f, 0.12f)), PrimitiveType.Sphere).transform;
      var gm = cfg.GetMat(ref cfg.gatorMat, new Color(0.3f, 0.4f, 0.18f));
      a.gator = Root(g.transform, new Vector3(a.bushX, 0, 0), "Gator").transform;
      Box(a.gator, new Vector3(0, Height / 2, 0), new Vector3(HitSize.x * 0.9f, Height * 0.8f, 2f), gm);
      a.jaw = Box(a.gator, new Vector3(0, Height * 0.75f, -1.3f), new Vector3(0.7f, 0.18f, 0.9f), gm).transform; // snout faces the runner
      var gatorArt = ArtM3Skin.Enabled ? ArtM3Skin.Load("Alligator") : null;
      if (gatorArt){ // ArtM3 alligator faces +Z: turn 180 deg to face the runner; 'Lunge' trigger fired on lunge
        foreach (var r in a.gator.GetComponentsInChildren<Renderer>()) r.enabled = false;
        var ga = Object.Instantiate(gatorArt, a.gator, false); ga.name = "AlligatorArt";
        ga.transform.localRotation = Quaternion.Euler(0, 180f, 0);
        foreach (var c in ga.GetComponentsInChildren<Collider>()) Destroy(c);
        a.anim = ga.GetComponentInChildren<Animator>(); a.jaw = null;
      }
      a.gator.gameObject.SetActive(false);
      return a;
    }
    void Update(){
      if (!Playing) return;
      float ttr = TimeToReach(); timer += Time.deltaTime;
      if (state == S.Hidden && ttr < WarnTime){ state = S.Shake; timer = 0; Sfx.Hiss(); }
      if (state == S.Shake){
        bush.localPosition = new Vector3(bushX + Mathf.Sin(timer * 45f) * 0.15f, bushY, 0);
        bush.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(timer * 38f) * 8f);
        if (ttr < LungeTime){ state = S.Lunge; timer = 0; gator.gameObject.SetActive(true); bush.localRotation = Quaternion.identity; bush.localPosition = new Vector3(bushX, bushY, 0); if (anim) anim.SetTrigger("Lunge"); }
      }
      if (state == S.Lunge){
        float u = Mathf.Clamp01(timer / LungeDuration);
        gator.localPosition = new Vector3(Mathf.Lerp(bushX, outX, 1 - (1 - u) * (1 - u)), 0, 0);
        if (u >= 1){ state = S.Out; timer = 0; }
      }
      if (state == S.Out){
        if (jaw) jaw.localRotation = Quaternion.Euler(-Mathf.Abs(Mathf.Sin(timer * 10f)) * 25f, 0, 0); // snapping
        if (ttr < -0.5f){ state = S.Retreat; timer = 0; }
      }
      if (state == S.Retreat) gator.localPosition = new Vector3(Mathf.MoveTowards(gator.localPosition.x, bushX, 8f * Time.deltaTime), 0, 0);
      if (state == S.Lunge || state == S.Out) CheckHit(gator.position + Vector3.up * Height / 2, HitSize);
    }
  }
}
