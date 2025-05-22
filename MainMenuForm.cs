using System;
using System.Media;
using System.Windows.Forms;

namespace bomberman
{
    public partial class MainMenuForm : Form
    {
        private bool gameIsRunning = false;

        private SoundPlayer menuMusic;

        public MainMenuForm()
        {
            InitializeComponent();

            // Load and play the main menu music (ensure the file is a WAV file in the correct location)
            try
            {
                menuMusic = new SoundPlayer("MainMenuMusic.wav");
                menuMusic.PlayLooping();  // Loops infinitely
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading main menu music: {ex.Message}");
            }

            // Wiring up button events remains the same...
            Level1.Click += Level1_Click;
            Level2.Click += Level2_Click;
            Level3.Click += Level3_Click;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Stop the music when leaving the main menu
            menuMusic?.Stop();
            base.OnFormClosing(e);
        }

        private void Level1_Click(object sender, EventArgs e)
        {
            if (!Level1.Enabled) return;
            StartGame(1);
        }

        private void Level2_Click(object sender, EventArgs e)
        {
            if (!Level2.Enabled) return;
            StartGame(2);
        }

        private void Level3_Click(object sender, EventArgs e)
        {
            if (!Level3.Enabled) return;
            StartGame(3);
        }

        private void StartGame(int level)
        {
            if (gameIsRunning) return;
            gameIsRunning = true;

            try
            {
                Level1.Enabled = Level2.Enabled = Level3.Enabled = false;

                // Stop the main menu music before hiding the form.
                menuMusic?.Stop();

                this.Hide();

                using (var game = new GameForm(level))
                {
                    var result = game.ShowDialog();
                }

                Application.Exit();
            }
            finally
            {
                gameIsRunning = false;
            }
        }

        private void MainMenuForm_Load(object sender, EventArgs e)
        {

        }
    }
}
