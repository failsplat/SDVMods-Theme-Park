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

using SDV_ThemePark.ShellGame;

namespace SDV_ThemePark
{
    public class ModEntry : Mod
    {
        private IModHelper modHelper;
        public override void Entry(IModHelper helper)
        {
            this.modHelper = helper;
            this.modHelper.ConsoleCommands.Add("tp_run_minigame", "Starts a minigame from the Theme Park mod.", this.TestGame);
        }

        private void TestGame(string command, string[] args)
        {
            if (!Context.IsPlayerFree) {
                Monitor.Log("To run a minigame Must be in a loaded game and not busy!", LogLevel.Warn);
                return;
            }

            int i;
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
                if (args.Length == 0) { gameName = gameNumbers[0]; } else { gameName = args[0]; }
            }

            switch (gameName)
            {
                case "shell":
                    Game1.currentMinigame = new ShellGame.ShellGame(new StardewValley.Object(392, 1), 10, Monitor, this.modHelper);
                    break;
                default:
                    if (enteredNumber)
                    {
                        Monitor.Log($"Did not find a minigame named: {gameName} (#{i})!", LogLevel.Info);
                    }
                    else
                    {
                        Monitor.Log($"Did not find a minigame named: {gameName}!", LogLevel.Info);
                    }
                    break;

            }
        }

        private static readonly Dictionary<int, string> gameNumbers = new Dictionary<int, string>()
        {
            {0, "shell"},
        };
    }
}
