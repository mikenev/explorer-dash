using UnityEngine;
namespace ExplorerDash {
  /// <summary>M3 art pass. Runs after the Main scene loads and dresses the gray-box objects with the M3 prefabs from
  /// Resources/ArtM3 without editing the M1 scripts or scene: explorer (+ idol in hand, Animator driven by
  /// ExplorerAnimDriver), boulder, and every pooled TrackTile (stone path, dart-hole walls, foliage).
  /// Obstacles (pit, vine, alligator) are exposed as prefabs via <see cref="Load"/> for the obstacle code to spawn.
  /// Disable by setting <see cref="Enabled"/> = false before scene load.</summary>
  public static class ArtM3Skin {
    public static bool Enabled = true;
    public static GameObject Load(string name) => Resources.Load<GameObject>("ArtM3/M3_" + name);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Apply(){
      if (!Enabled || Load("Explorer") == null) return;
      var pc = Object.FindFirstObjectByType<PlayerController>();
      if (pc) SkinPlayer(pc);
      var boulder = Object.FindFirstObjectByType<Boulder>();
      if (boulder) SkinBoulder(boulder);
      foreach (var tile in Object.FindObjectsByType<TrackTile>(FindObjectsSortMode.None)) SkinTile(tile);
    }

    static void HideRenderers(Transform t, Transform keep = null){
      foreach (var r in t.GetComponentsInChildren<Renderer>(true))
        if (keep == null || !r.transform.IsChildOf(keep)) r.enabled = false;
    }

    static void SkinPlayer(PlayerController pc){
      HideRenderers(pc.transform.GetChild(0)); // gray-box body/hat/idol (still scaled by PlayerController.ApplyPose)
      var ex = Object.Instantiate(Load("Explorer"), pc.transform, false); ex.name = "ExplorerArt";
      ex.transform.localPosition = Vector3.zero; ex.transform.localRotation = Quaternion.identity;
      var hand = FindDeep(ex.transform, "LowerArm.L");
      if (hand){
        var idol = Object.Instantiate(Load("GoldenIdol"), hand, false); idol.name = "IdolInHand";
        idol.transform.localScale = Vector3.one * 0.55f;
        idol.transform.localPosition = new Vector3(0, 0.28f, 0); // bone length ~0.25 m: sits in the palm
      }
      ex.AddComponent<ExplorerAnimDriver>().player = pc;
    }

    static void SkinBoulder(Boulder b){
      var mr = b.GetComponent<Renderer>(); if (mr) mr.enabled = false;
      var art = Object.Instantiate(Load("Boulder"), b.transform, false); art.name = "BoulderArt";
      // Boulder root is a unit sphere scaled to 6 (radius 3); the art is authored at 6 m diameter in world units.
      var s = b.transform.lossyScale; art.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
    }

    static void SkinTile(TrackTile t){
      HideRenderers(t.transform, t.obstacleRoot);
      var art = new GameObject("TileArt").transform; art.SetParent(t.transform, false);
      Place("Track_StonePath", art, Vector3.zero, 0);
      float w = GameConfig.LaneWidth * GameConfig.LaneCount + 1f; // path width (8.5 m)
      float wallX = w / 2 + 0.4f;
      Place("Track_Wall", art, new Vector3(-wallX, 0, 0), 0);
      Place("Track_Wall", art, new Vector3(wallX, 0, 0), 0);
      var rng = new System.Random(t.GetInstanceID());
      for (int s = -1; s <= 1; s += 2){
        for (int i = 0; i < 2; i++) Place("Foliage_Palm", art, new Vector3(s * (w / 2 + 3f + i * 2.5f), 0, t.length * (0.25f + 0.5f * i)), rng.Next(360));
        for (int i = 0; i < 3; i++) Place("Foliage_Bush", art, new Vector3(s * (w / 2 + 1.6f + (float)rng.NextDouble() * 1.5f), 0, (float)rng.NextDouble() * t.length), rng.Next(360));
        for (int i = 0; i < 2; i++) Place("Foliage_Fern", art, new Vector3(s * (w / 2 + 1.2f + (float)rng.NextDouble() * 3f), 0, (float)rng.NextDouble() * t.length), rng.Next(360));
      }
    }
    static void Place(string n, Transform parent, Vector3 pos, float yaw){
      var g = Object.Instantiate(Load(n), parent, false); g.transform.localPosition = pos; g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
    }
    public static Transform FindDeep(Transform t, string name){
      if (t.name == name) return t;
      foreach (Transform c in t){ var r = FindDeep(c, name); if (r) return r; }
      return null;
    }
  }

  /// <summary>Maps PlayerController/GameManager state onto the Explorer Animator (Playing, Grounded, Sliding, Stumble, Caught).</summary>
  public class ExplorerAnimDriver : MonoBehaviour {
    public PlayerController player;
    Animator anim; GameState last;
    static readonly int Playing = Animator.StringToHash("Playing"), Grounded = Animator.StringToHash("Grounded"),
      Sliding = Animator.StringToHash("Sliding"), StumbleT = Animator.StringToHash("Stumble"), CaughtT = Animator.StringToHash("Caught");
    void Start(){ anim = GetComponent<Animator>(); player.OnStumble += () => anim.SetTrigger(StumbleT); }
    void LateUpdate(){
      var gm = GameManager.I; if (!gm || !anim) return;
      var st = gm.State;
      anim.SetBool(Playing, st == GameState.Playing);
      anim.SetBool(Grounded, player.IsGrounded);
      anim.SetBool(Sliding, player.IsSliding);
      if (st == GameState.GameOver && last != GameState.GameOver) anim.SetTrigger(CaughtT);
      if (st != GameState.GameOver) anim.ResetTrigger(CaughtT);
      last = st;
      // Slide/stumble poses come from the clips; cancel M1's gray-box body tilt on the art root.
      transform.localRotation = Quaternion.identity;
      if (st == GameState.Playing) anim.speed = Mathf.Lerp(0.9f, 1.5f, Mathf.InverseLerp(player.startSpeed, player.maxSpeed, player.Speed));
      else anim.speed = 1f;
    }
  }
}
