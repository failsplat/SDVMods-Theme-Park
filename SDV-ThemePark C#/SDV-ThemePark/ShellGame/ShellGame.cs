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
		public Vector2 upperLeft;
		private int screenWidth;
		private int screenHeight;
		public float pixelScale = 4f;
		private Rectangle viewport;
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
			b.Draw(this.bgtexture, this.viewport, Color.White);
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
			this.screenWidth = 320;
			this.screenHeight = 320;
			float pixel_zoom_adjustment = 1f / Game1.options.zoomLevel;
			int viewport_width = Game1.game1.localMultiplayerWindow.Width;
			int viewport_height = Game1.game1.localMultiplayerWindow.Height;
			this.pixelScale = Math.Min(5f, Math.Min((float)viewport_width * pixel_zoom_adjustment / (float)this.screenWidth, (float)viewport_height * pixel_zoom_adjustment / (float)this.screenHeight));
			float snap = 0.1f;
			this.pixelScale = (float)(int)(this.pixelScale / snap) * snap;
			this.upperLeft = new Vector2((float)(viewport_width / 2) * pixel_zoom_adjustment, (float)(viewport_height / 2) * pixel_zoom_adjustment);
			this.upperLeft.X -= (float)(this.screenWidth / 2) * this.pixelScale;
			this.upperLeft.Y -= (float)(this.screenHeight / 2) * this.pixelScale;
			this.viewport = new Rectangle((int)this.upperLeft.X, (int)this.upperLeft.Y, viewport_width, viewport_height);

			this.startButtonPos.X = (int)(0.3 * viewport_width + this.upperLeft.X);
			this.startButtonPos.Width = (int)(0.4 * viewport_width);
			this.startButtonPos.Y = (int)(0.2 * viewport_height + this.upperLeft.Y);
			this.startButtonPos.Height = (int)(0.2 * viewport_height);
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

