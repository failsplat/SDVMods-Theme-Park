using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Minigames;
using StardewModdingAPI;

namespace SDV_ThemePark.ShellGame {
	public class ShellGame : IMinigame
	{
		private IMonitor monitor;
		private IModHelper modHelper;
		
		// Gameplay variables
		public enum GameState
		{
			WaitToStart, // Waiting for player to press start
			RevealStart, // Showing the player the prize under a shell
			SwapShells, // Moving shells around
			WaitForPick, // Done moving, waiting for player to select shell
			RevealPick, // Reveal shell
			GameOver, // Show "Win" or "Lose" message
		};
		public GameState currentGameState = GameState.WaitToStart;
		public int MaxSwaps;
		public int RemainingSwaps;
		public StardewValley.Object prizeObject; // Object to be hidden in a shell, (?)and potentially won by player.
		public Pos prizePos;

		// Screen managment
		public float pixelScale = 4f;
		public double minigameScale = 0.8;
		private Vector2 topLeft;
		private Rectangle minigameWindow;

		// Positioning variables
		public enum Pos
		{
			Left,
			Center,
			Right,
		};
		// Pixel location and size of the shells (when not moving)
		public System.Collections.Generic.Dictionary<Pos, Vector2> ShellRestPositions = new System.Collections.Generic.Dictionary<Pos, Vector2>();
		public Vector2 ShellSize;
		public Rectangle startButtonPos;
		// Recalculate these when the screen is resized

		// Kinematics variables
		public int t_shift; // Time parameter for moving shells (as they swap, etc.)
		public int swapTime; // Time it takes to swap (speeds up as more swaps), should be at least 0.2 seconds
		public static int raiseLowerTime = 30; // Raising/lowering shell
		// Times are in update ticks (60 ticks per second)
		// When two shells are swapped, they travel in an ellipse
		public static double swapEccentricity = 0.85;
		public static double axisRatio = Math.Pow(Math.Pow(swapEccentricity,2) - 1,0.5);
		// Define which positions are swapped, and direction of travel
		// ex: swapToUpper is Left, swapToLower is Right = counterclockwise swap between left and right shells
		// Shell sprites should be drawn so that "lower" shell is in front
		public Pos swapToUpper;
		public Pos swapToLower;

		// Textures
		private Texture2D bgtexture;
		private Texture2D startbuttontexture;

		public ShellGame(StardewValley.Object prize, int swaps, IMonitor monitor, IModHelper helper)
		{
			this.changeScreenSize();
			this.prizeObject = prize;
			this.MaxSwaps = swaps;
			this.RemainingSwaps = swaps;
			this.monitor = monitor;
			this.modHelper = helper;
			this.monitor.Log($"Shell Game starting! Prize:{prize.Name}, Swaps{swaps}");
			this.bgtexture = this.modHelper.Content.Load<Texture2D>("assets/ShellGame/background.png");
			this.startbuttontexture = this.modHelper.Content.Load<Texture2D>("assets/ShellGame/startbutton.png");

			// Todo: Game initialization stuff

			this.TransitionGameState(GameState.WaitToStart);
		}

		private void TransitionGameState(GameState new_state)
        {
			switch (new_state)
            {
				case GameState.WaitToStart:
					this.currentGameState = new_state;
					LogGameStateTransition(new_state, false, LogLevel.Trace);
					break;
				default:
					LogGameStateTransition(new_state, false, LogLevel.Error);
					break;
            }
        }

		private void LogGameStateTransition(GameState new_state, bool log_current_state, LogLevel log_level)
        {
			if (log_current_state) { 
				this.monitor.Log($"Unimplemented game state change! \"{Enum.GetName(typeof(GameState), this.currentGameState)}\"->\"{Enum.GetName(typeof(GameState), new_state)}\"", log_level); 
			} else
            {
				this.monitor.Log($"Unimplemented game state change! ->\"{Enum.GetName(typeof(GameState), new_state)}\"", log_level);
			}
				
		}

		public bool overrideFreeMouseMovement()
		{
			return false;
		}

		public bool tick(GameTime time)
		{
			switch (this.currentGameState)
            {
				case GameState.WaitToStart:
					// Continue waiting
					return false;
				default:
					this.monitor.Log($"Unhandled game state \"{Enum.GetName(typeof(GameState), this.currentGameState)}\" for tick() in minigame \"{this.minigameId()}\"", 
						LogLevel.Warn);
					this.monitor.Log($"Ending the minigame!", LogLevel.Warn);
					return true;
            }
		}

		public virtual bool IsAiming()
		{
			return false;
		}

		public float GetRadiusFromCharge()
		{
			return 0;
		}

		public void receiveLeftClick(int x, int y, bool playSound = true)
		{
		}

		public void releaseLeftClick(int x, int y)
		{
		}

		public virtual int GetPointsForAim()
		{
			return 0;
		}

		public virtual void FireDart(float radius)
		{
		}

		public void releaseRightClick(int x, int y)
		{
		}

		public void receiveRightClick(int x, int y, bool playSound = true)
		{
		}

		public void receiveKeyPress(Keys k)
		{
		}

		public void receiveKeyRelease(Keys k)
		{
		}

		public void QuitGame()
		{
		}

		public void draw(SpriteBatch b)
		{
			b.Begin();
			b.Draw(this.bgtexture, this.minigameWindow, Color.White);
			switch (this.currentGameState)
            {
				case (GameState.WaitToStart):
					b.Draw(this.startbuttontexture, this.startButtonPos, Color.White);
					break;
				default:
					break;
            }
			b.End();
		}

		public void changeScreenSize()
		{
			// Screen Management
			int window_width = Game1.game1.localMultiplayerWindow.Width;
			int window_height = Game1.game1.localMultiplayerWindow.Height;
			this.topLeft = Utility.getTopLeftPositionForCenteringOnScreen((int)(this.minigameScale * window_width), (int)(this.minigameScale * window_height));
			this.minigameWindow = new Rectangle((int) this.topLeft.X, (int) this.topLeft.Y, (int)(this.minigameScale * window_width), (int)(this.minigameScale * window_height));

			this.startButtonPos.X = (int)(0.3 * this.minigameWindow.Width + this.topLeft.X);
			this.startButtonPos.Width = (int)(0.4 * this.minigameWindow.Width);
			this.startButtonPos.Y = (int)(0.2 * this.minigameWindow.Height + this.topLeft.Y);
			this.startButtonPos.Height = (int)(0.2 * this.minigameWindow.Height);
		}

		public void unload()
		{
			Game1.stopMusicTrack(Game1.MusicContext.MiniGame);
			Game1.player.faceDirection(0);
		}

		public bool forceQuit()
		{
			this.unload();
			return true;
		}

		public void leftClickHeld(int x, int y)
		{
		}

		public void receiveEventPoke(int data)
		{
			throw new NotImplementedException();
		}

		public string minigameId()
		{
			return "ShellGame";
		}

		public bool doMainGameUpdates()
		{
			return false;
		}
	}

}

