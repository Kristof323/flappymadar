using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
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
        private bool gameOver = false;
        private bool pipeScored = false;
        private Random rand = new Random();
        private double rainGravity = 8;
        private bool isRaining = false;
        private bool isFoggy = false;
        private DateTime lastRainToggle = DateTime.Now;
        private DateTime lastFogToggle = DateTime.Now;

        public MainWindow()
        {
            InitializeComponent();
            GameCanvas.Focus();

            gameTimer = new DispatcherTimer();
            gameTimer.Interval = TimeSpan.FromMilliseconds(20);
            gameTimer.Tick += GameLoop;
            gameTimer.Start();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Space && !gameOver)
            {
                velocity = -8 * (isRaining ? 0.7 : 1.0);
            }
            base.OnPreviewKeyDown(e);
        }

        private void GameLoop(object sender, EventArgs e)
        {
            if (gameOver) return;

            UpdateBird();
            UpdatePipes();
            UpdateScore();
            CheckCollisions();
            UpdateWeather();
            UpdateRain();
        }

        private void UpdateBird()
        {
            velocity += gravity;
            Canvas.SetTop(Bird, Canvas.GetTop(Bird) + velocity);

            if (velocity < -2)
                Bird.Source = new BitmapImage(new Uri("/kepek/flappyup.png", UriKind.Relative));
            else if (velocity > 2)
                Bird.Source = new BitmapImage(new Uri("/kepek/flappy.png", UriKind.Relative));
            else
                Bird.Source = new BitmapImage(new Uri("/kepek/flappymid.png", UriKind.Relative));
        }

        private void UpdatePipes()
        {
            double pipeLeft = Canvas.GetLeft(TopPipe);
            Canvas.SetLeft(TopPipe, pipeLeft + pipeSpeed);
            Canvas.SetLeft(BottomPipe, pipeLeft + pipeSpeed);

            if (pipeLeft < -200)
            {
                double gapTop = rand.Next(100, 250);
                Canvas.SetTop(TopPipe, gapTop - 250);
                Canvas.SetTop(BottomPipe, gapTop + 150);
                Canvas.SetLeft(TopPipe, 800);
                Canvas.SetLeft(BottomPipe, 800);
                pipeScored = false;
            }
        }

        private void UpdateScore()
        {
            if (!pipeScored && Canvas.GetLeft(TopPipe) < 50 && Canvas.GetLeft(TopPipe) > -100)
            {
                score++;
                pipeScored = true;
            }
            ScoreText.Text = $"Pont: {(int)score}";
        }

        private void CheckCollisions()
        {
            Rect birdRect = new Rect(Canvas.GetLeft(Bird), Canvas.GetTop(Bird), 50, 40);
            Rect topRect = new Rect(Canvas.GetLeft(TopPipe), Canvas.GetTop(TopPipe), 80, 250);
            Rect bottomRect = new Rect(Canvas.GetLeft(BottomPipe), Canvas.GetTop(BottomPipe), 80, 250);

            if (birdRect.IntersectsWith(topRect) || birdRect.IntersectsWith(bottomRect))
            {
                GameOver();
            }
            else if (Canvas.GetTop(Bird) > 450 || Canvas.GetTop(Bird) < -50)
            {
                velocity = 0;
            }
        }

        private void GameOver()
        {
            gameOver = true;
            GameOverImage.Visibility = Visibility.Visible;
            FinalScoreText.Text = $"Végső pont: {(int)score}";
            FinalScoreText.Visibility = Visibility.Visible;
        }

        private void UpdateWeather()
        {
            if ((DateTime.Now - lastRainToggle).TotalSeconds > 10)
            {
                isRaining = !isRaining;
                lastRainToggle = DateTime.Now;
            }
            if ((DateTime.Now - lastFogToggle).TotalSeconds > 15)
            {
                isFoggy = !isFoggy;
                FogOverlay.Opacity = isFoggy ? 0.4 : 0;
                lastFogToggle = DateTime.Now;
            }
        }

        private void UpdateRain()
        {
            if (!isRaining) return;

            Canvas.SetTop(RainDrop1, Canvas.GetTop(RainDrop1) + rainGravity);
            Canvas.SetTop(RainDrop2, Canvas.GetTop(RainDrop2) + rainGravity);
            Canvas.SetTop(RainDrop3, Canvas.GetTop(RainDrop3) + rainGravity);
            Canvas.SetTop(RainDrop4, Canvas.GetTop(RainDrop4) + rainGravity);

            if (Canvas.GetTop(RainDrop1) > 500) ResetRainDrop1();
            if (Canvas.GetTop(RainDrop2) > 500) ResetRainDrop2();
            if (Canvas.GetTop(RainDrop3) > 500) ResetRainDrop3();
            if (Canvas.GetTop(RainDrop4) > 500) ResetRainDrop4();
        }

        private void ResetRainDrop1() { Canvas.SetLeft(RainDrop1, rand.Next(0, 800)); Canvas.SetTop(RainDrop1, rand.Next(-100, 0)); }
        private void ResetRainDrop2() { Canvas.SetLeft(RainDrop2, rand.Next(0, 800)); Canvas.SetTop(RainDrop2, rand.Next(-100, 0)); }
        private void ResetRainDrop3() { Canvas.SetLeft(RainDrop3, rand.Next(0, 800)); Canvas.SetTop(RainDrop3, rand.Next(-100, 0)); }
        private void ResetRainDrop4() { Canvas.SetLeft(RainDrop4, rand.Next(0, 800)); Canvas.SetTop(RainDrop4, rand.Next(-100, 0)); }
    }
}
