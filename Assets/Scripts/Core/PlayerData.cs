using System;

namespace MafiaGame.Core
{
    /// <summary>
    /// 플레이어 한 명의 상태를 담는 순수 데이터 클래스.
    /// 서버-권위(멀티플레이) 전환을 고려해 입력/뷰 로직을 포함하지 않는다.
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        public int Id { get; private set; }
        public string Name { get; private set; }
        public Role Role { get; set; }
        public bool IsAlive { get; private set; }
        public bool IsAI { get; private set; }

        public PlayerData(int id, string name, Role role, bool isAI)
        {
            Id = id;
            Name = name;
            Role = role;
            IsAI = isAI;
            IsAlive = true;
        }

        public void Kill()
        {
            IsAlive = false;
        }

        public void Revive()
        {
            IsAlive = true;
        }
    }
}
