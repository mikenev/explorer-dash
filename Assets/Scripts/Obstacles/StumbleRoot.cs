using UnityEngine;
namespace ExplorerDash {
  /// <summary>M1 placeholder obstacle: a low root across one lane. Jump over it or you stumble. Replace/extend in M2.</summary>
  public class StumbleRoot : ObstacleBase {
    public override bool CanAvoidByJump => true;
    public static StumbleRoot Create(Transform parent, Vector3 localPos){
      var g = TrackTile.Prim(parent, PrimitiveType.Cube, localPos + new Vector3(0, 0.25f, 0), new Vector3(GameConfig.LaneWidth * 0.8f, 0.5f, 0.5f), GameConfig.I.obstacleMat);
      g.name = "StumbleRoot";
      g.GetComponent<Collider>().isTrigger = true;
      var art = ArtM3Skin.Enabled ? ArtM3Skin.Load("StumbleRoot") : null;
      if (art){ // ArtM3 gnarled root: visual only, the box collider/trigger and its size are unchanged
        g.GetComponent<Renderer>().enabled = false;
        var a = Object.Instantiate(art, g.transform, false); a.name = "StumbleRootArt";
        foreach (var c in a.GetComponentsInChildren<Collider>()) Destroy(c);
        var sc = g.transform.localScale; // undo the box scale; art is authored in metres with its origin on the ground
        a.transform.localScale = new Vector3(1f / sc.x, 1f / sc.y, 1f / sc.z);
        a.transform.localPosition = new Vector3(0, -0.5f, 0); // box centre is 0.25 m up = 0.5 in box-local units
      }
      return g.AddComponent<StumbleRoot>();
    }
  }
}
