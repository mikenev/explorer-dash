using UnityEngine;
namespace ExplorerDash {
  /// <summary>M1 placeholder obstacle: a low root across one lane. Jump over it or you stumble. Replace/extend in M2.</summary>
  public class StumbleRoot : ObstacleBase {
    public override bool CanAvoidByJump => true;
    public static StumbleRoot Create(Transform parent, Vector3 localPos){
      var g = TrackTile.Prim(parent, PrimitiveType.Cube, localPos + new Vector3(0, 0.25f, 0), new Vector3(GameConfig.LaneWidth * 0.8f, 0.5f, 0.5f), GameConfig.I.obstacleMat);
      g.name = "StumbleRoot";
      g.GetComponent<Collider>().isTrigger = true;
      return g.AddComponent<StumbleRoot>();
    }
  }
}
