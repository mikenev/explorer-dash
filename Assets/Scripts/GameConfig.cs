using UnityEngine;
namespace ExplorerDash {
  /// <summary>Shared tuning values and gray-box materials (assigned by the editor build/setup script).</summary>
  [DefaultExecutionOrder(-100)]
  public class GameConfig : MonoBehaviour {
    public static GameConfig I;
    public const float LaneWidth = 2.5f;
    public const int LaneCount = 3;
    public static float LaneX(int lane) => (lane - 1) * LaneWidth;
    public Material groundMat, edgeMat, playerMat, boulderMat, obstacleMat, idolMat;
    /// <summary>M2 gray-box materials (created at runtime if not assigned).</summary>
    public Material pitMat, dartMat, warnMat, gatorMat, bushMat, vineMat;
    public Material GetMat(ref Material m, Color c){
      if (!m){ var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"); m = new Material(obstacleMat ? obstacleMat : new Material(sh)); m.SetColor("_BaseColor", c); m.color = c; }
      return m;
    }
    void Awake(){ I = this; }
  }
}
