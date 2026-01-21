using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FlappyBird
{
    public partial class MainWindow : Window
    {
        // Játék paraméterek
        private DispatcherTimer gameTimer;
        private double gravity = 0.4;
        private double velocity = 0;
        private double pipeSpeed = -5;
        private int score = 0;
        private bool gameOver = false;
        private Random rand = new Random();

        // Eső paraméterek
        private double rainGravity = 8;
        private bool isRaining = false;
        private DateTime lastRainToggle = DateTime.Now;
        private List<Rectangle> rainDrops = new List<Rectangle>();
        private const int RAIN_DROP_COUNT = 15;

        // Köd paraméterek
        private bool isFoggy = false;
        private DateTime lastFogToggle = DateTime.Now;

        // Csövek kezelése
        private List<PipePair> pipes = new List<PipePair>();
        private DateTime lastPipeSpawn = DateTime.Now;
        private const double PIPE_SPAWN_INTERVAL = 2000; // 2 másodperc

        // Konstansok
        private const int BIRD_WIDTH = 50;
        private const int BIRD_HEIGHT = 40;
        private const int PIPE_WIDTH = 80;
        private const int PIPE_HEIGHT = 250;
        private const int PIPE_GAP = 150;
        private const int CANVAS_HEIGHT = 534;
        private const int CANVAS_WIDTH = 850;

        public MainWindow()
        {
            InitializeComponent();
            GameCanvas.Focus();

            // Esőcseppek inicializálása
            InitializeRain();

            // Játéktimer
            gameTimer = new DispatcherTimer();
            gameTimer.Interval = TimeSpan.FromMilliseconds(20);
            gameTimer.Tick += GameLoop;
            gameTimer.Start();

            // Első cső pár
            SpawnPipe();
        }

        private void InitializeRain()
        {
            for (int i = 0; i < RAIN_DROP_COUNT; i++)
            {
                Rectangle raindrop = new Rectangle
                {
                    Width = 2,
                    Height = 20,
                    Fill = System.Windows.Media.Brushes.LightBlue,
                    Opacity = 0.6
                };

                Canvas.SetLeft(raindrop, rand.Next(0, CANVAS_WIDTH));
                Canvas.SetTop(raindrop, rand.Next(-100, CANVAS_HEIGHT));

                GameCanvas.Children.Add(raindrop);
                rainDrops.Add(raindrop);
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                if (gameOver)
                {
                    RestartGame();
                }
                else
                {
                    Jump();
                }
                e.Handled = true;
            }
            base.OnPreviewKeyDown(e);
        }

        private void Jump()
        {
            // Eső csökkenti az ugrás erejét
            double jumpForce = isRaining ? -8 * 0.7 : -8;
            velocity = jumpForce;
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
            // Gravitáció
            velocity += gravity;
            double newTop = Canvas.GetTop(Bird) + velocity;

            // Felső és alsó határ (nem összecsapódik)
            if (newTop < 0)
            {
                Canvas.SetTop(Bird, 0);
                velocity = 0;
            }
            else if (newTop > CANVAS_HEIGHT - BIRD_HEIGHT)
            {
                Canvas.SetTop(Bird, CANVAS_HEIGHT - BIRD_HEIGHT);
                velocity = 0;
            }
            else
            {
                Canvas.SetTop(Bird, newTop);
            }

            // Madár képének frissítése
            if (velocity < -2)
                Bird.Source = new BitmapImage(new Uri("pack://application:,,,/kepek/flappyup.png", UriKind.Absolute));
            else if (velocity > 2)
                Bird.Source = new BitmapImage(new Uri("pack://application:,,,/kepek/flappy.png", UriKind.Absolute));
            else
                Bird.Source = new BitmapImage(new Uri("pack://application:,,,/kepek/flappymid.png", UriKind.Absolute));
        }

        private void SpawnPipe()
        {
            // Véletlenszerű hézag pozíciója
            int gapTop = rand.Next(100, 300);

            // Felső cső
            Rectangle topPipe = new Rectangle
            {
                Width = PIPE_WIDTH,
                Height = gapTop,
                Fill = System.Windows.Media.Brushes.Green
            };

            Canvas.SetLeft(topPipe, CANVAS_WIDTH);
            Canvas.SetTop(topPipe, 0);
            GameCanvas.Children.Add(topPipe);

            // Alsó cső
            int bottomPipeTop = gapTop + PIPE_GAP;
            Rectangle bottomPipe = new Rectangle
            {
                Width = PIPE_WIDTH,
                Height = CANVAS_HEIGHT - bottomPipeTop,
                Fill = System.Windows.Media.Brushes.Green
            };

            Canvas.SetLeft(bottomPipe, CANVAS_WIDTH);
            Canvas.SetTop(bottomPipe, bottomPipeTop);
            GameCanvas.Children.Add(bottomPipe);

            // Cső pár hozzáadása
            pipes.Add(new PipePair
            {
                TopPipe = topPipe,
                BottomPipe = bottomPipe,
                Scored = false
            });
        }

        private void UpdatePipes()
        {
            // Új cső spawn
            if ((DateTime.Now - lastPipeSpawn).TotalMilliseconds > PIPE_SPAWN_INTERVAL)
            {
                SpawnPipe();
                lastPipeSpawn = DateTime.Now;
            }

            // Csövek mozgatása
            for (int i = 0; i < pipes.Count; i++)
            {
                double pipeLeft = Canvas.GetLeft(pipes[i].TopPipe) + pipeSpeed;
                Canvas.SetLeft(pipes[i].TopPipe, pipeLeft);
                Canvas.SetLeft(pipes[i].BottomPipe, pipeLeft);

                // Túl régi csövek eltávolítása (nem törlöm, csak eltávolítom)
                if (pipeLeft < -200)
                {
                    GameCanvas.Children.Remove(pipes[i].TopPipe);
                    GameCanvas.Children.Remove(pipes[i].BottomPipe);
                    pipes.RemoveAt(i);
                    i--;
                }
            }
        }

        private void UpdateScore()
        {
            for (int i = 0; i < pipes.Count; i++)
            {
                if (!pipes[i].Scored && Canvas.GetLeft(pipes[i].TopPipe) < BIRD_WIDTH + 10 && Canvas.GetLeft(pipes[i].TopPipe) > BIRD_WIDTH - PIPE_WIDTH - 10)
                {
                    score++;
                    pipes[i].Scored = true;
                    ScoreText.Text = $"Pont: {score}";
                }
            }
        }

        private void CheckCollisions()
        {
            Rect birdRect = new Rect(Canvas.GetLeft(Bird), Canvas.GetTop(Bird), BIRD_WIDTH, BIRD_HEIGHT);

            // Felső és alsó határok
            if (Canvas.GetTop(Bird) <= 0 || Canvas.GetTop(Bird) + BIRD_HEIGHT >= CANVAS_HEIGHT)
            {
                GameOverMethod();
                return;
            }

            // Csövek ütközése
            foreach (PipePair pipe in pipes)
            {
                Rect topPipeRect = new Rect(Canvas.GetLeft(pipe.TopPipe), Canvas.GetTop(pipe.TopPipe), PIPE_WIDTH, Canvas.GetHeight(pipe.TopPipe));
                Rect bottomPipeRect = new Rect(Canvas.GetLeft(pipe.BottomPipe), Canvas.GetTop(pipe.BottomPipe), PIPE_WIDTH, Canvas.GetHeight(pipe.BottomPipe));

                if (birdRect.IntersectsWith(topPipeRect) || birdRect.IntersectsWith(bottomPipeRect))
                {
                    GameOverMethod();
                    return;
                }
            }
        }

        private void GameOverMethod()
        {
            gameOver = true;
            GameOverOverlay.Visibility = Visibility.Visible;
            GameOverText.Visibility = Visibility.Visible;
            FinalScoreText.Text = $"Végső pont: {score}";
            FinalScoreText.Visibility = Visibility.Visible;
            RestartText.Visibility = Visibility.Visible;
        }

        private void RestartGame()
        {
            // Játék állapotának alaphelyzetbe állítása
            gameOver = false;
            score = 0;
            velocity = 0;

            // UI elrejtése
            GameOverOverlay.Visibility = Visibility.Collapsed;
            GameOverText.Visibility = Visibility.Collapsed;
            FinalScoreText.Visibility = Visibility.Collapsed;
            RestartText.Visibility = Visibility.Collapsed;

            // Madár pozíciója
            Canvas.SetTop(Bird, 200);

            // Csövek eltávolítása
            foreach (PipePair pipe in pipes)
            {
                GameCanvas.Children.Remove(pipe.TopPipe);
                GameCanvas.Children.Remove(pipe.BottomPipe);
            }
            pipes.Clear();

            // Első cső spawn
            SpawnPipe();
            lastPipeSpawn = DateTime.Now;

            // Pontszám frissítése
            ScoreText.Text = "Pont: 0";

            // Focus vissza
            GameCanvas.Focus();
        }

        private void UpdateWeather()
        {
            // Eső toggle
            if ((DateTime.Now - lastRainToggle).TotalSeconds > 10)
            {
                isRaining = !isRaining;
                lastRainToggle = DateTime.Now;
            }

            // Köd toggle
            if ((DateTime.Now - lastFogToggle).TotalSeconds > 15)
            {
                isFoggy = !isFoggy;
                FogOverlay.Opacity = isFoggy ? 0.35 : 0;
                lastFogToggle = DateTime.Now;
            }
        }

        private void UpdateRain()
        {
            if (!isRaining) return;

            foreach (Rectangle raindrop in rainDrops)
            {
                double top = Canvas.GetTop(raindrop) + rainGravity;

                if (top > CANVAS_HEIGHT)
                {
                    Canvas.SetLeft(raindrop, rand.Next(0, CANVAS_WIDTH));
                    Canvas.SetTop(raindrop, rand.Next(-100, 0));
                }
                else
                {
                    Canvas.SetTop(raindrop, top);
                }
            }
        }

        // Segédosztály a cső párokhoz
        private class PipePair
        {
            public Rectangle TopPipe { get; set; }
            public Rectangle BottomPipe { get; set; }
            public bool Scored { get; set; }
        }
    }
}
