using System;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace MinimalGameLauncher.Models
{
    public class GameItem : INotifyPropertyChanged
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;

        private string? _imagePath;
        public string? ImagePath
        {
            get => string.IsNullOrWhiteSpace(_imagePath) ? null : _imagePath;
            set
            {
                _imagePath = value;
                OnPropertyChanged(nameof(ImagePath));
                OnPropertyChanged(nameof(HeroImagePath));
            }
        }

        private string? _heroImagePath;
        public string? HeroImagePath
        {
            get => string.IsNullOrWhiteSpace(_heroImagePath) ? ImagePath : _heroImagePath;
            set
            {
                _heroImagePath = value;
                OnPropertyChanged(nameof(HeroImagePath));
            }
        }

        private bool _isSelected;
        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        private bool _isDownloading;
        [JsonIgnore]
        public bool IsDownloading
        {
            get => _isDownloading;
            set { _isDownloading = value; OnPropertyChanged(nameof(IsDownloading)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}