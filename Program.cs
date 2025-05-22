using System;
using System.Drawing;
using System.Windows.Forms;
using System.Timers;
using System.Collections.Generic;
using Bombermanv2;
using System.Linq;
using System.Media;

namespace bomberman
{
    public class GameForm : Form
    {
        private SoundPlayer gameMusic;
        private int gridSize;
        private const int CellSize = 50;
        private const int ExplosionRange = 4;
        private const int BombTimerMs = 2000;
        private const int ExplosionDisplayMs = 500;
        private DateTime lastBombTime = DateTime.MinValue;

        private readonly int level;
        private PictureBox[,] grid;
        private readonly HashSet<Point> walls = new HashSet<Point>
        {
            new Point(1,1), new Point(1,3), new Point(1,5), new Point(1,7),
            new Point(3,1), new Point(3,3), new Point(3,5), new Point(3,7),
            new Point(5,1), new Point(5,3), new Point(5,5), new Point(5,7),
            new Point(7,1), new Point(7,3), new Point(7,5), new Point(7,7)
        };

        private int playerX, playerY;
        private int aiX, aiY;
        private PictureBox player, ai;
        private readonly Random random = new Random();
        private readonly System.Timers.Timer aiMoveTimer;
        private readonly System.Timers.Timer aiBombTimer;
        private bool gameOver = false;

        // New field: tracks the position of the last bomb placed by the AI
        private Point? lastAIBombPos = null;

        private readonly Image grassImage, wallImage, playerImage, enemyImage, bombImage, explosionImage;

        public GameForm(int selectedLevel)
        {
            level = selectedLevel;

            // Set grid size and wall pattern based on the level.
            switch (level)
            {
                case 1:
                    gridSize = 7;
                    walls = GetWallsLevel1();
                    break;
                case 2:
                    gridSize = 9;
                    walls = GetWallsLevel2();
                    break;
                case 3:
                    gridSize = 11;
                    walls = GetWallsLevel3();
                    break;
            }
            grid = new PictureBox[gridSize, gridSize];

            // Set initial positions (make sure these still work with a variable grid)
            playerX = 0;
            playerY = 0;
            aiX = gridSize - 1;
            aiY = gridSize - 1;

            try
            {
                grassImage = Image.FromFile("grass.png");
                wallImage = Image.FromFile("wall.png");
                playerImage = Image.FromFile("player.png");
                enemyImage = Image.FromFile("enemy.png");
                bombImage = Image.FromFile("bomb.png");
                explosionImage = Image.FromFile("explosion.png");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading images: {ex.Message}");
                Application.Exit();
                return; // Prevent further execution if images fail to load
            }

            InitializeGameComponents();

            try
            {
                gameMusic = new SoundPlayer("GameMusic.wav");
                gameMusic.PlayLooping(); // This plays the audio on an infinite loop.
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading game screen music: {ex.Message}");
            }

            int aiMoveInterval;
            switch (level)
            {
                case 1:
                    aiMoveInterval = 600;
                    break;
                case 2:
                    aiMoveInterval = 450;
                    break;
                default:
                    aiMoveInterval = 300;
                    break;
            }

            int aiBombInterval;
            switch (level)
            {
                case 1:
                    aiBombInterval = 7000;
                    break;
                case 2:
                    aiBombInterval = 5000;
                    break;
                default:
                    aiBombInterval = 3000;
                    break;
            }

            aiMoveTimer = new System.Timers.Timer(aiMoveInterval);
            aiMoveTimer.Elapsed += (sender, e) => MoveAI();
            aiMoveTimer.AutoReset = true;

            // Modified bomb timer for the AI: store its bomb position before placing the bomb.
            aiBombTimer = new System.Timers.Timer(aiBombInterval);
            aiBombTimer.Elapsed += (sender, e) =>
            {
                int bombX = aiX, bombY = aiY;
                lastAIBombPos = new Point(bombX, bombY);
                PlaceBomb(bombX, bombY);
            };
            aiBombTimer.AutoReset = true;

            aiMoveTimer.Start();
            aiBombTimer.Start();
            this.FormClosing += (s, e) =>
            {
                aiMoveTimer?.Dispose();
                aiBombTimer?.Dispose();
            };
        }

