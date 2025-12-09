using UnityEngine;

namespace Checkers.Progress
{
    public static class PlayerProgress
    {
        private const string CoinsKey = "Checkers_Coins";

        public static int Coins
        {
            get => PlayerPrefs.GetInt(CoinsKey, 0);
            private set
            {
                PlayerPrefs.SetInt(CoinsKey, value);
                PlayerPrefs.Save();
            }
        }

        public static void AddCoins(int amount)
        {
            if (amount <= 0) return;
            Coins = Coins + amount;
        }
    }
}
