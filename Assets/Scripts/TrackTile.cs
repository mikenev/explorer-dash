using System.Collections.Generic;
using UnityEngine;
namespace ExplorerDash {
  /// <summary>One gray-box track segment. Exposes obstacle spawn points (lane x row) for M2.</summary>
  public class TrackTile : MonoBehaviour {
    public float length;
    public Transform obstacleRoot;
    /// <summary>Local spawn points: rows along the tile, one per lane.</summary>
    public readonly List<Vector3> spawnPoints = new();
    public static TrackTile Create(Transform parent, float length){
      var go = new GameObject("TrackTile"); go.transform.SetParent(parent, false);
      var t = go.AddComponent<TrackTile>(); t.length = length;
      var cfg = GameConfig.I;
      float w = GameConfig.LaneWidth * GameConfig.LaneCount + 1f;
      Prim(go.transform, PrimitiveType.Cube, new Vector3(0, -0.25f, length / 2), new Vector3(w, 0.5f, length), cfg.groundMat);
      for (int s = -1; s <= 1; s += 2){
        Prim(go.transform, PrimitiveType.Cube, new Vector3(s * (w / 2 + 0.4f), 0.5f, length / 2), new Vector3(0.8f, 1.5f, length), cfg.edgeMat);
        // jungle "trees" as gray-box pillars
        for (int i = 0; i < 2; i++)
          Prim(go.transform, PrimitiveType.Cylinder, new Vector3(s * (w / 2 + 3f + i * 2.5f), 3f, length * (0.25f + 0.5f * i)), new Vector3(1f, 3f + i, 1f), cfg.edgeMat);
      }
      for (int l = 1; l < GameConfig.LaneCount; l++)
        Prim(go.transform, PrimitiveType.Cube, new Vector3(GameConfig.LaneX(l) - GameConfig.LaneWidth / 2, 0.01f, length / 2), new Vector3(0.05f, 0.02f, length), cfg.edgeMat);
      t.obstacleRoot = new GameObject("Obstacles").transform; t.obstacleRoot.SetParent(go.transform, false);
      for (float z = 5; z < length; z += 5)
        for (int l = 0; l < GameConfig.LaneCount; l++) t.spawnPoints.Add(new Vector3(GameConfig.LaneX(l), 0, z));
      return t;
    }
    public void ClearObstacles(){ for (int i = obstacleRoot.childCount - 1; i >= 0; i--) Destroy(obstacleRoot.GetChild(i).gameObject); }
    public static GameObject Prim(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material m){
      var g = GameObject.CreatePrimitive(type); g.transform.SetParent(parent, false);
      g.transform.localPosition = pos; g.transform.localScale = scale;
      if (m) g.GetComponent<Renderer>().sharedMaterial = m;
      return g;
    }
  }
}
