using UnityEngine;
namespace ExplorerDash {
  /// <summary>Shared tuning values and gray-box materials (assigned by the editor build/setup script).</summary>
  public class GameConfig : MonoBehaviour {
    public static GameConfig I;
    public const float LaneWidth = 2.5f;
    public const int LaneCount = 3;
    public static float LaneX(int lane) => (lane - 1) * LaneWidth;
    public Material groundMat, edgeMat, playerMat, boulderMat, obstacleMat, idolMat;
    void Awake(){ I = this; }
  }
}
