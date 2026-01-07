using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace FlappyBird
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer gameTimer;
        private double gravity = 0.4;
        private double velocity = 0;
        private double pipeSpeed = -5;
        private double score = 0;
        private bool jumped = false;

        public MainWindow()
        {
            InitializeComponent();
            GameCanvas.Focus(); 

            gameTimer = new DispatcherTimer();
            gameTimer.Interval = TimeSpan.FromMilliseconds(20);
            gameTimer.Tick += GameLoop;
            gameTimer.Start();
        }

        private void GameCanvas_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                velocity = -8; 
            }
        }

        private void GameLoop(object sender, EventArgs e)
        {
            velocity += gravity;
            Canvas.SetTop(Bird, Canvas.GetTop(Bird) + velocity);

            Canvas.SetLeft(TopPipe, Canvas.GetLeft(TopPipe) + pipeSpeed);
            Canvas.SetLeft(BottomPipe, Canvas.GetLeft(BottomPipe) + pipeSpeed);

            if (Canvas.GetLeft(TopPipe) < -80)
            {
                Canvas.SetLeft(TopPipe, 800);
                Canvas.SetTop(TopPipe, 0);
                score++;
            }
            if (Canvas.GetLeft(BottomPipe) < -80)
            {
                Canvas.SetLeft(BottomPipe, 800);
                Canvas.SetTop(BottomPipe, 250);
            }

            ScoreText.Text = $"Pont: {(int)score}";

            if (Canvas.GetTop(Bird) > 450 || Canvas.GetTop(Bird) < -50)
            {
                gameTimer.Stop();
                MessageBox.Show("Game Over!");
            }
        }
    }
}
