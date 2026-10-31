using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using Windows.System;
using Windows.UI.Xaml;

namespace StreamioX.App.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private DispatcherTimer _telemetryTimer;

        [ObservableProperty]
        private string appVersionDisplay = "Version 1.0.0-alpha";
        [ObservableProperty]
        private string currentMemoryUsage;

        [ObservableProperty]
        private string systemHardLimit;

        [ObservableProperty]
        private double memoryPercentage;

        [ObservableProperty]
        private bool hardwareAccelerationEnabled = true;

        public SettingsViewModel()
        {
            StartTelemetry();
        }

        private void StartTelemetry()
        {
            _telemetryTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000)
            };
            _telemetryTimer.Tick += (s, e) => UpdateMemoryStats();
            _telemetryTimer.Start();
            UpdateMemoryStats();
        }

        private void UpdateMemoryStats()
        {
            double usageMB = MemoryManager.AppMemoryUsage / 1024.0 / 1024.0;
            double limitMB = MemoryManager.AppMemoryUsageLimit / 1024.0 / 1024.0;

            CurrentMemoryUsage = $"{usageMB:F2} MB";
            SystemHardLimit = $"{limitMB:F2} MB";
            MemoryPercentage = (usageMB / limitMB) * 100;
        }

        [RelayCommand]
        private void ClearImageCache()
        {
            // TODO: Implement cache clearing logic
        }
    }
}