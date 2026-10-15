namespace ExplorerDash {
  /// <summary>M2 hook: anything the player can collide with. Implement OnPlayerHit (e.g. call player.Stumble() or end the run).</summary>
  public interface IObstacle {
    void OnPlayerHit(PlayerController player);
  }
}
