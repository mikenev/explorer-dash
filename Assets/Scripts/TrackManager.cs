using System.Collections.Generic;
using UnityEngine;
namespace ExplorerDash {
  /// <summary>Pool of gray-box track tiles; tiles recycle from behind the player to the front. Calls ObstacleSpawner on each placed tile.</summary>
  public class TrackManager : MonoBehaviour {
    public Transform player;
    public ObstacleSpawner spawner;
    public float tileLength = 20f; public int tileCount = 8; public int safeTiles = 2;
    readonly List<TrackTile> tiles = new();
    float nextZ; int tileIndex;
    void Awake(){ for (int i = 0; i < tileCount; i++) tiles.Add(TrackTile.Create(transform, tileLength)); }
    public void ResetTrack(){
      nextZ = -tileLength * 2; tileIndex = 0;
      foreach (var t in tiles) Place(t);
    }
    void Place(TrackTile t){
      t.transform.position = new Vector3(0, 0, nextZ);
      t.ClearObstacles();
      if (spawner && tileIndex >= safeTiles + 2) spawner.Populate(t, tileIndex);
      nextZ += tileLength; tileIndex++;
    }
    void Update(){
      foreach (var t in tiles)
        if (t.transform.position.z + tileLength < player.position.z - 25f) Place(t);
    }
  }
}
