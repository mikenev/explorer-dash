using UnityEngine;
using UnityEngine.InputSystem;
namespace ExplorerDash {
  public enum GameState { Ready, Playing, GameOver }
  /// <summary>Owns game state, distance score, local high score (PlayerPrefs) and the IMGUI HUD.</summary>
  public class GameManager : MonoBehaviour {
    public static GameManager I;
    const string HighKey = "ExplorerDash.HighScore";
    public PlayerController player;
    public Boulder boulder;
    public TrackManager track;
    public GameState State { get; private set; } = GameState.Ready;
    public float Distance { get; private set; }
    public int HighScore { get; private set; }
    bool newHigh;
    float startZ;
    void Awake(){ I = this; HighScore = PlayerPrefs.GetInt(HighKey, 0); Application.targetFrameRate = 60; }
    void Start(){ ResetRun(); }
    public void ResetRun(){
      player.ResetPlayer(); boulder.ResetBoulder(); track.ResetTrack();
      startZ = player.transform.position.z; Distance = 0; newHigh = false; State = GameState.Ready;
    }
    public void StartRun(){ if (State == GameState.Ready) State = GameState.Playing; }
    public void GameOver(){
      if (State != GameState.Playing) return;
      State = GameState.GameOver;
      int s = Mathf.FloorToInt(Distance);
      if (s > HighScore){ HighScore = s; newHigh = true; PlayerPrefs.SetInt(HighKey, s); PlayerPrefs.Save(); }
    }
    void Update(){
      if (State == GameState.Playing) Distance = player.transform.position.z - startZ;
      bool any = (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.rKey.wasPressedThisFrame))
        || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        || (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);
      if (State == GameState.GameOver && any) ResetRun();
      else if (State == GameState.Ready && any) StartRun();
    }
    GUIStyle big, small;
    void OnGUI(){
      float s = Screen.height / 720f;
      big ??= new GUIStyle(GUI.skin.label){ alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
      small ??= new GUIStyle(GUI.skin.label){ fontStyle = FontStyle.Bold };
      big.fontSize = Mathf.RoundToInt(48 * s); small.fontSize = Mathf.RoundToInt(26 * s);
      GUI.Label(new Rect(20*s, 15*s, 600*s, 40*s), $"Distance: {Mathf.FloorToInt(Distance)} m", small);
      GUI.Label(new Rect(20*s, 50*s, 600*s, 40*s), $"Best: {HighScore} m", small);
      GUI.Label(new Rect(20*s, 85*s, 600*s, 40*s), $"Boulder: {boulder.Gap:0.0} m behind", small);
      var full = new Rect(0, 0, Screen.width, Screen.height);
      if (State == GameState.Ready)
        GUI.Label(full, "EXPLORER DASH\n<size=" + Mathf.RoundToInt(26*s) + ">You grabbed the golden idol... RUN!\nA/D or ←/→ or swipe: change lane   W/↑/Space: jump   S/↓: slide\nPress Space / click / tap to start</size>", RichBig());
      else if (State == GameState.GameOver)
        GUI.Label(full, "CRUSHED!\n<size=" + Mathf.RoundToInt(30*s) + ">" + Mathf.FloorToInt(Distance) + " m" + (newHigh ? "  NEW BEST!" : "") + "\nPress Space / R / tap to restart</size>", RichBig());
    }
    GUIStyle RichBig(){ big.richText = true; return big; }
  }
}
