using MinimalGameLauncher.Models;
using MinimalGameLauncher.Services;
using System;
using System.ComponentModel;
using System.Diagnostics;
using Xunit;

namespace AeroLauncher.Tests
{
    public class GameLauncherServiceTests
    {
        [Fact]
        public void Launch_WhenExecutableDoesNotExist_ReturnsFileNotFound()
        {
            var service = new GameLauncherService(
                processStarter: _ => throw new InvalidOperationException("Should not start process"),
                fileExists: _ => false);

            var game = new GameItem
            {
                Title = "Missing Game",
                ExecutablePath = @"D:\Games\Missing\game.exe"
            };

            LaunchResult result = service.Launch(game);

            Assert.Equal(LaunchStatus.FileNotFound, result.Status);
        }

        [Fact]
        public void Launch_WhenValidExecutable_StartsProcessWithWorkingDirectory()
        {
            ProcessStartInfo? capturedStartInfo = null;

            var service = new GameLauncherService(
                processStarter: info =>
                {
                    capturedStartInfo = info;
                    return null;
                },
                fileExists: _ => true);

            var game = new GameItem
            {
                Title = "Need for Speed Most Wanted",
                ExecutablePath = @"D:\Games\NFSMW\speed.exe"
            };

            LaunchResult result = service.Launch(game);

            Assert.Equal(LaunchStatus.Launched, result.Status);
            Assert.NotNull(capturedStartInfo);
            Assert.Equal(@"D:\Games\NFSMW\speed.exe", capturedStartInfo!.FileName);
            Assert.Equal(@"D:\Games\NFSMW", capturedStartInfo.WorkingDirectory);
            Assert.True(capturedStartInfo.UseShellExecute);
        }

        [Fact]
        public void Launch_WhenIsoFile_MountsViaPowerShellSilently()
        {
            ProcessStartInfo? capturedStartInfo = null;

            var service = new GameLauncherService(
                processStarter: info =>
                {
                    capturedStartInfo = info;
                    return null;
                },
                fileExists: _ => true);

            var game = new GameItem
            {
                Title = "Classic Disc Game",
                ExecutablePath = @"D:\ISOs\game.iso"
            };

            LaunchResult result = service.Launch(game);

            Assert.Equal(LaunchStatus.Launched, result.Status);
            Assert.NotNull(capturedStartInfo);
            Assert.Equal("powershell.exe", capturedStartInfo!.FileName);
            Assert.Contains("Mount-DiskImage", capturedStartInfo.Arguments);
            Assert.Contains(@"D:\ISOs\game.iso", capturedStartInfo.Arguments);
            Assert.True(capturedStartInfo.CreateNoWindow);
        }

        [Fact]
        public void Launch_WhenUserCancelsUacPrompt_ReturnsCanceledByUser()
        {
            var service = new GameLauncherService(
                processStarter: _ => throw new Win32Exception(1223), // ERROR_CANCELLED
                fileExists: _ => true);

            var game = new GameItem
            {
                Title = "Admin Required Game",
                ExecutablePath = @"D:\Games\AdminGame\start.exe"
            };

            LaunchResult result = service.Launch(game);

            Assert.Equal(LaunchStatus.CanceledByUser, result.Status);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public void Launch_WhenUnexpectedExceptionOccurs_ReturnsErrorWithMessage()
        {
            var service = new GameLauncherService(
                processStarter: _ => throw new UnauthorizedAccessException("Access denied by antivirus"),
                fileExists: _ => true);

            var game = new GameItem
            {
                Title = "Blocked Game",
                ExecutablePath = @"D:\Games\Blocked\game.exe"
            };

            LaunchResult result = service.Launch(game);

            Assert.Equal(LaunchStatus.Error, result.Status);
            Assert.Equal("Access denied by antivirus", result.ErrorMessage);
        }
    }
}