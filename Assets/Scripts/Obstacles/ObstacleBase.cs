using UnityEngine;
namespace ExplorerDash {
  /// <summary>M2 hook: base class for obstacles. Needs a trigger collider. Default behaviour = stumble.
  /// Override CanAvoidByJump/CanAvoidBySlide or OnPlayerHit for specific obstacle types.</summary>
  public abstract class ObstacleBase : MonoBehaviour, IObstacle {
    public virtual bool CanAvoidByJump => false;
    public virtual bool CanAvoidBySlide => false;
    public virtual void OnPlayerHit(PlayerController player){ player.Stumble(); }
  }
}
