using UnityEngine;
namespace ExplorerDash {
  /// <summary>Tiny procedural gray-box SFX (no audio assets): dart-trap click and alligator hiss. Art/Audio can replace the clips.</summary>
  public class Sfx : MonoBehaviour {
    static Sfx inst; AudioSource src; public AudioClip click, hiss;
    static Sfx I {
      get {
        if (inst) return inst;
        if (!FindAnyObjectByType<AudioListener>() && Camera.main) Camera.main.gameObject.AddComponent<AudioListener>();
        inst = new GameObject("Sfx").AddComponent<Sfx>();
        inst.src = inst.gameObject.AddComponent<AudioSource>(); inst.src.spatialBlend = 0; inst.src.playOnAwake = false;
        inst.click = Make("click", 0.06f, (t, r) => Mathf.Exp(-t * 90f) * (Mathf.Sin(t * 2 * Mathf.PI * 1800f) * 0.6f + r * 0.4f));
        inst.hiss = Make("hiss", 0.7f, (t, r) => r * 0.35f * Mathf.Min(1f, t * 20f) * Mathf.Clamp01((0.7f - t) * 4f));
        return inst;
      }
    }
    static AudioClip Make(string name, float len, System.Func<float, float, float> f){
      const int rate = 22050; int n = Mathf.CeilToInt(len * rate); var d = new float[n]; var rng = new System.Random(name.GetHashCode());
      for (int i = 0; i < n; i++) d[i] = f(i / (float)rate, (float)(rng.NextDouble() * 2 - 1));
      var c = AudioClip.Create(name, n, 1, rate, false); c.SetData(d, 0); return c;
    }
    public static void Click(){ I.src.PlayOneShot(I.click, 0.8f); }
    public static void Hiss(){ I.src.PlayOneShot(I.hiss, 0.7f); }
  }
}
