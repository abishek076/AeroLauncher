using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace MinimalGameLauncher.Services
{
    public enum UpdateCheckStatus
    {
        NotInstalledMode,
        UpToDate,
        UpdateAvailable,
        Error
    }

    public record UpdateCheckResult(
        UpdateCheckStatus Status,
        string Message,
        UpdateInfo? UpdateInfo = null);

    public class UpdateService
    {
        private readonly string _githubRepoUrl;
        private UpdateManager? _updateManager;

        public UpdateService(string githubRepoUrl)
        {
            if (string.IsNullOrWhiteSpace(githubRepoUrl))
                throw new ArgumentException("GitHub repository URL is required.", nameof(githubRepoUrl));

            _githubRepoUrl = githubRepoUrl;
        }

        private UpdateManager? TryGetManager()
        {
            if (_updateManager != null)
                return _updateManager;

            try
            {
                var source = new GithubSource(_githubRepoUrl, accessToken: null, prerelease: false);
                _updateManager = new UpdateManager(source);
                return _updateManager;
            }
            catch
            {
                return null;
            }
        }

        public string CurrentVersion
        {
            get
            {
                var manager = TryGetManager();
                return manager != null && manager.IsInstalled
                    ? manager.CurrentVersion?.ToString() ?? "1.0.0"
                    : "Dev Build (Uninstalled)";
            }
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync()
        {
            var manager = TryGetManager();
            if (manager == null || !manager.IsInstalled)
            {
                return new UpdateCheckResult(
                    UpdateCheckStatus.NotInstalledMode,
                    "Auto-update is disabled while running inside Visual Studio.");
            }

            try
            {
                UpdateInfo? updateInfo = await manager.CheckForUpdatesAsync();
                if (updateInfo == null)
                {
                    return new UpdateCheckResult(
                        UpdateCheckStatus.UpToDate,
                        $"Aero Launcher v{CurrentVersion} is up to date.");
                }

                string targetVersion = updateInfo.TargetFullRelease.Version.ToString();
                return new UpdateCheckResult(
                    UpdateCheckStatus.UpdateAvailable,
                    $"Update available: v{targetVersion}",
                    updateInfo);
            }
            catch (Exception ex)
            {
                return new UpdateCheckResult(
                    UpdateCheckStatus.Error,
                    $"Update check failed: {ex.Message}");
            }
        }

        public async Task DownloadAndRestartAsync(UpdateInfo updateInfo, Action<int>? onProgress = null)
        {
            if (updateInfo == null) throw new ArgumentNullException(nameof(updateInfo));

            var manager = TryGetManager()
                ?? throw new InvalidOperationException("Velopack UpdateManager is not initialized.");

            await manager.DownloadUpdatesAsync(updateInfo, onProgress);
            manager.ApplyUpdatesAndRestart(updateInfo);
        }
    }
}