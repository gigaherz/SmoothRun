using SmoothRun.Annotations;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;

namespace SmoothRun
{
    public class AppEntry : INotifyPropertyChanged
    {
        private LaunchPhase _phase;

        internal Process Process { get; set; }

        public Visibility Visibility => Phase == LaunchPhase.Done ? Visibility.Collapsed : Visibility.Visible;
        public string Title { get; set; }
        public string FullPath { get; set; }
        public int Timeout { get; set; } = 5;

        public int ExecuteTick { get; internal set; }
        public int WaitSeconds => (ExecuteTick - Environment.TickCount) / 1000;
        public Visibility ProgressVisibility => Phase == LaunchPhase.Next ? Visibility.Visible : Visibility.Hidden;
        public double ProgressAngle => 360 * WaitSeconds / Timeout;
        public bool ProgressLarge => ProgressAngle > 180;
        public Point ProgressPoint
        {
            get
            {
                var angleRad = (ProgressAngle - 90) * Math.PI / 180.0;

                var endPoint = new Point(
                    50 + 50 * Math.Cos(angleRad),
                    50 + 50 * Math.Sin(angleRad));

                if (Math.Abs(50 - Math.Round(endPoint.X)) < 1 &&
                    Math.Abs(0 - Math.Round(endPoint.Y)) < 1)
                    endPoint.X -= 0.01;

                return endPoint;
            }
        }

        public DateTime LaunchDate { get; set; }
        public TimeSpan LaunchTime => DateTime.Now - LaunchDate;

        public LaunchPhase Phase
        {
            get { return _phase; }
            set
            {
                if (value == _phase) return;
                _phase = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PhaseTitle));
                OnPropertyChanged(nameof(Visibility));
            }
        }

        public string PhaseTitle
        {
            get
            {
                switch (Phase)
                {
                    case LaunchPhase.Waiting: return "Waiting...";
                    case LaunchPhase.Next: return $"Starts in {TimeSpan.FromSeconds(WaitSeconds):mm':'ss}...";
                    case LaunchPhase.Launching: return $"Launching... {LaunchTime:mm':'ss}";
                }
                return "";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Pulse()
        {
            OnPropertyChanged(nameof(WaitSeconds));
            OnPropertyChanged(nameof(ProgressAngle));
            OnPropertyChanged(nameof(ProgressLarge));
            OnPropertyChanged(nameof(ProgressPoint));
            OnPropertyChanged(nameof(ProgressVisibility));
            OnPropertyChanged(nameof(LaunchTime));
            OnPropertyChanged(nameof(PhaseTitle));
        }
    }
}
