using UnityEngine;
using UnityEngine.InputSystem;
namespace ExplorerDash {
  public enum GameState { Ready, Intro, Playing, GameOver }
  /// <summary>Owns game state, distance score, local high score (PlayerPrefs), the intro beat and the IMGUI title/HUD/game-over UI.
  /// Flow: Ready (title) -> first input unlocks audio + title sting -> Intro (~2.2 s: Juniper lifts the idol, the temple rumbles,
  /// the boulder rolls in) -> Playing (run theme) -> GameOver (cause, distance, best, restart).</summary>
  public class GameManager : MonoBehaviour {
    public static GameManager I;
    const string HighKey = "ExplorerDash.HighScore";
    public const float IntroTime = 2.2f, RumbleAt = 0.9f, RestartDelay = 0.6f;
    public PlayerController player;
    public Boulder boulder;
    public TrackManager track;
    public GameState State { get; private set; } = GameState.Ready;
    public float Distance { get; private set; }
    public int HighScore { get; private set; }
    bool newHigh; string deathText = "CRUSHED BY THE BOULDER!";
    /// <summary>Frame the run started on; PlayerController ignores input on that frame so the start key doesn't jump.</summary>
    public int StartFrame { get; private set; } = -1;
    float startZ, stateTime; bool rumbled;
    Transform idol; Vector3 idolPos, idolScale;
    void Awake(){ I = this; HighScore = PlayerPrefs.GetInt(HighKey, 0); Application.targetFrameRate = 60; }
    void Start(){ ResetRun(); }
    public void ResetRun(){
      player.ResetPlayer(); boulder.ResetBoulder(); track.ResetTrack(); RestoreIdol();
      startZ = player.transform.position.z; Distance = 0; newHigh = false; SetState(GameState.Ready);
    }
    void SetState(GameState s){ State = s; stateTime = 0; }
    /// <summary>Title -> intro beat.</summary>
    public void BeginIntro(){
      if (State != GameState.Ready) return;
      SetState(GameState.Intro); rumbled = false; Sfx.TitleSting();
      idol = FindIdol();
      if (idol){ idolPos = idol.localPosition; idolScale = idol.localScale; }
    }
    public void StartRun(){
      if (State != GameState.Ready && State != GameState.Intro) return;
      RestoreIdol(); SetState(GameState.Playing); StartFrame = Time.frameCount; CameraFollow.Shake = 0;
      if (!rumbled) Sfx.StartRumble();
      Sfx.StartRunMusic();
    }
    public void GameOver(string reason = "CRUSHED BY THE BOULDER!"){
      if (State != GameState.Playing) return;
      deathText = reason; SetState(GameState.GameOver); CameraFollow.Shake = 0;
      Sfx.GameOver(player.IsFalling);
      int s = Mathf.FloorToInt(Distance);
      if (s > HighScore){ HighScore = s; newHigh = true; PlayerPrefs.SetInt(HighKey, s); PlayerPrefs.Save(); }
    }
    Transform FindIdol(){
      var t = ArtM3Skin.FindDeep(player.transform, "IdolInHand");
      return t ? t : ArtM3Skin.FindDeep(player.transform, "GoldenIdol");
    }
    void RestoreIdol(){ if (idol){ idol.localPosition = idolPos; idol.localScale = idolScale; idol = null; } }

    Rect MuteRect(){ float s = Screen.height / 720f; return new Rect(Screen.width - 220 * s, 15 * s, 200 * s, 44 * s); } // GUI coords (top-left origin)
    bool OverMute(Vector2 screenPos){ return MuteRect().Contains(new Vector2(screenPos.x, Screen.height - screenPos.y)); }

