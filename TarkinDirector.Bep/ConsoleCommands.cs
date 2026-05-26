using Comfort.Common;
using EFT;
using EFT.Console.Core;

namespace tarkin.Director.EFTRuntime
{
    [ConsoleGroup("director")]
    internal class ConsoleCommands
    {
        [ConsoleCommand("munch")]
        public static void RestoreEnergyAndHydration()
        {
            if (Singleton<GameWorld>.Instantiated)
            {
                Singleton<GameWorld>.Instance.MainPlayer.ActiveHealthController.ChangeHydration(110);
                Singleton<GameWorld>.Instance.MainPlayer.ActiveHealthController.ChangeEnergy(110);
            }
        }

        [ConsoleCommand("heal")]
        public static void RestoreHealth()
        {
            if (Singleton<GameWorld>.Instantiated)
                Singleton<GameWorld>.Instance.MainPlayer.ActiveHealthController.RestoreFullHealth();
        }
    }
}
