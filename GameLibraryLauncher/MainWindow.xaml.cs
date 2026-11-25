using MinimalGameLauncher.Models;
using MinimalGameLauncher.Services;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;


namespace MinimalGameLauncher
{
    public partial class MainWindow : Window
    {
        private readonly LibraryService _libraryService;
        private readonly SteamGridService _steamGridService;
        private readonly GameLauncherService _launcherService;
        private readonly UpdateService _updateService = new("https://github.com/abishek076/AeroLauncher");
        private static readonly HttpClient _httpClient = new();

        private List<GameItem> _games = new();
        private string _steamGridApiKey = string.Empty;

        private Controller _controller = null!;
        private DispatcherTimer _gamepadTimer = null!;
        private int _controllerSelectedIndex = -1;
        private GamepadButtonFlags _previousButtons;
        private bool _isTopMenuFocused = false;
        private int _topMenuIndex = 0;

        private System.Windows.Forms.NotifyIcon _trayIcon = null!;
        private SoundPlayer? _navSound;
        private SoundPlayer? _launchSound;

        public MainWindow()
        {
            InitializeComponent();

            string appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MinimalGameLauncher");
            Directory.CreateDirectory(appDataFolder);

            string storageFilePath = Path.Combine(appDataFolder, "games.json");
            string settingsFilePath = Path.Combine(appDataFolder, "settings.json");
            string coversFolderPath = Path.Combine(appDataFolder, "Covers");

            _libraryService = new LibraryService(storageFilePath, settingsFilePath);
            _steamGridService = new SteamGridService(_httpClient, coversFolderPath);
            _launcherService = new GameLauncherService();
            _launcherService.GameExited += RestoreLauncher;

            LoadSettings();
            LoadLibrary();
            SetupController();
            SetupTrayAndAudio();
        }

        private void SetupTrayAndAudio()
        {
            _trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Application,
                Text = "Aero Launcher (Running)",
                Visible = false
            };
            _trayIcon.DoubleClick += (_, _) => RestoreLauncher();

            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string navPath = Path.Combine(baseDir, "sound", "nav.wav");
                string launchPath = Path.Combine(baseDir, "sound", "launch.wav");

                if (File.Exists(navPath)) _navSound = new SoundPlayer(navPath);
                if (File.Exists(launchPath)) _launchSound = new SoundPlayer(launchPath);

