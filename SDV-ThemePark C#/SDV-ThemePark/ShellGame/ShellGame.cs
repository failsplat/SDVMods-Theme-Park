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
		public Pos pickPos;
		private readonly Random random;

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
		public static int num_pos = Enum.GetNames(typeof(Pos)).Length;
		// Pixel location and size of the shells (when not moving)
		public System.Collections.Generic.Dictionary<Pos, Rectangle> ShellRestPositions = new System.Collections.Generic.Dictionary<Pos, Rectangle>();
		public System.Collections.Generic.List<Rectangle> ShellPositions = new System.Collections.Generic.List<Rectangle>();
		public Vector2 ShellSize;
		public Rectangle startButtonPos;
		public Rectangle exitButtonPos;
		public System.Collections.Generic.Dictionary<Pos, Rectangle> PrizeDisplayPositions = new System.Collections.Generic.Dictionary<Pos, Rectangle>();
		// Recalculate these when the screen is resized

		// Kinematics variables
		public double t_shift; // Time parameter for moving shells (as they swap, etc.)
		public static int swapTimeMin = 30;
		public static int swapTimeMax = 60;
		public int swapTime; // Time it takes to swap (speeds up as more swaps), should be at least 0.2 seconds
		public static double raiseLowerTime = 45; // Raising/lowering shell
											   // Times are in update ticks (60 ticks per second)
											   // When two shells are swapped, they travel in an ellipse
		public static double raisePauseTime = 20;
		public static double raiseLowerHeight = 0.3; // How high, in screen height, to raise and lower shells
		public static double swapEccentricity = 0.92;
		public static double axisRatio = Math.Pow((1 - Math.Pow(ShellGame.swapEccentricity, 2)), 0.5);
		// Define which positions are swapped, and direction of travel
		// ex: swapToUpper is Left, swapToLower is Right = counterclockwise swap between left and right shells
		// Shell sprites should be drawn so that "lower" shell is in front
		public Pos swapToUpper;
		public Pos swapToLower;
		public Pos swapLeft, swapRight;
		public Pos not_swapped_pos;
		public bool swap_cw;
		public int swap_center_x, swap_center_y;
		public int swap_r;
		

		// Textures
		private Texture2D bgtexture;
		private Texture2D startbuttontexture;
		private Texture2D shelltexture;
		private Texture2D exitbuttontexture;
		private Rectangle prizeTileSheetRect;

		public ShellGame(StardewValley.Object prize, int swaps, IMonitor monitor, IModHelper helper)
		{
			this.changeScreenSize();
			this.prizeObject = prize;
			this.MaxSwaps = swaps;
			this.RemainingSwaps = swaps;
			this.monitor = monitor;
			this.modHelper = helper;
			this.monitor.Log($"Shell Game starting! Prize:{prize.Name}, Swaps{swaps}");
			this.bgtexture = this.modHelper.ModContent.Load<Texture2D>("assets/ShellGame/background.png");
			this.startbuttontexture = this.modHelper.ModContent.Load<Texture2D>("assets/ShellGame/startbutton.png");
			this.shelltexture = this.modHelper.ModContent.Load<Texture2D>("assets/ShellGame/shell.png");
			this.exitbuttontexture = this.modHelper.ModContent.Load<Texture2D>("assets/ShellGame/exitbutton.png");
			this.prizeTileSheetRect = Game1.getSourceRectForStandardTileSheet(Game1.objectSpriteSheet, prize.ParentSheetIndex, 16, 16);
			this.random = new Random();
			//this.monitor.Log($"Ecc, Axis Ratio: {ShellGame.swapEccentricity}, {ShellGame.axisRatio}", LogLevel.Debug);

			this.TransitionGameState(GameState.WaitToStart);
		}

		private void TransitionGameState(GameState new_state)
		{
			if ((new_state - this.currentGameState) > 1)
            {
				LogGameStateTransition(new_state, true, true, GameStateMessage.Skip);
            } else if ((new_state - this.currentGameState) < 0)
			{
				LogGameStateTransition(new_state, true, true, GameStateMessage.Regress);
			}
			else if ((new_state == this.currentGameState) && (this.currentGameState != GameState.WaitToStart))
			{
				LogGameStateTransition(new_state, true, true, GameStateMessage.Already);
			}

			switch (new_state)
			{
				case GameState.WaitToStart:
					this.CalculateShellPositions(); // Calculate ONCE at start, then wait until movement starts to calculate on each tick
					break;
				case GameState.RevealStart:
					this.prizePos = Pos.Center;
					this.t_shift = 0;
					break;
				case GameState.SwapShells:
					this.t_shift = 0;
					this.SetupSwapMotion();
					break;
				case GameState.WaitForPick:
					this.CalculateShellPositions(); // Calculate ONCE at start, then wait until movement starts to calculate on each tick
					break;
				case GameState.RevealPick:
					break;
				default:
					LogGameStateTransition(new_state, true, true, GameStateMessage.Unimplemented);
					return;
			}
			LogGameStateTransition(new_state, true, false, GameStateMessage.Normal);
			this.currentGameState = new_state;
		}

		public class GameStateMessage
        {
			public static readonly string Unimplemented = "Unimplemented game state change!";
			public static readonly string Normal = "Game state change:";
			public static readonly string Error = "Error in game state change!";
			public static readonly string Skip = "TransitionGameState called to skip phases!";
			public static readonly string Already = "TransitionGameState called with already active phase!";
			public static readonly string Regress = "TransitionGameState called to regress phases!";
		}

		private void LogGameStateTransition(GameState new_state, bool log_current_state, bool is_error, string message)
        {
			string log_message = message;
			LogLevel log_level = is_error ? LogLevel.Error : LogLevel.Debug;
			if (log_current_state && new_state != this.currentGameState) {
				message += $" \"{Enum.GetName(typeof(GameState), this.currentGameState)}\"->\"{Enum.GetName(typeof(GameState), new_state)}\"";
			} else
            {
				message += $"->\"{Enum.GetName(typeof(GameState), new_state)}\"";
			}
			this.monitor.Log(message, log_level);
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
				case GameState.WaitForPick:
					// Continue waiting
					return false;
				case GameState.RevealStart:
					this.t_shift++;
					if (this.t_shift > (2 * (ShellGame.raiseLowerTime + ShellGame.raisePauseTime)))
                    {
						this.TransitionGameState(GameState.SwapShells);
                    }
					this.CalculateShellPositions();
					return false;
				case GameState.SwapShells:
					this.t_shift++;
					if (this.t_shift > this.swapTime)
                    {
						this.t_shift = 0;
						this.RemainingSwaps--;
						this.SetupSwapMotion();
						if (this.RemainingSwaps <= 0)
                        {
							this.TransitionGameState(GameState.WaitForPick);
                        }
                    }
					this.CalculateShellPositions();
					return false;
				case GameState.RevealPick:
					return false;
				default:
					this.monitor.Log($"Unhandled game state \"{Enum.GetName(typeof(GameState), this.currentGameState)}\" for tick() in minigame \"{this.minigameId()}\"", 
						LogLevel.Warn);
					return true;
            }
		}

		private void CalculateShellPositions()
        {
			this.ShellPositions.Clear();
			switch (this.currentGameState)
			{
				case GameState.WaitToStart:
				case GameState.WaitForPick:
					foreach (System.Collections.Generic.KeyValuePair<Pos, Rectangle> entry in this.ShellRestPositions)
					{
						this.ShellPositions.Add(entry.Value);
					}
					break;
				case GameState.RevealStart:
					int y_shift;
					int max_y_shift = (int)(ShellGame.raiseLowerHeight * this.minigameWindow.Height);

					double[] movement_times = {
						ShellGame.raisePauseTime,
						ShellGame.raiseLowerTime + ShellGame.raisePauseTime,
						ShellGame.raiseLowerTime + 2*ShellGame.raisePauseTime,
					};

					if (this.t_shift < movement_times[0])
                    { // Initial pause after pressing Start
						y_shift = 0;
                    }
					else if (this.t_shift < movement_times[1])
					{ // Raising
						y_shift = (int)(max_y_shift * (this.t_shift-movement_times[0]) / ShellGame.raiseLowerTime);
					} 
					else if (this.t_shift < movement_times[2])
					{ // At height, pause
						y_shift = max_y_shift;
					} else
                    { // Lowering
						y_shift = (int)(max_y_shift * (1-((this.t_shift - movement_times[2]) / ShellGame.raiseLowerTime)));
					}
					this.ShellPositions.Add(this.ShellRestPositions[Pos.Left]);
					this.ShellPositions.Add(this.ShellRestPositions[Pos.Right]);
					Rectangle center_shell = this.ShellRestPositions[Pos.Center];
					center_shell.Y -= y_shift;
					this.ShellPositions.Add(center_shell);
					break;
				case GameState.SwapShells:
					double phi = Math.PI * ((double)this.t_shift / (double)this.swapTime); // Phi = angle of rotation, goes from 0 to pi over swapTime
					double thl, thr;
					thl = this.swap_cw ? Math.PI - phi : Math.PI + phi;
					thr = this.swap_cw ? -phi : phi;

					double xl, yl;
					double xr, yr;
					(xl, yl) = SwapEllipseParametric((double)this.swap_center_x, (double)this.swap_center_y, (double) this.swap_r, thl, ShellGame.axisRatio);
					(xr, yr) = SwapEllipseParametric((double)this.swap_center_x, (double)this.swap_center_y, (double)this.swap_r, thr, ShellGame.axisRatio);

					if (this.swap_cw)
                    {
						this.ShellPositions.Add(new Rectangle((int)xl, (int)yl, (int)this.ShellSize.X, (int)this.ShellSize.Y));
						this.ShellPositions.Add(this.ShellRestPositions[this.not_swapped_pos]);
						this.ShellPositions.Add(new Rectangle((int)xr, (int)yr, (int)this.ShellSize.X, (int)this.ShellSize.Y));

						// Motion tracing
						//this.monitor.Log($"L:{this.ShellPositions[0].X}, {this.ShellPositions[0].Y}", LogLevel.Trace);
						//this.monitor.Log($"C:{this.ShellPositions[1].X}, {this.ShellPositions[1].Y}", LogLevel.Trace);
						//this.monitor.Log($"R:{this.ShellPositions[2].X}, {this.ShellPositions[2].Y}", LogLevel.Trace);
					} else
                    {
						this.ShellPositions.Add(new Rectangle((int)xr, (int)yr, (int)this.ShellSize.X, (int)this.ShellSize.Y));
						this.ShellPositions.Add(this.ShellRestPositions[this.not_swapped_pos]);
						this.ShellPositions.Add(new Rectangle((int)xl, (int)yl, (int)this.ShellSize.X, (int)this.ShellSize.Y));

						// Motion tracing
						//this.monitor.Log($"L:{this.ShellPositions[2].X}, {this.ShellPositions[0].Y}", LogLevel.Trace);
						//this.monitor.Log($"C:{this.ShellPositions[1].X}, {this.ShellPositions[1].Y}", LogLevel.Trace);
						//this.monitor.Log($"R:{this.ShellPositions[0].X}, {this.ShellPositions[2].Y}", LogLevel.Trace);
					}
					break;
				default:
					break;
            }
        }

		public (double,double) SwapEllipseParametric(double cx, double cy, double r, double th, double axisRatio)
        {
			double rx = r * Math.Cos(th);
			double ry = (r * Math.Sin(th)) * ShellGame.axisRatio;
			//this.monitor.Log($"SwapEllipseParametric {rx}, {ry}", LogLevel.Debug); // Motion tracing
			return (cx+rx, cy+ry);
        }

		private void SetupSwapMotion()
		{
			this.swapTime = (int)(((double)this.RemainingSwaps / (double)this.MaxSwaps) * ((double)(ShellGame.swapTimeMax - ShellGame.swapTimeMin)) + ShellGame.swapTimeMin);
			//this.monitor.Log($"Swap time{this.swapTime}", LogLevel.Debug); // Motion tracing
			not_swapped_pos = (Pos)this.random.Next(num_pos);
			System.Collections.Generic.List<Pos> pp = new System.Collections.Generic.List<Pos>();
			for (int p = 0; p < num_pos; p++)
			{
				if (p != (int)not_swapped_pos) { pp.Add((Pos)p); }
			}
			this.swapLeft = pp[0];
			this.swapRight = pp[1];
			if (this.random.Next(2) == 1) { pp.Reverse(); }
			this.swapToLower = pp[0];
			this.swapToUpper = pp[1];
			if (this.prizePos == this.swapToLower)
            {
				this.prizePos = this.swapToUpper;
			} else if (this.prizePos == this.swapToUpper)
            {
				this.prizePos = this.swapToLower;
			}

			this.swap_cw = this.swapToLower > this.swapToUpper;
			this.swap_center_x = (int)(0.5 * ( this.ShellRestPositions[this.swapToLower].X + this.ShellRestPositions[this.swapToUpper].X));
			this.swap_center_y = this.ShellRestPositions[Pos.Center].Y;
			this.swap_r = this.ShellRestPositions[this.swapToLower].X - this.swap_center_x;

			//this.monitor.Log($"Center({this.RemainingSwaps}: {this.swap_center_x},{this.swap_center_y}", LogLevel.Trace);// Motion tracing
		}

		public void receiveLeftClick(int x, int y, bool playSound = true)
		{
			if (this.exitButtonPos.Contains(x, y)) {
				this.forceQuit();
				this.monitor.Log($"Exit button pressed, ending the minigame!", LogLevel.Info);
				return;
            }
			switch (this.currentGameState)
			{
				case GameState.WaitToStart:
					if (this.startButtonPos.Contains(x, y)) { this.TransitionGameState(GameState.RevealStart); };
					break;
				case GameState.WaitForPick:
					for (int p = 0; p < num_pos; p++)
					{
						Pos pos = (Pos)p;
						if (this.ShellRestPositions[pos].Contains(x, y)) { 
							this.pickPos = pos;
							this.TransitionGameState(GameState.RevealPick);
						}
					}
					break;
				default:
					break;
			}
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
			Game1.currentMinigame = null;
		}

		public void draw(SpriteBatch b)
		{
			b.Begin();
			b.Draw(this.bgtexture, this.minigameWindow, Color.White);
			b.Draw(this.exitbuttontexture, this.exitButtonPos, Color.White);
			switch (this.currentGameState)
            {
				case (GameState.WaitToStart):
					b.Draw(this.startbuttontexture, this.startButtonPos, Color.White);
					this.DrawShells(b);
					break;
				case (GameState.RevealStart):
					this.DrawPrize(b);
					this.DrawShells(b);
					break;
				case (GameState.SwapShells):
					this.DrawPrize(b); // For tracking prize position while testing/development, comment out later
					this.DrawShells(b);
					break;
				case (GameState.WaitForPick):
					this.DrawPrize(b);
					this.DrawShells(b);
					break;
				default:
					break;
            }
			b.End();
		}

		private void DrawShells(SpriteBatch b)
        {
			foreach (Rectangle sp in this.ShellPositions) 
            {
				b.Draw(this.shelltexture, sp, Color.White);
            }
        }

		private void DrawPrize(SpriteBatch b)
        {
			Rectangle pp = this.PrizeDisplayPositions[this.prizePos];
			b.Draw(Game1.objectSpriteSheet, pp, this.prizeTileSheetRect, Color.White);
			//b.Draw(this.exitbuttontexture, pp, Color.White);
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

			int exit_button_size = Math.Max(32, (int)(window_width * 0.05));
			this.exitButtonPos.X = (int) (this.minigameWindow.Width + this.topLeft.X - exit_button_size);
			this.exitButtonPos.Width = exit_button_size;
			this.exitButtonPos.Y = (int) (this.topLeft.Y);
			this.exitButtonPos.Height = exit_button_size;

			this.CalcShellRestPositions();
			this.CalcPrizeDisplayPositions();
		}

		private void CalcShellRestPositions()
        {
			// Size/Positioning Parameters
			double shell_ww = 0.2; // width relative to game window
			double start_X_ww = 0.1; // Horiz pos of leftmost shell, in window-widths

			int mg_window_width = this.minigameWindow.Width;
			
			int shell_width = (int)(mg_window_width * shell_ww);
			int shell_height = (int)(0.75 * shell_width); // 4:3 w:h ratio for sprite

			this.ShellSize = new Vector2(x: shell_width, y: shell_height);

			int shell_Y = (int)(0.35 * this.minigameWindow.Height + this.topLeft.Y);
			int shell_X;

			// Size of each gap between shells, in window-widths
			double gap_X_ww = num_pos>1?(1 - (2*start_X_ww) - (num_pos * shell_ww))/(num_pos-1):0;
			

			this.ShellRestPositions.Clear();
			for (int p = 0; p < num_pos; p++)
            {
				shell_X = (int) ((start_X_ww + p * (gap_X_ww + shell_ww)) * mg_window_width);
				// Adjustment for Top Left corner done here!
				// Calculating current shell positions, and resting prize positions, and drawing shells/prizes base their positions on this, so no adjustment needed in those!
				this.ShellRestPositions[(Pos)p] = new Rectangle((int)(shell_X+ this.topLeft.X), (int)(shell_Y+ this.topLeft.Y), shell_width, shell_height);
            }
		}

		private void CalcPrizeDisplayPositions()
        {
			this.PrizeDisplayPositions.Clear();
			double prize_ww = 0.08;
			int mg_window_width = this.minigameWindow.Width;
			int prize_width = (int)(mg_window_width * prize_ww);

			Rectangle shellpos;
			int shellcenter_x, shellcenter_y, prizetopleft_x, prizetopleft_y;

			for (int p = 0; p < num_pos; p++)
			{
				shellpos = this.ShellRestPositions[(Pos)p];
				shellcenter_x = (int)(shellpos.X + 0.5 * shellpos.Width);
				shellcenter_y = (int)(shellpos.Y + 0.5 * shellpos.Height);

				prizetopleft_x = (int)(shellcenter_x - 0.5 * prize_width);
				prizetopleft_y = (int)(shellcenter_y - 0.5 * prize_width);

				this.PrizeDisplayPositions[(Pos)p] = new Rectangle(prizetopleft_x, prizetopleft_y, prize_width, prize_width);
			}
		}

		public void unload()
		{
			Game1.stopMusicTrack(Game1.MusicContext.MiniGame);
			Game1.player.faceDirection(0);
		}

		public bool forceQuit()
		{
			this.unload();
			this.QuitGame();
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

