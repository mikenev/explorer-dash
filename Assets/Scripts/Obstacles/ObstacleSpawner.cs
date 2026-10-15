using UnityEngine;
namespace ExplorerDash {
  /// <summary>M2 hook: called by TrackManager each time a tile is (re)placed ahead of the player.
  /// Spawn obstacles under tile.obstacleRoot at tile.spawnPoints. M1 only places occasional StumbleRoot placeholders
  /// (toggle with spawnPlaceholderRoots) so the boulder/stumble loop is testable.</summary>
  public class ObstacleSpawner : MonoBehaviour {
    public bool spawnPlaceholderRoots = true;
    [Range(0, 1)] public float rootChancePerTile = 0.6f;
    public virtual void Populate(TrackTile tile, int tileIndex){
      if (!spawnPlaceholderRoots || Random.value > rootChancePerTile) return;
      var p = tile.spawnPoints[Random.Range(0, tile.spawnPoints.Count)];
      StumbleRoot.Create(tile.obstacleRoot, p);
    }
  }
}