        private void InitializeGameComponents()
        {
            SuspendLayout();

            for (int i = 0; i < gridSize; i++)
            {
                for (int j = 0; j < gridSize; j++)
                {
                    grid[i, j] = new PictureBox
                    {
                        Width = CellSize,
                        Height = CellSize,
                        Location = new Point(j * CellSize, i * CellSize),
                        BorderStyle = BorderStyle.FixedSingle,
                        SizeMode = PictureBoxSizeMode.StretchImage,
                        Image = walls.Contains(new Point(j, i)) ? wallImage : grassImage
                    };
                    Controls.Add(grid[i, j]);
                }
            }

            // Create and position your player and AI PictureBoxes.
            player = new PictureBox
            {
                Width = CellSize,
                Height = CellSize,
                BackColor = Color.Transparent,
                Location = new Point(playerX * CellSize, playerY * CellSize),
                Image = playerImage,
                SizeMode = PictureBoxSizeMode.StretchImage
            };
            Controls.Add(player);
            player.BringToFront();

            ai = new PictureBox
            {
                Width = CellSize,
                Height = CellSize,
                BackColor = Color.Transparent,
                Location = new Point(aiX * CellSize, aiY * CellSize),
                Image = enemyImage,
                SizeMode = PictureBoxSizeMode.StretchImage
            };
            Controls.Add(ai);
            ai.BringToFront();

            KeyDown += OnKeyDown;
            ClientSize = new Size(gridSize * CellSize, gridSize * CellSize);
            DoubleBuffered = true;
            ResumeLayout(false);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (gameOver) return;

            switch (e.KeyCode)
            {
                case Keys.Up:
                    TryMovePlayer(playerX, playerY - 1);
                    break;
                case Keys.Down:
                    TryMovePlayer(playerX, playerY + 1);
                    break;
                case Keys.Left:
                    TryMovePlayer(playerX - 1, playerY);
                    break;
                case Keys.Right:
                    TryMovePlayer(playerX + 1, playerY);
                    break;
                case Keys.Space:
                    if ((DateTime.Now - lastBombTime).TotalSeconds >= 2) // 2-second delay between bomb placements
                    {
                        PlaceBomb(playerX, playerY);
                        lastBombTime = DateTime.Now;
                    }
                    break;
            }
        }

        private void TryMovePlayer(int newX, int newY)
        {
            if (IsValidMove(newX, newY))
            {
                playerX = newX;
                playerY = newY;
                player.Location = new Point(playerX * CellSize, playerY * CellSize);
            }
        }

        private bool IsValidMove(int x, int y)
        {
            return x >= 0 && x < gridSize &&
                   y >= 0 && y < gridSize &&
                   !walls.Contains(new Point(x, y));
        }

        private void MoveAI()
        {
            if (gameOver || !IsHandleCreated) return;

            var possibleMoves = new List<Point>
    {
        new Point(aiX, aiY - 1), // Up
        new Point(aiX, aiY + 1), // Down
        new Point(aiX - 1, aiY), // Left
        new Point(aiX + 1, aiY)  // Right
    };

            var safeMoves = possibleMoves.Where(move =>
                                IsValidMove(move.X, move.Y) && !IsRiskFromAIBomb(move.X, move.Y))
                                .ToList();

            IEnumerable<Point> candidateMoves;
            if (safeMoves.Any())
                candidateMoves = safeMoves;
            else
                candidateMoves = possibleMoves.Where(move => IsValidMove(move.X, move.Y));

            Point bestMove = new Point(aiX, aiY);
            double smallestDistance = double.MaxValue;
            foreach (var move in candidateMoves)
            {
                double distance = Math.Abs(move.X - playerX) + Math.Abs(move.Y - playerY);
                if (distance < smallestDistance)
                {
                    smallestDistance = distance;
                    bestMove = move;
                }
            }

            aiX = bestMove.X;
            aiY = bestMove.Y;

            BeginInvoke((MethodInvoker)delegate
            {
                ai.Location = new Point(aiX * CellSize, aiY * CellSize);
            });
        }

        private bool IsRiskFromAIBomb(int x, int y)
        {
            if (!lastAIBombPos.HasValue)
                return false;
            var explosionTiles = GetExplosionTiles(lastAIBombPos.Value.X, lastAIBombPos.Value.Y);
            return explosionTiles.Contains(new Point(x, y));
        }

        private void PlaceBomb(int x, int y)
        {
            if (gameOver || !IsHandleCreated) return;

            BeginInvoke((MethodInvoker)delegate
            {
                var bomb = new PictureBox
                {
                    Width = CellSize,
                    Height = CellSize,
                    Location = new Point(x * CellSize, y * CellSize),
                    BackColor = Color.Transparent,
                    Image = bombImage,
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    Tag = "bomb"
                };

                Controls.Add(bomb);
                bomb.BringToFront();

                var explosionTimer = new System.Timers.Timer(BombTimerMs) { AutoReset = false };
                explosionTimer.Elapsed += (sender, e) => Explode(x, y, bomb);
                explosionTimer.Start();
            });
        }

