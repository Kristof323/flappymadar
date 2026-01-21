using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FlappyBird
{
    public partial class MainWindow : Window
    {
       
        private double velocity = 0;
        private const double GRAVITY_PX_PER_SEC2 = 1800; 
        private const double JUMP_VELOCITY_PX_PER_SEC = -520; 
        private int score = 0;
        private bool gameOver = false;
        private Random rand = new Random();
        private TimeSpan _lastRenderTime = TimeSpan.Zero;


        private List<PipePair> pipes = new List<PipePair>();
        private const int PIPE_WIDTH = 80;
        private const int PIPE_GAP = 150;
        private const double PIPE_SPEED_PX_PER_SEC = 240;
        private const double PIPE_SPACING_PX = 360;

        private List<Rectangle> rainDrops = new List<Rectangle>();
        private bool isRaining = false;
        private const double RAIN_GRAVITY = 8;
        private const double RAIN_JUMP_MODIFIER = 0.6;

      
        private bool isFoggy = false;
        
        private int columnsPassed = 0;
        private WeatherPhase currentWeatherPhase = WeatherPhase.ClearWeather;

        private enum WeatherPhase
        {
            ClearWeather,
            FogOnly,
            RainOnly,
            FogAgain,
            RainAgain
        }

        
        private const int CANVAS_WIDTH = 900;
        private const int CANVAS_HEIGHT = 600;
        private const int BIRD_WIDTH = 50;
        private const int BIRD_HEIGHT = 40;
        private const int GROUND_LEVEL = 550;

       
        private Dictionary<string, BitmapImage> imageCache = new Dictionary<string, BitmapImage>();
        private BitmapImage _birdUp, _birdMid, _birdDown, _gameOverImage;

        public MainWindow()
        {
            InitializeComponent();
            InitializeGame();
        }

        private void InitializeGame()
        {
            GameCanvas.Focus();

            
            PreloadImages();
            
            InitializeRainDrops();
            
            SpawnPipe();

            UpdateWeatherUI();

            CompositionTarget.Rendering += OnRenderFrame;
        }


        private void PreloadImages()
        {
            _birdUp = LoadFrozenImage("kepek/flappyup.png");
            _birdMid = LoadFrozenImage("kepek/flappymid.png");
            _birdDown = LoadFrozenImage("kepek/flappydown.png");
            _gameOverImage = LoadFrozenImage("kepek/gameover.png");

            BitmapImage bgImage = LoadFrozenImage("kepek/background.jpg");
            if (bgImage != null)
                BackgroundImage.Source = bgImage;

            if (_birdMid != null)
                BirdImage.Source = _birdMid;
        }

        private BitmapImage LoadFrozenImage(string imagePath)
        {
            if (imageCache.ContainsKey(imagePath))
                return imageCache[imagePath];

            try
            {
                string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, imagePath);

                if (File.Exists(fullPath))
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    imageCache[imagePath] = bitmap;
                    return bitmap;
                }
                else
                {
                    MessageBox.Show($"Fájl nem található: {fullPath}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hiba a betöltéskor: {imagePath}\n{ex.Message}");
            }

            return null;
        }

        private void InitializeRainDrops()
        {
            for (int i = 0; i < 20; i++)
            {
                Rectangle drop = new Rectangle
                {
                    Width = 3,
                    Height = 15,
                    Fill = System.Windows.Media.Brushes.LightBlue,
                    Opacity = 0.7
                };

                Canvas.SetLeft(drop, rand.Next(0, CANVAS_WIDTH));
                Canvas.SetTop(drop, rand.Next(-100, 0));
                GameCanvas.Children.Add(drop);
                rainDrops.Add(drop);
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Space || e.Key == Key.Up)
            {
                if (gameOver)
                {
                    RestartGame();
                }
                else
                {
                    JumpBird();
                }
                e.Handled = true;
            }
        }

     
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (!gameOver)
            {
                JumpBird();
                e.Handled = true;
            }
            else
            {
                RestartGame();
            }
        }

        private void JumpBird()
        {
            double v = isRaining ? (JUMP_VELOCITY_PX_PER_SEC * RAIN_JUMP_MODIFIER) : JUMP_VELOCITY_PX_PER_SEC;
            velocity = v;
        }

        private void OnRenderFrame(object? sender, EventArgs e)
        {
            if (gameOver) return;

            var re = (RenderingEventArgs)e;

           
            if (_lastRenderTime == TimeSpan.Zero)
            {
                _lastRenderTime = re.RenderingTime;
                return;
            }

           
            double dt = (re.RenderingTime - _lastRenderTime).TotalSeconds;
            _lastRenderTime = re.RenderingTime;

            
            if (dt > 0.05) dt = 0.05;

       
            UpdateBird(dt);
            UpdatePipes(dt);
            UpdateScore();
            CheckCollisions();
            UpdateRain(dt);
        }

    
        private void UpdateBird(double dt)
        {
            velocity += GRAVITY_PX_PER_SEC2 * dt;
            double newTop = Canvas.GetTop(BirdImage) + velocity * dt;

            if (newTop < 0)
            {
                Canvas.SetTop(BirdImage, 0);
                velocity = 0;
            }
    
            else if (newTop + BIRD_HEIGHT > GROUND_LEVEL)
            {
                Canvas.SetTop(BirdImage, GROUND_LEVEL - BIRD_HEIGHT);
                GameOver();
                return;
            }
            else
            {
                Canvas.SetTop(BirdImage, newTop);
            }

            UpdateBirdImage();
        }

        private void UpdateBirdImage()
        {
            BitmapImage newImage = null;

            if (velocity < -200)
                newImage = _birdUp;
            else if (velocity > 200)
                newImage = _birdDown;
            else
                newImage = _birdMid;

            if (newImage != null && BirdImage.Source != newImage)
            {
                BirdImage.Source = newImage;
            }
        }

        private void SpawnPipe()
        {
            int minGapStart = 80;
            int maxGapStart = CANVAS_HEIGHT - PIPE_GAP - 80;
            int gapStart = rand.Next(minGapStart, maxGapStart);

            Rectangle topPipe = new Rectangle
            {
                Width = PIPE_WIDTH,
                Height = gapStart,
                Fill = System.Windows.Media.Brushes.Green,
                Stroke = System.Windows.Media.Brushes.DarkGreen,
                StrokeThickness = 2
            };
            Canvas.SetLeft(topPipe, CANVAS_WIDTH);
            Canvas.SetTop(topPipe, 0);
            GameCanvas.Children.Add(topPipe);

            int bottomPipeStart = gapStart + PIPE_GAP;
            Rectangle bottomPipe = new Rectangle
            {
                Width = PIPE_WIDTH,
                Height = CANVAS_HEIGHT - bottomPipeStart,
                Fill = System.Windows.Media.Brushes.Green,
                Stroke = System.Windows.Media.Brushes.DarkGreen,
                StrokeThickness = 2
            };
            Canvas.SetLeft(bottomPipe, CANVAS_WIDTH);
            Canvas.SetTop(bottomPipe, bottomPipeStart);
            GameCanvas.Children.Add(bottomPipe);

            pipes.Add(new PipePair
            {
                TopPipe = topPipe,
                BottomPipe = bottomPipe,
                HasScored = false
            });
        }

        private void UpdatePipes(double dt)
        {
            for (int i = 0; i < pipes.Count; i++)
            {
                double newLeft = Canvas.GetLeft(pipes[i].TopPipe) - PIPE_SPEED_PX_PER_SEC * dt;
                Canvas.SetLeft(pipes[i].TopPipe, newLeft);
                Canvas.SetLeft(pipes[i].BottomPipe, newLeft);
            }

            if (pipes.Count == 0)
            {
                SpawnPipe();
                return;
            }

            double lastX = Canvas.GetLeft(pipes[pipes.Count - 1].TopPipe);
            if (lastX < CANVAS_WIDTH - PIPE_SPACING_PX)
            {
                SpawnPipe();
            }

            for (int i = pipes.Count - 1; i >= 0; i--)
            {
                if (Canvas.GetLeft(pipes[i].TopPipe) < -PIPE_WIDTH - 50)
                {
                    GameCanvas.Children.Remove(pipes[i].TopPipe);
                    GameCanvas.Children.Remove(pipes[i].BottomPipe);
                    pipes.RemoveAt(i);
                }
            }
        }

        private void UpdateScore()
        {
            foreach (PipePair pipe in pipes)
            {
                double birdCenterX = Canvas.GetLeft(BirdImage) + BIRD_WIDTH / 2;
                double pipeLeftEdge = Canvas.GetLeft(pipe.TopPipe);
                double pipeRightEdge = pipeLeftEdge + PIPE_WIDTH;

                if (!pipe.HasScored && birdCenterX > pipeLeftEdge && birdCenterX < pipeRightEdge)
                {
                    score++;
                    columnsPassed++;
                    pipe.HasScored = true;
                    ScoreDisplay.Text = $"Pont: {score}";

                    UpdateWeatherPhase();
                    UpdateWeatherUI();
                }
            }
        }

        private void UpdateWeatherPhase()
        {

            if (columnsPassed < 5)
                currentWeatherPhase = WeatherPhase.ClearWeather;
            else if (columnsPassed < 10)
                currentWeatherPhase = WeatherPhase.FogOnly;
            else if (columnsPassed < 15)
                currentWeatherPhase = WeatherPhase.RainOnly;
            else if (columnsPassed < 20)
                currentWeatherPhase = WeatherPhase.FogOnly;
            else if (columnsPassed < 25)
                currentWeatherPhase = WeatherPhase.RainOnly;
            else if (columnsPassed < 30)
                currentWeatherPhase = WeatherPhase.FogOnly;
            else 
                currentWeatherPhase = WeatherPhase.RainOnly;
        }

        private void UpdateWeatherUI()
        {
            isRaining = (currentWeatherPhase == WeatherPhase.RainOnly || currentWeatherPhase == WeatherPhase.RainAgain);
            isFoggy = (currentWeatherPhase == WeatherPhase.FogOnly || currentWeatherPhase == WeatherPhase.FogAgain);

            RainStatusDisplay.Text = isRaining ? "Eső: BE" : "Eső: KI";
            FogStatusDisplay.Text = isFoggy ? "Köd: BE" : "Köd: KI";
            FogLayer.Opacity = isFoggy ? 0.35 : 0;
        }

        private void CheckCollisions()
        {
            Rect birdRect = new Rect(
                Canvas.GetLeft(BirdImage),
                Canvas.GetTop(BirdImage),
                BIRD_WIDTH,
                BIRD_HEIGHT
            );

            foreach (PipePair pipe in pipes)
            {
                Rect topPipeRect = new Rect(
                    Canvas.GetLeft(pipe.TopPipe),
                    Canvas.GetTop(pipe.TopPipe),
                    PIPE_WIDTH,
                    pipe.TopPipe.Height
                );

                Rect bottomPipeRect = new Rect(
                    Canvas.GetLeft(pipe.BottomPipe),
                    Canvas.GetTop(pipe.BottomPipe),
                    PIPE_WIDTH,
                    pipe.BottomPipe.Height
                );

                if (birdRect.IntersectsWith(topPipeRect) || birdRect.IntersectsWith(bottomPipeRect))
                {
                    GameOver();
                    return;
                }
            }
        }

        private void GameOver()
        {
            gameOver = true;
            CompositionTarget.Rendering -= OnRenderFrame;

            GameOverOverlay.Visibility = Visibility.Visible;
            GameOverText.Visibility = Visibility.Visible;
            FinalScoreText.Text = $"Végső pont: {score}";
            FinalScoreText.Visibility = Visibility.Visible;
            RestartText.Visibility = Visibility.Visible;

            if (_gameOverImage != null)
                BirdImage.Source = _gameOverImage;
        }

        private void RestartGame()
        {
            gameOver = false;
            score = 0;
            columnsPassed = 0;
            velocity = 0;
            currentWeatherPhase = WeatherPhase.ClearWeather;
            _lastRenderTime = TimeSpan.Zero;

            GameOverOverlay.Visibility = Visibility.Collapsed;
            GameOverText.Visibility = Visibility.Collapsed;
            FinalScoreText.Visibility = Visibility.Collapsed;
            RestartText.Visibility = Visibility.Collapsed;
            ScoreDisplay.Text = "Pont: 0";

            Canvas.SetTop(BirdImage, 250);
            if (_birdMid != null)
                BirdImage.Source = _birdMid;

            foreach (PipePair pipe in pipes)
            {
                GameCanvas.Children.Remove(pipe.TopPipe);
                GameCanvas.Children.Remove(pipe.BottomPipe);
            }
            pipes.Clear();

            SpawnPipe();

            UpdateWeatherUI();

            CompositionTarget.Rendering += OnRenderFrame;
            GameCanvas.Focus();
        }

        private void UpdateRain(double dt)
        {
            if (!isRaining) return;

            double rainSpeed = RAIN_GRAVITY * 60 * dt;

            foreach (Rectangle drop in rainDrops)
            {
                double top = Canvas.GetTop(drop) + rainSpeed;

                if (top > CANVAS_HEIGHT)
                {
                    Canvas.SetLeft(drop, rand.Next(0, CANVAS_WIDTH));
                    Canvas.SetTop(drop, rand.Next(-100, 0));
                }
                else
                {
                    Canvas.SetTop(drop, top);
                }
            }
        }
    }

    public class PipePair
    {
        public Rectangle TopPipe { get; set; }
        public Rectangle BottomPipe { get; set; }
        public bool HasScored { get; set; }
    }
}
