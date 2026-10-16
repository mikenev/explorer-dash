using UnityEngine;
namespace ExplorerDash {
  /// <summary>M2 hook: base class for obstacles. Default behaviour = stumble.
  /// Override CanAvoidByJump/CanAvoidBySlide or OnPlayerHit for specific obstacle types.
  /// M2 obstacles use deterministic box-overlap checks (HitBox + PlayerOverlaps) each frame instead of physics triggers,
  /// so jump/slide clearance is exact; the M1 StumbleRoot still uses its trigger collider.</summary>
  public abstract class ObstacleBase : MonoBehaviour, IObstacle {
    public virtual bool CanAvoidByJump => false;
    public virtual bool CanAvoidBySlide => false;
    public virtual void OnPlayerHit(PlayerController player){ player.Stumble(); }

    protected bool hitDone;
    protected static PlayerController Player => GameManager.I ? GameManager.I.player : null;
    protected static bool Playing => GameManager.I && GameManager.I.State == GameState.Playing;

    /// <summary>Seconds until the player reaches this obstacle's z (negative = already passed).</summary>
    protected float TimeToReach(){
      var p = Player; if (!p) return 999f;
      return (transform.position.z - p.transform.position.z) / Mathf.Max(1f, p.Speed);
    }

    /// <summary>World-space AABB test against the player's collider box (x/z radius, feet..feet+height).</summary>
    protected static bool PlayerOverlaps(PlayerController p, Vector3 center, Vector3 size){
      var pp = p.transform.position; float r = PlayerController.Radius;
      return Mathf.Abs(pp.x - center.x) < size.x / 2 + r
          && Mathf.Abs(pp.z - center.z) < size.z / 2 + r
          && p.FeetY < center.y + size.y / 2 && p.FeetY + p.Height > center.y - size.y / 2;
    }

    /// <summary>Checks the hit box once per frame; calls OnPlayerHit at most once per obstacle.</summary>
    protected void CheckHit(Vector3 worldCenter, Vector3 size){
      if (hitDone || !Playing) return;
      var p = Player; if (!p) return;
      if (PlayerOverlaps(p, worldCenter, size)){ hitDone = true; OnPlayerHit(p); }
    }

    protected static GameObject Box(Transform parent, Vector3 localPos, Vector3 scale, Material m, PrimitiveType t = PrimitiveType.Cube){
      var g = TrackTile.Prim(parent, t, localPos, scale, m);
      var c = g.GetComponent<Collider>(); if (c) Destroy(c);
      return g;
    }
    static Mesh sphereMesh;
    /// <summary>Collider-free sphere: same look as CreatePrimitive(Sphere) without adding a SphereCollider
    /// (that type is stripped from WebGL builds, so CreatePrimitive logs "Can't add component").</summary>
    protected static GameObject Sphere(Transform parent, Vector3 localPos, Vector3 scale, Material m){
      if (!sphereMesh) sphereMesh = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
      if (!sphereMesh) sphereMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
      var g = new GameObject("Sphere"); g.transform.SetParent(parent, false);
      g.transform.localPosition = localPos; g.transform.localScale = scale;
      g.AddComponent<MeshFilter>().sharedMesh = sphereMesh;
      var r = g.AddComponent<MeshRenderer>();
      if (m) r.sharedMaterial = m;
      return g;
    }
    protected static GameObject Root(Transform parent, Vector3 localPos, string name){
      var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = localPos; return g;
    }
  }
}
