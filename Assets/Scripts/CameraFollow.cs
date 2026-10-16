using UnityEngine;
namespace ExplorerDash {
  /// <summary>Smooth over-the-shoulder follow camera.</summary>
  public class CameraFollow : MonoBehaviour {
    public Transform target;
    public Vector3 offset = new Vector3(0.9f, 3.2f, -6.5f);
    public float lookAhead = 8f, smoothTime = 0.12f, lateralFollow = 0.6f;
    Vector3 vel;
    /// <summary>Camera shake amplitude in metres (set by GameManager during the intro rumble / boulder closing in).</summary>
    public static float Shake;
    void LateUpdate(){
      if (!target) return;
      var t = target.position;
      var desired = new Vector3(t.x * lateralFollow + offset.x, offset.y + t.y * 0.3f, t.z + offset.z);
      var p = transform.position; 
      p.x = Mathf.SmoothDamp(p.x, desired.x, ref vel.x, smoothTime);
      p.y = Mathf.SmoothDamp(p.y, desired.y, ref vel.y, smoothTime * 2);
      p.z = desired.z; // lock z so speed never causes lag/jitter
      transform.position = p + (Shake > 0 ? (Vector3)(Random.insideUnitCircle * Shake) : Vector3.zero);
      var look = new Vector3(t.x * 0.8f, 1.2f, t.z + lookAhead);
      transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look - p), 1 - Mathf.Exp(-12f * Time.deltaTime));
    }
  }
}
