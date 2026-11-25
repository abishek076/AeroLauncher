using MinimalGameLauncher.Models;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace MinimalGameLauncher.Services
{
    public enum LaunchStatus
    {
        Launched,
        FileNotFound,
        CanceledByUser,
        Error
    }

    public record LaunchResult(LaunchStatus Status, string? ErrorMessage = null);

    public class GameLauncherService
    {
        private readonly Func<ProcessStartInfo, Process?> _processStarter;
        private readonly Func<string, bool> _fileExists;

        public event Action? GameExited;

        public GameLauncherService(
            Func<ProcessStartInfo, Process?>? processStarter = null,
            Func<string, bool>? fileExists = null)
        {
            _processStarter = processStarter ?? Process.Start;
            _fileExists = fileExists ?? File.Exists;
        }

        public LaunchResult Launch(GameItem game)
        {
            if (game == null || string.IsNullOrWhiteSpace(game.ExecutablePath) || !_fileExists(game.ExecutablePath))
            {
                return new LaunchResult(LaunchStatus.FileNotFound, "Game executable or image file could not be found.");
            }

            try
            {
                string extension = Path.GetExtension(game.ExecutablePath).ToLowerInvariant();

                if (extension == ".iso")
                {
                    var isoStartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-Command \"Mount-DiskImage -ImagePath '{game.ExecutablePath}'\"",
                        CreateNoWindow = true
                    };
                    _processStarter(isoStartInfo);
                    return new LaunchResult(LaunchStatus.Launched);
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = game.ExecutablePath,
                    WorkingDirectory = Path.GetDirectoryName(game.ExecutablePath) ?? string.Empty,
                    UseShellExecute = true
                };

                Process? gameProcess = _processStarter(startInfo);
                if (gameProcess != null)
                {
                    gameProcess.EnableRaisingEvents = true;
                    gameProcess.Exited += (_, _) => GameExited?.Invoke();
                }

                return new LaunchResult(LaunchStatus.Launched);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // User canceled the Windows UAC elevation prompt—do not show an error dialog
                return new LaunchResult(LaunchStatus.CanceledByUser);
            }
            catch (Exception ex)
            {
                return new LaunchResult(LaunchStatus.Error, ex.Message);
            }
        }
    }
}