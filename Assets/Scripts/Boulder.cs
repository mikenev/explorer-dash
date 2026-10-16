using UnityEngine;
namespace ExplorerDash {
  /// <summary>Giant boulder chasing the player. Each stumble closes the gap; the gap slowly recovers. Gap 0 = caught.</summary>
  public class Boulder : MonoBehaviour {
    public PlayerController player;
    public float startGap = 12f, minGap = 0f, recoverRate = 0.6f, maxGap = 12f, stumblePenalty = 5f, radius = 3f;
    public float Gap { get; private set; }
    void Start(){ player.OnStumble += () => Gap -= stumblePenalty; }
    public float introGap = 40f;
    /// <summary>Title: boulder waits out of sight (introGap); Intro: it rumbles into place at startGap.</summary>
    public void ResetBoulder(){ Gap = introGap; Place(); }
    void Update(){
      var gm = GameManager.I;
      if (gm.State == GameState.Playing){
        Gap = Mathf.Min(maxGap, Gap + recoverRate * Time.deltaTime);
        if (Gap <= minGap){ Gap = 0; gm.GameOver("CRUSHED BY THE BOULDER!"); }
        transform.Rotate(Vector3.right, player.Speed * Time.deltaTime / radius * Mathf.Rad2Deg, Space.World);
      } else if (gm.State == GameState.Intro){
        Gap = Mathf.MoveTowards(Gap, startGap, (introGap - startGap) / 1.2f * Time.deltaTime);
        transform.Rotate(Vector3.right, 12f * Time.deltaTime / radius * Mathf.Rad2Deg, Space.World);
      } else if (gm.State == GameState.GameOver){
        if (!player.IsFalling) Gap = Mathf.MoveTowards(Gap, -radius * 0.6f, 20f * Time.deltaTime); // roll over the explorer
      }
      Place();
    }
    void Place(){
      var p = player.transform.position;
      transform.position = new Vector3(0, radius, p.z - Gap - radius);
    }
  }
}
