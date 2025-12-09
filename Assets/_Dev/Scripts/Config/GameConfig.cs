using UnityEngine;

namespace Checkers.Config
{
    [CreateAssetMenu(menuName = "Checkers/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("Board")]
        public int rows = 6;
        public int cols = 6;
        public int rowsPerSide = 2;

        [Header("Coin")]
        public int coins = 10;
    }
}
