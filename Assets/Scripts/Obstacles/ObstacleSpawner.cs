using System.Collections.Generic;
using UnityEngine;
namespace ExplorerDash {
  public enum ObstacleKind { Root, Pit, PitFull, Vine, Darts, Alligator }

  /// <summary>One planned obstacle row: a kind at a world z, occupying some lanes (full-width kinds occupy all).</summary>
  public struct PlannedObstacle {
    public ObstacleKind kind; public float z; public int lane; public bool fullWidth; public int safeLane;
  }

  /// <summary>M2 hook implementation: called by TrackManager each time a tile is (re)placed ahead of the player.
  /// Plans rows on tile.spawnPoints (rows every 5 m) with fairness rules, then instantiates them under tile.obstacleRoot.
  /// Fairness rules (checked by BuildWeb.ValidatePatterns across thousands of tiles):
  ///  - rows are at least MinRowGap apart; full-width rows (vine, darts, full pit) need MinFullGap clear before and after;
  ///  - a "safe lane" is carried row to row and moves at most one lane per row, and is never blocked by a lane obstacle;
  ///  - full-width rows always have a clean action (jump vine/pit, slide darts).
  /// Difficulty 0..1 ramps with distance: more rows per tile, two blocked lanes, more full-width and telegraphed hazards.</summary>
  public class ObstacleSpawner : MonoBehaviour {
    public bool spawnPlaceholderRoots = true;   // M1 flag kept: allows the low StumbleRoot in the lane mix
    [Range(0, 1)] public float rootChancePerTile = 0.6f; // M1 field kept for compatibility (unused by M2 planner)
    public float rampDistance = 1500f;          // metres until max difficulty
    public const float MinRowGap = 10f, MinFullGap = 20f;

    float lastRowZ = float.NegativeInfinity, lastFullZ = float.NegativeInfinity; int safeLane = 1;
    public System.Random rng = new();

    public void ResetPlan(){ lastRowZ = lastFullZ = float.NegativeInfinity; safeLane = 1; }

    public float Difficulty(float worldZ) => Mathf.Clamp01((worldZ - 40f) / rampDistance);

    /// <summary>Pure planning step (no GameObjects) so it can be validated headlessly.</summary>
    public List<PlannedObstacle> Plan(float tileZ, IReadOnlyList<Vector3> spawnPoints){
      var rows = new SortedSet<float>(); foreach (var p in spawnPoints) rows.Add(p.z);
      var outList = new List<PlannedObstacle>();
      foreach (var lz in rows){
        float z = tileZ + lz, d = Difficulty(z);
        if (z - lastRowZ < MinRowGap || z - lastFullZ < MinFullGap) continue;
        if (rng.NextDouble() > 0.3 + 0.45 * d) continue;           // density ramp: 30% -> 75% of eligible rows
        bool canFull = z - lastFullZ >= MinFullGap && z - lastRowZ >= MinFullGap;
        if (canFull && rng.NextDouble() < 0.25 + 0.2 * d){
          double r = rng.NextDouble();
          var k = r < 0.4 ? ObstacleKind.Vine : r < 0.75 || d < 0.15 ? ObstacleKind.Darts : ObstacleKind.PitFull;
          outList.Add(new PlannedObstacle{ kind = k, z = z, lane = -1, fullWidth = true, safeLane = safeLane });
          lastFullZ = lastRowZ = z; continue;
        }
        // lane row: drift the safe lane by at most 1, then block 1 (or 2 at higher difficulty) other lanes
        safeLane = Mathf.Clamp(safeLane + rng.Next(-1, 2), 0, GameConfig.LaneCount - 1);
        int blocks = rng.NextDouble() < 0.15 + 0.5 * d ? 2 : 1;
        var free = new List<int>(); for (int l = 0; l < GameConfig.LaneCount; l++) if (l != safeLane) free.Add(l);
        for (int b = 0; b < blocks && free.Count > 0; b++){
          int lane = free[rng.Next(free.Count)]; free.Remove(lane);
          outList.Add(new PlannedObstacle{ kind = PickLane(d), z = z, lane = lane, safeLane = safeLane });
        }
        lastRowZ = z;
      }
      return outList;
    }
    ObstacleKind PickLane(float d){
      double r = rng.NextDouble();
      if (r < 0.15 + 0.15 * d) return ObstacleKind.Alligator;
      if (r < 0.55) return ObstacleKind.Pit;
      return spawnPlaceholderRoots ? ObstacleKind.Root : ObstacleKind.Pit;
    }

    public virtual void Populate(TrackTile tile, int tileIndex){
      float tz = tile.transform.position.z;
      foreach (var o in Plan(tz, tile.spawnPoints)){
        var lp = new Vector3(o.lane >= 0 ? GameConfig.LaneX(o.lane) : 0, 0, o.z - tz);
        switch (o.kind){
          case ObstacleKind.Root: StumbleRoot.Create(tile.obstacleRoot, lp); break;
          case ObstacleKind.Pit: Pit.Create(tile.obstacleRoot, lp, 1); break;
          case ObstacleKind.PitFull: Pit.Create(tile.obstacleRoot, lp, GameConfig.LaneCount); break;
          case ObstacleKind.Vine: Vine.Create(tile.obstacleRoot, lp); break;
          case ObstacleKind.Darts: DartTrap.Create(tile.obstacleRoot, lp); break;
          case ObstacleKind.Alligator: Alligator.Create(tile.obstacleRoot, lp, o.lane); break;
        }
      }
    }
  }
}
