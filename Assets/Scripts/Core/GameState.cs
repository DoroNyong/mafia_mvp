namespace MafiaGame.Core
{
    /// <summary>
    /// 게임 진행 페이즈. Setup -> Day -> Vote -> Night -> (Day로 반복) -> GameOver.
    /// </summary>
    public enum GameState
    {
        Setup,
        Day,
        Vote,
        Night,
        GameOver
    }
}
