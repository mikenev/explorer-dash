using UnityEngine;
using UnityEngine.InputSystem;
namespace ExplorerDash {
  /// <summary>Auto-runs forward in 3 lanes. Keyboard + swipe (touch or mouse drag) input via the new Input System.</summary>
  [RequireComponent(typeof(CapsuleCollider))]
  public class PlayerController : MonoBehaviour {
    public float startSpeed = 10f, maxSpeed = 22f, accel = 0.15f;
    public float laneChangeSpeed = 14f, jumpVelocity = 9f, gravity = 28f, slideTime = 0.8f;
    public float stumbleSlowTime = 0.6f, stumbleSpeedFactor = 0.45f;
    public float Speed { get; private set; }
    public bool IsSliding => slideTimer > 0; public bool IsGrounded => y <= 0f && vy <= 0f;
    public int Lane => lane;
    /// <summary>Current collider height (standing 1.8, sliding 0.8) and feet height, for obstacle overlap checks.</summary>
    public float Height => IsSliding ? 0.8f : 1.8f; public float FeetY => y; public const float Radius = 0.4f;
    public bool IsFalling => falling;
    public event System.Action OnStumble;
    int lane = 1; float x, y, vy, slideTimer, stumbleTimer, stumbleCooldown;
    CapsuleCollider col; Transform body; bool falling;
    Vector2 swipeStart; bool swiping; const float SwipeMin = 50f;
    void Awake(){ col = GetComponent<CapsuleCollider>(); body = transform.GetChild(0); }
    public void ResetPlayer(){
      lane = 1; x = 0; y = 0; vy = 0; slideTimer = 0; stumbleTimer = 0; stumbleCooldown = 0; Speed = startSpeed; falling = false;
      transform.position = new Vector3(0, 0, 0); ApplyPose();
    }
    /// <summary>Called by obstacles (IObstacle) or failed actions. Slows the runner so the boulder closes in.</summary>
    public void Stumble(){
      if (stumbleCooldown > 0 || GameManager.I.State != GameState.Playing) return;
      stumbleTimer = stumbleSlowTime; stumbleCooldown = 1.0f;
      OnStumble?.Invoke(); Sfx.Stumble();
    }
    /// <summary>Pit death: ends the run and drops the explorer into the pit.</summary>
    public void FallIntoPit(){
      if (GameManager.I.State != GameState.Playing) return;
      falling = true; vy = 0; slideTimer = 0;
      Sfx.PitFall(); GameManager.I.GameOver("FELL INTO A PIT!");
    }
    void Update(){
      if (falling){ vy -= gravity * Time.deltaTime; y = Mathf.Max(-6f, y + vy * Time.deltaTime); var fp = transform.position; fp.y = y; transform.position = fp; return; }
      if (GameManager.I.State != GameState.Playing) { if (GameManager.I.State == GameState.Ready) ApplyPose(); return; }
      float dt = Time.deltaTime;
      // The Space/click that started the run must not also jump.
      if (Time.frameCount != GameManager.I.StartFrame) ReadInput(); else swiping = false;
      Speed = Mathf.Min(maxSpeed, Speed + accel * dt);
      stumbleCooldown -= dt;
      float sp = Speed;
      if (stumbleTimer > 0){ stumbleTimer -= dt; sp *= stumbleSpeedFactor; }
      x = Mathf.MoveTowards(x, GameConfig.LaneX(lane), laneChangeSpeed * dt);
      if (y > 0 || vy > 0){ vy -= gravity * dt; y += vy * dt; if (y <= 0){ y = 0; vy = 0; } }
      if (slideTimer > 0) slideTimer -= dt;
      var p = transform.position; p.x = x; p.y = y; p.z += sp * dt; transform.position = p;
      ApplyPose();
    }
    void ApplyPose(){
      bool slide = IsSliding;
      col.height = slide ? 0.8f : 1.8f; col.center = new Vector3(0, slide ? 0.4f : 0.9f, 0);
      body.localScale = new Vector3(0.8f, slide ? 0.4f : 0.9f, 0.8f);
      body.localPosition = new Vector3(0, slide ? 0.4f : 0.9f, 0);
      body.localRotation = Quaternion.Euler(slide ? -20f : (stumbleTimer > 0 ? 25f : 0f), 0, 0);
    }
    void Left(){ if (lane > 0) lane--; else Stumble(); }
    void Right(){ if (lane < GameConfig.LaneCount - 1) lane++; else Stumble(); }
    void Jump(){ if (IsGrounded){ vy = jumpVelocity; slideTimer = 0; } }
    void Slide(){ slideTimer = slideTime; if (y > 0) vy = -jumpVelocity * 1.5f; }
    void ReadInput(){
      var k = Keyboard.current;
      if (k != null){
        if (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame) Left();
        if (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame) Right();
        if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame) Jump();
        if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) Slide();
      }
      var t = Touchscreen.current;
      if (t != null && t.primaryTouch.press.isPressed) Swipe(true, t.primaryTouch.position.ReadValue(), t.primaryTouch.press.wasPressedThisFrame);
      else if (Mouse.current != null && Mouse.current.leftButton.isPressed) Swipe(true, Mouse.current.position.ReadValue(), Mouse.current.leftButton.wasPressedThisFrame);
      else swiping = false;
    }
    void Swipe(bool down, Vector2 pos, bool began){
      if (began){ swipeStart = pos; swiping = true; return; }
      if (!swiping) return;
      var d = pos - swipeStart;
      if (d.magnitude < SwipeMin) return;
      swiping = false;
      if (Mathf.Abs(d.x) > Mathf.Abs(d.y)){ if (d.x < 0) Left(); else Right(); }
      else { if (d.y > 0) Jump(); else Slide(); }
    }
    void OnTriggerEnter(Collider other){
      var ob = other.GetComponentInParent<IObstacle>();
      if (ob != null) ob.OnPlayerHit(this);
    }
  }
}
