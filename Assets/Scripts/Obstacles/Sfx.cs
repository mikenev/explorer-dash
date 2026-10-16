using UnityEngine;
namespace ExplorerDash {
  /// <summary>Audio manager (static API, lazily created). Plays Audio Dev's clips wired on GameConfig:
  /// run_theme_loop + boulder_rumble_loop (looping), title/game-over stings, footsteps and gameplay SFX.
  /// Nothing plays until the first user input (WebGL autoplay policy): GameManager calls Unlock() on the first press.
  /// Mute: M key or the on-screen button (GameManager) -> ToggleMute(); persisted in PlayerPrefs.
  /// Dart click and gator hiss keep procedural fallbacks if their clips are missing.</summary>
  public class Sfx : MonoBehaviour {
    const string MuteKey = "ExplorerDash.Muted";
    static Sfx inst;
    AudioSource sfx, music, rumble; AudioClip click, hiss; float stepTimer; int stepIdx;
    public static bool Unlocked { get; private set; }
    public static bool Muted { get; private set; }
    static GameConfig C => GameConfig.I;
    static Sfx I {
      get {
        if (inst) return inst;
        if (!FindAnyObjectByType<AudioListener>() && Camera.main) Camera.main.gameObject.AddComponent<AudioListener>();
        inst = new GameObject("Sfx").AddComponent<Sfx>();
        inst.sfx = inst.Src(false); inst.music = inst.Src(true); inst.rumble = inst.Src(true);
        inst.click = Make("click", 0.06f, (t, r) => Mathf.Exp(-t * 90f) * (Mathf.Sin(t * 2 * Mathf.PI * 1800f) * 0.6f + r * 0.4f));
        inst.hiss = Make("hiss", 0.7f, (t, r) => r * 0.35f * Mathf.Min(1f, t * 20f) * Mathf.Clamp01((0.7f - t) * 4f));
        Muted = PlayerPrefs.GetInt(MuteKey, 0) == 1; AudioListener.volume = Muted ? 0 : 1;
        return inst;
      }
    }
    AudioSource Src(bool loop){ var s = gameObject.AddComponent<AudioSource>(); s.loop = loop; s.playOnAwake = false; s.spatialBlend = 0; return s; }
    static AudioClip Make(string name, float len, System.Func<float, float, float> f){
      const int rate = 22050; int n = Mathf.CeilToInt(len * rate); var d = new float[n]; var rng = new System.Random(name.GetHashCode());
      for (int i = 0; i < n; i++) d[i] = f(i / (float)rate, (float)(rng.NextDouble() * 2 - 1));
      var c = AudioClip.Create(name, n, 1, rate, false); c.SetData(d, 0); return c;
    }
    static void One(AudioClip c, float v){ if (c && Unlocked) I.sfx.PlayOneShot(c, v); }

    public static void Unlock(){ if (Unlocked) return; Unlocked = true; _ = I; }
    public static void ToggleMute(){ _ = I; Muted = !Muted; AudioListener.volume = Muted ? 0 : 1; PlayerPrefs.SetInt(MuteKey, Muted ? 1 : 0); PlayerPrefs.Save(); }

    // music / loops
    public static void TitleSting(){ if (!Unlocked) return; I.music.Stop(); I.rumble.Stop(); One(C ? C.titleSting : null, 0.9f); }
    public static void StartRumble(){ if (!Unlocked || !C || !C.rumbleLoop) return; I.rumble.clip = C.rumbleLoop; I.rumble.volume = 0.5f; I.rumble.Play(); }
    public static void StartRunMusic(){ if (!Unlocked || !C || !C.runTheme) return; I.music.clip = C.runTheme; I.music.volume = 0.6f; I.music.Play(); }
    public static void GameOver(bool pit){
      if (!Unlocked) return; I.music.Stop(); I.rumble.Stop();
      if (!pit) One(C ? C.crushedClip : null, 1f);
      if (C && C.gameOverSting) I.sfx.PlayOneShot(C.gameOverSting, 0.9f);
    }

    // gameplay SFX
    public static void Click(){ One(C && C.dartClip ? C.dartClip : I.click, 0.8f); }
    public static void Hiss(){ One(C && C.hissClip ? C.hissClip : I.hiss, 0.8f); }
    public static void PitFall(){ One(C ? C.pitFallClip : null, 0.9f); }
    public static void Stumble(){ One(C ? C.stumbleClip : null, 0.8f); }
    public static void Jump(){ One(C ? C.jumpClip : null, 0.7f); }
    public static void Slide(){ One(C ? C.slideClip : null, 0.7f); }
    public static void VineSwish(){ One(C ? C.vineClip : null, 0.7f); }
    public static void UiClick(){ One(C ? C.uiClickClip : null, 0.8f); }

    void Update(){
      var gm = GameManager.I; if (!gm || !Unlocked) return;
      if (gm.State == GameState.Playing){
        var p = gm.player;
        rumble.volume = Mathf.Lerp(0.9f, 0.3f, Mathf.Clamp01(gm.boulder.Gap / gm.boulder.maxGap)); // louder as the boulder closes in
        if (p.IsGrounded && !p.IsSliding && C && C.footsteps != null && C.footsteps.Length > 0){
          stepTimer -= Time.deltaTime;
          if (stepTimer <= 0){ stepTimer = Mathf.Clamp(3.2f / Mathf.Max(1f, p.Speed), 0.16f, 0.34f); sfx.PlayOneShot(C.footsteps[stepIdx++ % C.footsteps.Length], 0.45f); }
        } else stepTimer = 0.05f;
      }
    }
  }
}