    void Update(){
      stateTime += Time.deltaTime;
      var k = Keyboard.current; var m = Mouse.current; var t = Touchscreen.current;
      // WebGL autoplay policy: audio only starts after the first user gesture.
      bool anyInput = (k != null && k.anyKey.wasPressedThisFrame) || (m != null && m.leftButton.wasPressedThisFrame) || (t != null && t.primaryTouch.press.wasPressedThisFrame);
      if (anyInput) Sfx.Unlock();
      if (k != null && k.mKey.wasPressedThisFrame) Sfx.ToggleMute();

      if (State == GameState.Playing){
        Distance = player.transform.position.z - startZ;
        CameraFollow.Shake = Mathf.Lerp(0.08f, 0f, Mathf.Clamp01(boulder.Gap / 6f)); // boulder close -> shake
      }
      if (State == GameState.Intro){
        float u = stateTime;
        if (idol){ // Juniper lifts the idol
          float lift = Mathf.SmoothStep(0, 1, Mathf.Clamp01(u / RumbleAt));
          idol.localPosition = idolPos + new Vector3(0, 0.35f * lift, 0); idol.localScale = idolScale * (1f + 0.5f * lift);
        }
        if (!rumbled && u >= RumbleAt){ rumbled = true; Sfx.StartRumble(); }
        CameraFollow.Shake = rumbled ? 0.12f : 0f;
        if (u >= IntroTime) StartRun();
      }

      bool press = (k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.rKey.wasPressedThisFrame))
        || (m != null && m.leftButton.wasPressedThisFrame && !OverMute(m.position.ReadValue()))
        || (t != null && t.primaryTouch.press.wasPressedThisFrame && !OverMute(t.primaryTouch.position.ReadValue()));
      if (State == GameState.GameOver && press && stateTime >= RestartDelay){ Sfx.UiClick(); ResetRun(); }
      else if (State == GameState.Ready && press){ Sfx.UiClick(); BeginIntro(); }
    }

    GUIStyle big, small, btn; Texture2D shade;
    void OnGUI(){
      float s = Screen.height / 720f;
      big ??= new GUIStyle(GUI.skin.label){ alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = true };
      small ??= new GUIStyle(GUI.skin.label){ fontStyle = FontStyle.Bold };
      btn ??= new GUIStyle(GUI.skin.button){ fontStyle = FontStyle.Bold };
      if (!shade){ shade = new Texture2D(1, 1); shade.SetPixel(0, 0, new Color(0, 0, 0, 0.45f)); shade.Apply(); }
      big.fontSize = Mathf.RoundToInt(48 * s); small.fontSize = Mathf.RoundToInt(26 * s); btn.fontSize = Mathf.RoundToInt(22 * s);
      var full = new Rect(0, 0, Screen.width, Screen.height);
      string Sz(int px, string txt) => $"<size={Mathf.RoundToInt(px * s)}>{txt}</size>";

      if (State == GameState.Ready){
        GUI.DrawTexture(full, shade);
        GUI.Label(full, Sz(84, "EXPLORER DASH") + "\n" + Sz(26, "Juniper has found the golden idol. The temple will not let it go quietly.")
          + "\n\n" + Sz(24, "A/D or Arrows or swipe: change lane    W/Up/Space: jump    S/Down: slide    M: mute")
          + "\n\n" + Sz(34, Mathf.Repeat(Time.unscaledTime, 1f) < 0.65f ? "Press Space / click / tap to start" : " ")
          + (HighScore > 0 ? "\n" + Sz(24, $"Best: {HighScore} m") : ""), big);
      } else if (State == GameState.Intro){
        string line = stateTime < RumbleAt ? "Juniper lifts the golden idol..." : stateTime < IntroTime - 0.5f ? "The temple rumbles!" : "RUN!";
        GUI.Label(new Rect(0, Screen.height * 0.15f, Screen.width, 120 * s), Sz(52, line), big);
      } else {
        // HUD
        GUI.Label(new Rect(20*s, 15*s, 600*s, 40*s), $"Distance: {Mathf.FloorToInt(Distance)} m", small);
        GUI.Label(new Rect(20*s, 50*s, 600*s, 40*s), $"Best: {Mathf.Max(HighScore, Mathf.FloorToInt(Distance))} m", small);
        float gap = Mathf.Max(0f, boulder.Gap);
        var old = small.normal.textColor; if (gap < 4f) small.normal.textColor = new Color(1f, 0.35f, 0.3f);
        GUI.Label(new Rect(20*s, 85*s, 600*s, 40*s), $"Boulder: {gap:0.0} m behind", small);
        small.normal.textColor = old;
        if (State == GameState.GameOver){
          GUI.DrawTexture(full, shade);
          GUI.Label(full, Sz(64, deathText) + "\n\n" + Sz(36, $"Distance: {Mathf.FloorToInt(Distance)} m" + (newHigh ? "   NEW BEST!" : ""))
            + "\n" + Sz(30, $"Best: {HighScore} m")
            + "\n\n" + Sz(30, stateTime >= RestartDelay ? "Press Space / R / tap to restart" : " "), big);
        }
      }
      // Mute button (always visible, drawn last so it's on top; M key toggles too)
      if (GUI.Button(MuteRect(), Sfx.Muted ? "Sound: OFF (M)" : "Sound: ON (M)", btn)){ Sfx.Unlock(); Sfx.ToggleMute(); Sfx.UiClick(); }
    }
  }
}