        private void Explode(int x, int y, PictureBox bomb)
        {
            if (gameOver || !IsHandleCreated) return;

            BeginInvoke((MethodInvoker)delegate
            {
                Controls.Remove(bomb);
                bomb.Dispose();

                // Reset the AI bomb tracker if this explosion is for the AI's bomb.
                if (lastAIBombPos.HasValue && lastAIBombPos.Value.Equals(new Point(x, y)))
                {
                    lastAIBombPos = null;
                }

                if (gameOver) return;

                var explosionTiles = GetExplosionTiles(x, y);
                var explosionEffects = CreateExplosionEffects(explosionTiles);

                foreach (var point in explosionTiles)
                {
                    if (point.X == playerX && point.Y == playerY)
                    {
                        GameOver("Game Over!");
                        return;
                    }
                    if (point.X == aiX && point.Y == aiY)
                    {
                        GameOver("You Won!");
                        return;
                    }
                }

                var clearTimer = new System.Timers.Timer(ExplosionDisplayMs) { AutoReset = false };
                clearTimer.Elapsed += (sender, e) => ClearExplosions(explosionEffects);
                clearTimer.Start();
            });
        }

        private List<Point> GetExplosionTiles(int x, int y)
        {
            var explosionTiles = new List<Point> { new Point(x, y) };

            for (int dir = 0; dir < 4; dir++)
            {
                for (int step = 1; step <= ExplosionRange; step++)
                {
                    int newX = x + (dir < 2 ? (dir == 0 ? step : -step) : 0);
                    int newY = y + (dir >= 2 ? (dir == 2 ? step : -step) : 0);

                    if (!IsValidMove(newX, newY))
                        break;
                    explosionTiles.Add(new Point(newX, newY));
                }
            }

            return explosionTiles;
        }

        private List<PictureBox> CreateExplosionEffects(List<Point> explosionTiles)
        {
            var effects = new List<PictureBox>();

            foreach (var point in explosionTiles)
            {
                var explosion = new PictureBox
                {
                    Width = CellSize,
                    Height = CellSize,
                    Location = new Point(point.X * CellSize, point.Y * CellSize),
                    BackColor = Color.Transparent,
                    Image = explosionImage,
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    Tag = "explosion"
                };

                Controls.Add(explosion);
                explosion.BringToFront();
                effects.Add(explosion);
            }

            return effects;
        }

        private void ClearExplosions(List<PictureBox> explosions)
        {
            if (!IsHandleCreated) return;

            BeginInvoke((MethodInvoker)delegate
            {
                foreach (var explosion in explosions)
                {
                    Controls.Remove(explosion);
                    explosion.Dispose();
                }
            });
        }

        private HashSet<Point> GetWallsLevel1()
        {
            // Configure walls for level 1 (7x7 grid)
            return new HashSet<Point>
            {
                new Point(1, 1), new Point(1, 3), new Point(1, 5),
                new Point(3, 1), new Point(3, 3), new Point(3, 5),
                new Point(5, 1), new Point(5, 3), new Point(5, 5)
            };
        }

        private HashSet<Point> GetWallsLevel2()
        {
            // Configure walls for level 2 (9x9 grid)
            return new HashSet<Point>
            {
                new Point(1, 1), new Point(1, 3), new Point(1, 5), new Point(1, 7),
                new Point(3, 1), new Point(3, 3), new Point(3, 5), new Point(3, 7),
                new Point(5, 1), new Point(5, 3), new Point(5, 5), new Point(5, 7),
                new Point(7, 1), new Point(7, 3), new Point(7, 5), new Point(7, 7)
            };
        }

        private HashSet<Point> GetWallsLevel3()
        {
            // Configure walls for level 3 (11x11 grid)
            return new HashSet<Point>
            {
                new Point(1, 1), new Point(1, 3), new Point(1, 5), new Point(1, 7), new Point(1, 9),
                new Point(3, 1), new Point(3, 3), new Point(3, 5), new Point(3, 7), new Point(3, 9),
                new Point(5, 1), new Point(5, 3), new Point(5, 5), new Point(5, 7), new Point(5, 9),
                new Point(7, 1), new Point(7, 3), new Point(7, 5), new Point(7, 7), new Point(7, 9),
                new Point(9, 1), new Point(9, 3), new Point(9, 5), new Point(9, 7), new Point(9, 9)
            };
        }

        private void GameOver(string message)
        {
            if (gameOver) return;
            gameOver = true;

            // Stop any background music—if you have game music playing.
            gameMusic?.Stop();

            // Decide which sound to play based on the end game message.
            SoundPlayer popUpSound = null;
            if (message.Contains("You Won"))
            {
                // If the player won, load the win music.
                popUpSound = new SoundPlayer("WinMusic.wav");
            }
            else
            {
                // Otherwise, assume a game over.
                popUpSound = new SoundPlayer("LoseMusic.wav");
            }

            try
            {
                // Play the sound once.
                popUpSound.Play();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error playing end game sound: {ex.Message}");
            }

            // Set the dialog result for the game form.
            this.DialogResult = message.Contains("You Won") ? DialogResult.OK : DialogResult.Cancel;

            // Display the game over message and then close the form.
            BeginInvoke((MethodInvoker)delegate
            {
                MessageBox.Show(message, "Game Over", MessageBoxButtons.OK);
                this.Close();
            });
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            aiMoveTimer?.Dispose();
            aiBombTimer?.Dispose();
        }
    }

    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainMenuForm());
        }
    }
}
