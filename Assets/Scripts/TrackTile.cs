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
      t.groundRoot = new GameObject("Ground").transform; t.groundRoot.SetParent(go.transform, false); t.groundWidth = w;
      t.RebuildGround();
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
    public void ClearObstacles(){
      for (int i = obstacleRoot.childCount - 1; i >= 0; i--) Destroy(obstacleRoot.GetChild(i).gameObject);
      if (gaps.Count > 0){ gaps.Clear(); RebuildGround(); }
    }

    // ---- M2: ground with real gaps for pits ----
    Transform groundRoot; float groundWidth;
    readonly List<(float z0, float z1, float x0, float x1)> gaps = new();
    /// <summary>Cuts a hole in this tile's ground (local z range, local x range). Restored when obstacles are cleared on recycle.</summary>
    public void CutGap(float zCenter, float len, float xMin, float xMax){
      gaps.Add((zCenter - len / 2, zCenter + len / 2, Mathf.Max(xMin, -groundWidth / 2), Mathf.Min(xMax, groundWidth / 2)));
      RebuildGround();
    }
    void RebuildGround(){
      for (int i = groundRoot.childCount - 1; i >= 0; i--){ var c = groundRoot.GetChild(i).gameObject; c.SetActive(false); Destroy(c); }
      float hw = groundWidth / 2; var mat = GameConfig.I.groundMat;
      void Slab(float z0, float z1, float x0, float x1){
        if (z1 - z0 < 0.001f || x1 - x0 < 0.001f) return;
        var slab = Prim(groundRoot, PrimitiveType.Cube, new Vector3((x0 + x1) / 2, -0.25f, (z0 + z1) / 2), new Vector3(x1 - x0, 0.5f, z1 - z0), mat);
        if (transform.Find("TileArt")) slab.GetComponent<Renderer>().enabled = false; // ArtM3 stone path is drawn instead
      }
      gaps.Sort((a, b) => a.z0 != b.z0 ? a.z0.CompareTo(b.z0) : a.x0.CompareTo(b.x0));
      float z = 0; int gi = 0;
      while (gi < gaps.Count){ // pits in one row share a z range; rows never overlap (planner keeps them >= 10 m apart)
        float z0 = Mathf.Max(z, gaps[gi].z0), z1 = gaps[gi].z1, rowZ = gaps[gi].z0, x = -hw;
        Slab(z, z0, -hw, hw);
        for (; gi < gaps.Count && gaps[gi].z0 == rowZ; gi++){ Slab(z0, z1, x, gaps[gi].x0); x = Mathf.Max(x, gaps[gi].x1); }
        Slab(z0, z1, x, hw);
        z = Mathf.Max(z, z1);
      }
      Slab(z, length, -hw, hw);
    }
    public static GameObject Prim(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material m){
      var g = GameObject.CreatePrimitive(type); g.transform.SetParent(parent, false);
      g.transform.localPosition = pos; g.transform.localScale = scale;
      if (m) g.GetComponent<Renderer>().sharedMaterial = m;
      return g;
    }
  }
}
