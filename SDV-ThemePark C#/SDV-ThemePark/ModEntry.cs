using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using HarmonyLib;

namespace SDV_ThemePark
{
    public class ModEntry : Mod
    {
        public override void Entry(IModHelper helper)
        {
            helper.ConsoleCommands.Add("minigame_run", "Starts a minigame from the Theme Park mod.", this.TestGame);
        }

        private void TestGame(string command, string[] args)
        {
            if (!Context.IsPlayerFree) {
                Monitor.Log("To run a minigame Must be in a loaded game and not busy!", LogLevel.Warn);
                return;
            }

            int i = 0;
            string gameName;
            bool enteredNumber = int.TryParse(args[0], out i);
            if (enteredNumber) {
                if (gameNumbers.ContainsKey(i))
                {
                    gameName = gameNumbers[i];
                }
                else
                {
                    Monitor.Log($"Did not find a minigame numbered: {i}!", LogLevel.Info);
                    return;
                }
            }
            else {
                gameName = args[0];
            }

            if (minigameDict.ContainsKey(gameName))
            {
                Monitor.Log($"Running minigame: \"{gameName}\"!", LogLevel.Info);
                Game1.currentMinigame = Activator.CreateInstance(minigameDict[gameName]) as StardewValley.Minigames.IMinigame;

            } 
            else
            {   
                if (enteredNumber) {
                    Monitor.Log($"Did not find a minigame named: {gameName} (#{i})!", LogLevel.Info);
                } else {
                    Monitor.Log($"Did not find a minigame named: {gameName}!", LogLevel.Info); 
                }
            }
        }

        private static readonly Dictionary<int, string> gameNumbers = new Dictionary<int, string>()
        {
            {0, "shell"},
        };

        private static readonly Dictionary<string, Type> minigameDict = new Dictionary<string, Type>()
        {

        };

    }
}