                _navSound?.Load();
                _launchSound?.Load();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Audio Engine Error: {ex.Message}");
            }
        }

        private void PlayNavSound() { try { _navSound?.Play(); } catch { } }
        private void PlayLaunchSound() { try { _launchSound?.Play(); } catch { } }

        private void RestoreLauncher()
        {
            Dispatcher.Invoke(() =>
            {
                Show();
                WindowState = WindowState.Maximized;
                Activate();
                _trayIcon.Visible = false;
            });
        }

        private void LoadSettings()
        {
            _steamGridApiKey = _libraryService.LoadApiKey();
        }

        private void BtnSettings_Click(object? sender, RoutedEventArgs? e)
        {
            PlayNavSound();
            TxtApiKey.Text = _steamGridApiKey;
            TxtAppVersion.Text = $"v{_updateService.CurrentVersion}";
            TxtUpdateStatus.Text = "Ready";
            SettingsOverlay.Visibility = Visibility.Visible;
        }

        private async void BtnCheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            PlayNavSound();
            BtnCheckUpdates.IsEnabled = false;
            TxtUpdateStatus.Text = "Checking GitHub releases...";

            UpdateCheckResult result = await _updateService.CheckForUpdatesAsync();
            TxtUpdateStatus.Text = result.Message;

            if (result.Status == UpdateCheckStatus.UpdateAvailable && result.UpdateInfo != null)
            {
                var confirm = System.Windows.MessageBox.Show(
                    $"{result.Message} is ready to download. Install and restart Aero Launcher now?",
                    "Aero Launcher Update",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (confirm == MessageBoxResult.Yes)
                {
                    TxtUpdateStatus.Text = "Downloading update (0%)...";
                    await _updateService.DownloadAndRestartAsync(
                        result.UpdateInfo,
                        progress => Dispatcher.Invoke(() => TxtUpdateStatus.Text = $"Downloading update ({progress}%)..."));
                }
            }

            BtnCheckUpdates.IsEnabled = true;
        }

        private void BtnCancelSettings_Click(object? sender, RoutedEventArgs? e) => SettingsOverlay.Visibility = Visibility.Collapsed;

        private void BtnSaveSettings_Click(object? sender, RoutedEventArgs? e)
        {
            _steamGridApiKey = TxtApiKey.Text.Trim();
            _libraryService.SaveApiKey(_steamGridApiKey);
            SettingsOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnAddGame_Click(object? sender, RoutedEventArgs? e)
        {
            PlayNavSound();
            AddGameOverlay.Visibility = Visibility.Visible;

            TxtGameName.Text = string.Empty;
            TxtGamePath.Text = string.Empty;
            TxtImagePath.Text = string.Empty;
            TxtHeroImagePath.Text = string.Empty;

            var defaultBrush = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A0AABF"));

            BtnBrowseImage.Content = "Browse Cover Image...";
            BtnBrowseImage.Foreground = defaultBrush;

            BtnBrowseHero.Content = "Browse Hero Banner...";
            BtnBrowseHero.Foreground = defaultBrush;
        }

        private void BtnCancelOverlay_Click(object? sender, RoutedEventArgs? e) => AddGameOverlay.Visibility = Visibility.Collapsed;

        private void BtnQuit_Click(object? sender, RoutedEventArgs? e)
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            System.Windows.Application.Current.Shutdown();
        }

        private void BtnBrowseImage_Click(object? sender, RoutedEventArgs? e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Images (*.jpg;*.png)|*.jpg;*.png;*.jpeg",
                Title = "Select Cover Poster"
            };
            if (dialog.ShowDialog() == true)
            {
                TxtImagePath.Text = dialog.FileName;
                BtnBrowseImage.Content = "Cover Selected ✔";
                BtnBrowseImage.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LimeGreen);
            }
        }

        private void BtnBrowseHero_Click(object? sender, RoutedEventArgs? e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Images (*.jpg;*.png)|*.jpg;*.png;*.jpeg",
                Title = "Select Wide Hero Banner"
            };
            if (dialog.ShowDialog() == true)
            {
                TxtHeroImagePath.Text = dialog.FileName;
                BtnBrowseHero.Content = "Banner Selected ✔";
                BtnBrowseHero.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LimeGreen);
            }
        }

        private void BtnBrowse_Click(object? sender, RoutedEventArgs? e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executables (*.exe)|*.exe|Shortcuts (*.lnk)|*.lnk|All Files (*.*)|*.*",
                Title = "Select Game Executable"
            };

            if (dialog.ShowDialog() == true)
            {
                TxtGamePath.Text = dialog.FileName;
                if (string.IsNullOrWhiteSpace(TxtGameName.Text))
                {
                    TxtGameName.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
                }
            }
        }

        private async void BtnSaveNewGame_Click(object? sender, RoutedEventArgs? e)
        {
            if (string.IsNullOrWhiteSpace(TxtGamePath.Text)) return;

            string finalTitle = string.IsNullOrWhiteSpace(TxtGameName.Text) ? "Unknown Game" : TxtGameName.Text;
            string finalImagePath = TxtImagePath.Text;
            bool needsDownload = string.IsNullOrWhiteSpace(finalImagePath);

            var newGame = new GameItem
            {
                Title = finalTitle,
                ExecutablePath = TxtGamePath.Text,
                ImagePath = finalImagePath,
                HeroImagePath = string.IsNullOrWhiteSpace(TxtHeroImagePath.Text) ? finalImagePath : TxtHeroImagePath.Text,
                IsDownloading = needsDownload
            };

            _libraryService.AddGame(_games, newGame);
            AddGameOverlay.Visibility = Visibility.Collapsed;

            _controllerSelectedIndex = _games.Count - 1;
            foreach (var g in _games) g.IsSelected = false;
            newGame.IsSelected = true;

            RefreshGrid();
            UpdateSelectedGameUI();

            if (needsDownload)
            {
                var (downloadedPoster, downloadedHero) = await _steamGridService.DownloadCoverArtAsync(finalTitle, _steamGridApiKey);
                await Task.Delay(200);

                newGame.ImagePath = downloadedPoster;
                newGame.HeroImagePath = downloadedHero;
                newGame.IsDownloading = false;

                _libraryService.SaveGames(_games);
                UpdateSelectedGameUI();
            }
        }

        private void BtnRemoveGame_Click(object? sender, RoutedEventArgs? e)
        {
            if (_libraryService.RemoveGameAt(_games, _controllerSelectedIndex))
            {
                PlayNavSound();
                _controllerSelectedIndex = _games.Count > 0 ? 0 : -1;

                if (_controllerSelectedIndex == 0)
                    _games[0].IsSelected = true;

                RefreshGrid();
                UpdateSelectedGameUI();
            }
        }

        private void UpdateSelectedGameUI()
        {
            if (_controllerSelectedIndex >= 0 && _controllerSelectedIndex < _games.Count)
            {
                var selectedGame = _games[_controllerSelectedIndex];
                TxtSelectedGameTitle.Text = selectedGame.Title;
                BtnRemoveGame.Visibility = Visibility.Visible;

                string? heroPath = selectedGame.HeroImagePath;
                if (!string.IsNullOrWhiteSpace(heroPath) && File.Exists(heroPath))
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(heroPath, UriKind.Absolute);
                        bitmap.EndInit();
                        HeroImage.Source = bitmap;
                    }
                    catch
                    {
                        HeroImage.Source = null;
                    }
                }
                else
                {
                    HeroImage.Source = null;
                }
            }
            else
            {
                TxtSelectedGameTitle.Text = "Select a Game";
                BtnRemoveGame.Visibility = Visibility.Collapsed;
                HeroImage.Source = null;
            }
        }

        private void SetupController()
        {
            _controller = new Controller(UserIndex.One);
            _gamepadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _gamepadTimer.Tick += GamepadTimer_Tick;
            _gamepadTimer.Start();
        }

        private void GamepadTimer_Tick(object? sender, EventArgs e)
        {
            if (!_controller.IsConnected) return;

            var gamepad = _controller.GetState().Gamepad;
            var currentButtons = gamepad.Buttons;

            if (currentButtons.HasFlag(GamepadButtonFlags.Start) && !_previousButtons.HasFlag(GamepadButtonFlags.Start))
            {
                if (SettingsOverlay.Visibility == Visibility.Visible) SettingsOverlay.Visibility = Visibility.Collapsed;
                else BtnSettings_Click(null, null);
            }

            if (currentButtons.HasFlag(GamepadButtonFlags.B) && !_previousButtons.HasFlag(GamepadButtonFlags.B))
            {
                AddGameOverlay.Visibility = Visibility.Collapsed;
                SettingsOverlay.Visibility = Visibility.Collapsed;
            }

            if (AddGameOverlay.Visibility == Visibility.Collapsed && SettingsOverlay.Visibility == Visibility.Collapsed)
            {
                if (currentButtons.HasFlag(GamepadButtonFlags.DPadUp) && !_previousButtons.HasFlag(GamepadButtonFlags.DPadUp) && !_isTopMenuFocused)
                {
                    PlayNavSound();
                    _isTopMenuFocused = true;
                    foreach (var game in _games) game.IsSelected = false;
                    UpdateTopMenuVisuals();
                }

                if (currentButtons.HasFlag(GamepadButtonFlags.DPadDown) && !_previousButtons.HasFlag(GamepadButtonFlags.DPadDown) && _isTopMenuFocused && _games.Count > 0)
                {
                    PlayNavSound();
                    _isTopMenuFocused = false;
                    UpdateTopMenuVisuals();
                    if (_controllerSelectedIndex < 0) _controllerSelectedIndex = 0;
                    _games[_controllerSelectedIndex].IsSelected = true;
                    UpdateSelectedGameUI();
                }

                if (currentButtons.HasFlag(GamepadButtonFlags.DPadRight) && !_previousButtons.HasFlag(GamepadButtonFlags.DPadRight))
                {
                    if (_isTopMenuFocused)
                    {
                        _topMenuIndex++;
                        if (_topMenuIndex == 1 && BtnRemoveGame.Visibility == Visibility.Collapsed) _topMenuIndex++;
                        if (_topMenuIndex > 2) _topMenuIndex = 0;
                        UpdateTopMenuVisuals();
                    }
                    else if (_games.Count > 0) UpdateSelection(1);
                }

                if (currentButtons.HasFlag(GamepadButtonFlags.DPadLeft) && !_previousButtons.HasFlag(GamepadButtonFlags.DPadLeft))
                {
                    if (_isTopMenuFocused)
                    {
                        _topMenuIndex--;
                        if (_topMenuIndex == 1 && BtnRemoveGame.Visibility == Visibility.Collapsed) _topMenuIndex--;
                        if (_topMenuIndex < 0) _topMenuIndex = 2;
                        UpdateTopMenuVisuals();
                    }
                    else if (_games.Count > 0) UpdateSelection(-1);
                }

                if (currentButtons.HasFlag(GamepadButtonFlags.A) && !_previousButtons.HasFlag(GamepadButtonFlags.A))
                {
                    if (_isTopMenuFocused)
                    {
                        if (_topMenuIndex == 0) BtnAddGame_Click(null, null);
                        else if (_topMenuIndex == 1) BtnRemoveGame_Click(null, null);
                        else if (_topMenuIndex == 2) BtnQuit_Click(null, null);
                    }
                    else
                    {
                        BtnPlayGame_Click(null, null);
                    }
                }

                if (currentButtons.HasFlag(GamepadButtonFlags.Y) && !_previousButtons.HasFlag(GamepadButtonFlags.Y))
                {
                    BtnAddGame_Click(null, null);
                }
            }

            _previousButtons = currentButtons;
        }

        private void UpdateTopMenuVisuals()
        {
            PlayNavSound();
            BtnAddGame.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2C3852"));
            BtnRemoveGame.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#5E2222"));
            BtnQuit.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2C3852"));

            if (!_isTopMenuFocused) return;

            var highlightColor = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00E5FF"));

            if (_topMenuIndex == 0) BtnAddGame.BorderBrush = highlightColor;
            else if (_topMenuIndex == 1) BtnRemoveGame.BorderBrush = highlightColor;
            else if (_topMenuIndex == 2) BtnQuit.BorderBrush = highlightColor;
        }

        private void UpdateSelection(int direction)
        {
            PlayNavSound();

            if (_controllerSelectedIndex >= 0 && _controllerSelectedIndex < _games.Count)
                _games[_controllerSelectedIndex].IsSelected = false;

            _controllerSelectedIndex += direction;

            if (_controllerSelectedIndex >= _games.Count) _controllerSelectedIndex = 0;
            if (_controllerSelectedIndex < 0) _controllerSelectedIndex = _games.Count - 1;

            _games[_controllerSelectedIndex].IsSelected = true;

            RefreshGrid();
            UpdateSelectedGameUI();
        }

        private void LoadLibrary()
        {
            _games = _libraryService.LoadGames();

            if (_games.Count > 0)
            {
                _controllerSelectedIndex = 0;
                _games[0].IsSelected = true;
            }

            RefreshGrid();
            UpdateSelectedGameUI();
        }

        private void RefreshGrid()
        {
            CarouselItems.ItemsSource = null;
            CarouselItems.ItemsSource = _games;

            if (_controllerSelectedIndex >= 0 && _controllerSelectedIndex < _games.Count)
            {
                double targetOffset = (_controllerSelectedIndex * 160.0) - (CarouselScroller.ActualWidth / 2.0) + 80.0;
                if (targetOffset < 0) targetOffset = 0;

                CarouselScroller.ScrollToHorizontalOffset(targetOffset);
            }
        }

        private void GameCard_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is GameItem clickedGame)
            {
                _isTopMenuFocused = false;
                UpdateTopMenuVisuals();

                foreach (var game in _games) game.IsSelected = false;

                clickedGame.IsSelected = true;
                _controllerSelectedIndex = _games.IndexOf(clickedGame);

                PlayNavSound();
                UpdateSelectedGameUI();
            }
        }

        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (AddGameOverlay.Visibility == Visibility.Visible || SettingsOverlay.Visibility == Visibility.Visible)
                return;

            if (e.Key == Key.Right && _games.Count > 0)
            {
                UpdateSelection(1);
            }
            else if (e.Key == Key.Left && _games.Count > 0)
            {
                UpdateSelection(-1);
            }
            else if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                BtnPlayGame_Click(null, null);
            }
        }

        private void BtnPlayGame_Click(object? sender, RoutedEventArgs? e)
        {
            if (_games.Count < 0 || _controllerSelectedIndex < 0 || _controllerSelectedIndex >= _games.Count)
                return;

            var selectedGame = _games[_controllerSelectedIndex];
            var result = _launcherService.Launch(selectedGame);

            if (result.Status == LaunchStatus.Launched)
            {
                PlayLaunchSound();
                Hide();
                _trayIcon.Visible = true;
            }
            else if (result.Status == LaunchStatus.Error)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to launch '{selectedGame.Title}': {result.ErrorMessage}",
                    "Execution Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}