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
        private bool _gameRunning = false;
        private bool _gameOver = false;

        private TimeSpan _lastRenderTime = TimeSpan.Zero;

        private double _velocity = 0;
        private double _gravityPxPerSec2 = 1800;
        private double _jumpVelocityPxPerSec = -520;

        private const int BIRD_WIDTH = 50;
        private const int BIRD_HEIGHT = 40;
        private const int GROUND_LEVEL = 550;

        private int _score = 0;
        private int _columnsPassed = 0;

        private readonly Random _rand = new Random();
        private readonly List<PipePair> _pipes = new List<PipePair>();

        private const int CANVAS_WIDTH = 900;
        private const int CANVAS_HEIGHT = 600;
        private const int PIPE_WIDTH = 80;
        private const int PIPE_GAP = 150;

        private double _pipeSpeedPxPerSec = 240;
        private double _pipeSpacingPx = 360;

        private readonly List<Rectangle> _rainDrops = new List<Rectangle>();
        private bool _isRaining = false;
        private const double RAIN_DROP_SPEED_PX_PER_SEC = 450;
        private const double RAIN_JUMP_MODIFIER = 0.6;

        private bool _isFoggy = false;

        private GravityMode _gravityMode = GravityMode.Normal;
        private MapMode _mapMode = MapMode.Flappy;
        private WeatherMode _fogMode = WeatherMode.Few;
        private WeatherMode _rainMode = WeatherMode.Few;

        private enum GravityMode { Strong, Normal, Weak }
        private enum MapMode { Flappy, Petrik }
        private enum WeatherMode { Off, On, Few }

        private int _petrikIndex = 0;
        private readonly string[] _petrikPaths =
        {
            @"kepek\Petrik.jpg",
            @"kepek\Petrik2.jpg",
            @"kepek\Petrik3.jpg",
            @"kepek\Petrik4.jpg",
            @"kepek\Petrik5.jpg"
        };

        private readonly Dictionary<string, BitmapImage> _imageCache = new Dictionary<string, BitmapImage>();
        private BitmapImage _birdUp, _birdMid, _birdDown;
        private BitmapImage _bgFlappy;

        private enum BirdPose { Up, Mid, Down }
        private BirdPose _pose = BirdPose.Mid;

        public MainWindow()
        {
            InitializeComponent();

            SetMenuVisible(true);
            SetGameUiEnabled(false);

            PreloadBaseImages();
            InitializeRainDrops();
            SetRainVisible(false);
            SetFogVisible(false);
        }
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            ReadOptionsFromMenu();
            ApplyOptions();
            StartNewGame();
        }

        private void ReadOptionsFromMenu()
        {
            _gravityMode = GravityCombo.SelectedIndex switch
            {
                0 => GravityMode.Strong,
                2 => GravityMode.Weak,
                _ => GravityMode.Normal
            };

            _mapMode = MapCombo.SelectedIndex == 1 ? MapMode.Petrik : MapMode.Flappy;

            _fogMode = FogCombo.SelectedIndex switch
            {
                0 => WeatherMode.Off,
                1 => WeatherMode.On,
                _ => WeatherMode.Few
            };

            _rainMode = RainCombo.SelectedIndex switch
            {
                0 => WeatherMode.Off,
                1 => WeatherMode.On,
                _ => WeatherMode.Few
            };
        }

        private void ApplyOptions()
        {

            (_gravityPxPerSec2, _jumpVelocityPxPerSec) = _gravityMode switch
            {
                GravityMode.Strong => (2200, -560),
                GravityMode.Weak => (1400, -480),
                _ => (1800, -520)
            };

            if (_mapMode == MapMode.Flappy)
            {
                BackgroundImage.Source = _bgFlappy;
            }
            else
            {
                _petrikIndex = 0;
                BackgroundImage.Source = LoadFrozenImage(_petrikPaths[_petrikIndex]);
            }

            SetFogVisible(false);
            SetRainVisible(false);
        }

        private void SetMenuVisible(bool visible)
        {
            StartMenu.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetGameUiEnabled(bool enabled)
        {
            GameCanvas.Focusable = enabled;
            if (enabled) GameCanvas.Focus();
        }

        private void StartNewGame()
        {
            _gameOver = false;
            _gameRunning = true;

            _score = 0;
            _columnsPassed = 0;
            _velocity = 0;
            _pose = BirdPose.Mid;

            ScoreDisplay.Text = "Pont: 0";

            GameOverPanel.Visibility = Visibility.Collapsed;

            BirdImage.Source = _birdMid;
            Canvas.SetLeft(BirdImage, 100);
            Canvas.SetTop(BirdImage, 250);

            foreach (var p in _pipes)
            {
                GameCanvas.Children.Remove(p.TopPipe);
                GameCanvas.Children.Remove(p.BottomPipe);
            }
            _pipes.Clear();
            SpawnPipe();

            ApplyWeatherByMode();

            _lastRenderTime = TimeSpan.Zero;
            CompositionTarget.Rendering -= OnRenderFrame;
            CompositionTarget.Rendering += OnRenderFrame;

            SetMenuVisible(false);
            SetGameUiEnabled(true);
        }

        private void EndGame()
        {
            _gameOver = true;
            _gameRunning = false;
            CompositionTarget.Rendering -= OnRenderFrame;

            FinalScoreText2.Text = $"Végső pont: {_score}";
            GameOverPanel.Visibility = Visibility.Visible;
        }

        private void BackToMenuButton_Click(object sender, RoutedEventArgs e)
        {
            CompositionTarget.Rendering -= OnRenderFrame;
            _gameRunning = false;
            _gameOver = false;

            GameOverPanel.Visibility = Visibility.Collapsed;

            SetMenuVisible(true);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space || e.Key == Key.Up)
            {
                if (_gameOver)
                {
                    BackToMenuButton_Click(this, new RoutedEventArgs());
                }
                else
                {
                    JumpBird();
                }

                e.Handled = true;
            }
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_gameOver)
            {
                BackToMenuButton_Click(this, new RoutedEventArgs());
            }
            else
            {
                JumpBird();
            }

            e.Handled = true;
        }

        private void JumpBird()
        {
            double v = _jumpVelocityPxPerSec;

            if (_isRaining) v *= RAIN_JUMP_MODIFIER;

            _velocity = v;
        }

        private void OnRenderFrame(object? sender, EventArgs e)
        {
            if (_gameOver || !_gameRunning) return;

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
            UpdateScoreAndWeather();
            CheckCollisions();
            UpdateRain(dt);
        }

        private void UpdateBird(double dt)
        {
            _velocity += _gravityPxPerSec2 * dt;
            double newTop = Canvas.GetTop(BirdImage) + _velocity * dt;

            if (newTop < 0)
            {
                Canvas.SetTop(BirdImage, 0);
                _velocity = 0;
            }
            else if (newTop + BIRD_HEIGHT > GROUND_LEVEL)
            {
                Canvas.SetTop(BirdImage, GROUND_LEVEL - BIRD_HEIGHT);
                EndGame();
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
            BirdPose next =
                _velocity < -200 ? BirdPose.Up :
                _velocity > 200 ? BirdPose.Down :
                BirdPose.Mid;

            if (next == _pose) return;
            _pose = next;

            BirdImage.Source = _pose switch
            {
                BirdPose.Up => _birdUp,
                BirdPose.Down => _birdDown,
                _ => _birdMid
            };
        }

        private void SpawnPipe()
        {
            int minGapStart = 80;
            int maxGapStart = CANVAS_HEIGHT - PIPE_GAP - 80;
            int gapStart = _rand.Next(minGapStart, maxGapStart);

            var topPipe = new Rectangle
            {
                Width = PIPE_WIDTH,
                Height = gapStart,
                Fill = Brushes.Green,
                Stroke = Brushes.DarkGreen,
                StrokeThickness = 2
            };
            Canvas.SetLeft(topPipe, CANVAS_WIDTH);
            Canvas.SetTop(topPipe, 0);
            GameCanvas.Children.Add(topPipe);

            int bottomPipeStart = gapStart + PIPE_GAP;
            var bottomPipe = new Rectangle
            {
                Width = PIPE_WIDTH,
                Height = CANVAS_HEIGHT - bottomPipeStart,
                Fill = Brushes.Green,
                Stroke = Brushes.DarkGreen,
                StrokeThickness = 2
            };
            Canvas.SetLeft(bottomPipe, CANVAS_WIDTH);
            Canvas.SetTop(bottomPipe, bottomPipeStart);
            GameCanvas.Children.Add(bottomPipe);

            _pipes.Add(new PipePair { TopPipe = topPipe, BottomPipe = bottomPipe, HasScored = false });
        }

        private void UpdatePipes(double dt)
        {
            for (int i = 0; i < _pipes.Count; i++)
            {
                double x = Canvas.GetLeft(_pipes[i].TopPipe) - _pipeSpeedPxPerSec * dt;
                Canvas.SetLeft(_pipes[i].TopPipe, x);
                Canvas.SetLeft(_pipes[i].BottomPipe, x);
            }
            
            if (_pipes.Count == 0)
            {
                SpawnPipe();
                return;
            }

            double lastX = Canvas.GetLeft(_pipes[^1].TopPipe);
            if (lastX < CANVAS_WIDTH - _pipeSpacingPx)
                SpawnPipe();

            for (int i = _pipes.Count - 1; i >= 0; i--)
            {
                if (Canvas.GetLeft(_pipes[i].TopPipe) < -PIPE_WIDTH - 80)
                {
                    GameCanvas.Children.Remove(_pipes[i].TopPipe);
                    GameCanvas.Children.Remove(_pipes[i].BottomPipe);
                    _pipes.RemoveAt(i);
                }
            }
        }

        private void UpdateScoreAndWeather()
        {
            double birdCenterX = Canvas.GetLeft(BirdImage) + BIRD_WIDTH / 2;

            foreach (var pipe in _pipes)
            {
                if (pipe.HasScored) continue;

                double pipeLeft = Canvas.GetLeft(pipe.TopPipe);
                double pipeRight = pipeLeft + PIPE_WIDTH;

                if (birdCenterX > pipeLeft && birdCenterX < pipeRight)
                {
                    pipe.HasScored = true;
                    _score++;
                    _columnsPassed++;

                    ScoreDisplay.Text = $"Pont: {_score}";

                    if (_mapMode == MapMode.Petrik && _columnsPassed % 3 == 0)
                    {
                        _petrikIndex = (_petrikIndex + 1) % _petrikPaths.Length;
                        BackgroundImage.Source = LoadFrozenImage(_petrikPaths[_petrikIndex]);
                    }

                    ApplyWeatherByMode();
                }
            }
        }

        private void ApplyWeatherByMode()
        {
            bool fogActive = false;
            if (_fogMode == WeatherMode.On)
                fogActive = true;
            else if (_fogMode == WeatherMode.Few)
                fogActive = IsFogActiveFew(_columnsPassed);

            bool rainActive = false;
            if (_rainMode == WeatherMode.On)
                rainActive = true;
            else if (_rainMode == WeatherMode.Few)
                rainActive = IsRainActiveFew(_columnsPassed);

            SetFogVisible(fogActive);
            SetRainVisible(rainActive);

            RainStatusDisplay.Text = rainActive ? "Eső: BE" : "Eső: KI";
            FogStatusDisplay.Text = fogActive ? "Köd: BE" : "Köd: KI";
        }

        private static bool IsFogActiveFew(int passed)
        {
            if (passed < 5) return false;
            int block = passed / 5;
            return block % 2 == 1;
        }

        private static bool IsRainActiveFew(int passed)
        {
            if (passed < 10) return false;
            int block = passed / 5;
            return block % 2 == 0;
        }

        private void SetFogVisible(bool active)
        {
            _isFoggy = active;
            FogLayer.Opacity = active ? 0.35 : 0;
        }

        private void SetRainVisible(bool active)
        {
            _isRaining = active;

            foreach (var d in _rainDrops)
                d.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        }

        private void InitializeRainDrops()
        {
            for (int i = 0; i < 20; i++)
            {
                var drop = new Rectangle
                {
                    Width = 3,
                    Height = 15,
                    Fill = Brushes.LightBlue,
                    Opacity = 0.7,
                    Visibility = Visibility.Collapsed
                };

                Canvas.SetLeft(drop, _rand.Next(0, CANVAS_WIDTH));
                Canvas.SetTop(drop, _rand.Next(-CANVAS_HEIGHT, 0));

                GameCanvas.Children.Add(drop);
                _rainDrops.Add(drop);
            }
        }

        private void UpdateRain(double dt)
        {
            if (!_isRaining) return;

            double dy = RAIN_DROP_SPEED_PX_PER_SEC * dt;

            foreach (var drop in _rainDrops)
            {
                double top = Canvas.GetTop(drop) + dy;

                if (top > CANVAS_HEIGHT)
                {
                    Canvas.SetLeft(drop, _rand.Next(0, CANVAS_WIDTH));
                    Canvas.SetTop(drop, _rand.Next(-150, 0));
                }
                else
                {
                    Canvas.SetTop(drop, top);
                }
            }
        }

        private void CheckCollisions()
        {
            var birdRect = new Rect(
                Canvas.GetLeft(BirdImage),
                Canvas.GetTop(BirdImage),
                BIRD_WIDTH,
                BIRD_HEIGHT
            );

            foreach (var pipe in _pipes)
            {
                var topRect = new Rect(
                    Canvas.GetLeft(pipe.TopPipe),
                    Canvas.GetTop(pipe.TopPipe),
                    PIPE_WIDTH,
                    pipe.TopPipe.Height
                );

                var bottomRect = new Rect(
                    Canvas.GetLeft(pipe.BottomPipe),
                    Canvas.GetTop(pipe.BottomPipe),
                    PIPE_WIDTH,
                    pipe.BottomPipe.Height
                );

                if (birdRect.IntersectsWith(topRect) || birdRect.IntersectsWith(bottomRect))
                {
                    EndGame();
                    return;
                }
            }
        }

        private void PreloadBaseImages()
        {
            _bgFlappy = LoadFrozenImage(@"kepek\background.jpg");
            _birdUp = LoadFrozenImage(@"kepek\flappyup.png");
            _birdMid = LoadFrozenImage(@"kepek\flappymid.png");
            _birdDown = LoadFrozenImage(@"kepek\flappydown.png");

            if (_bgFlappy != null) BackgroundImage.Source = _bgFlappy;
            if (_birdMid != null) BirdImage.Source = _birdMid;
        }

        private BitmapImage LoadFrozenImage(string relPath)
        {
            if (_imageCache.TryGetValue(relPath, out var cached))
                return cached;

            string full = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relPath);

            if (!System.IO.File.Exists(full))
                return null;

            var bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource = new Uri(full, UriKind.Absolute);
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.EndInit();
            bi.Freeze();

            _imageCache[relPath] = bi;
            return bi;
        }
    }

    public class PipePair
    {
        public Rectangle TopPipe { get; set; }
        public Rectangle BottomPipe { get; set; }
        public bool HasScored { get; set; }
    }
}
