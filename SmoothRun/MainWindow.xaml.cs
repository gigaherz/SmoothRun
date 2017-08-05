using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using SmoothRun.Annotations;

namespace SmoothRun
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INotifyPropertyChanged
    {
        public double ComputedMaxWidth => SystemParameters.WorkArea.Width * 0.8;

        public DateTime StartupDate { get; set; } = DateTime.Now.AddSeconds(10);
        public TimeSpan RemainingTime => StartupDate - DateTime.Now;

        private Brush _popupBackground;
        private Brush _popupBorder;
        private Brush _popupPanel;
        private bool _hasMoreItems;
        private AppStatus _appStatus;

        public AppStatus AppStatus
        {
            get => _appStatus;
            set
            {
                if (value == _appStatus) return;
                _appStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public string StatusText
        {
            get
            {
                switch(AppStatus)
                {
                    case AppStatus.Waiting: return $"The following applications will start launching in {RemainingTime:mm':'ss} seconds...";
                    case AppStatus.Launching: return "Starting applications...";
                }
                return "";
            }
        }

        public Brush PopupBackground
        {
            get => _popupBackground;
            private set
            {
                if (Equals(value, _popupBackground)) return;
                _popupBackground = value;
                OnPropertyChanged();
            }
        }

        public Brush PopupBorder
        {
            get => _popupBorder;
            set
            {
                if (Equals(value, _popupBorder)) return;
                _popupBorder = value;
                OnPropertyChanged();
            }
        }

        public Brush PopupPanel
        {
            get => _popupPanel;
            set
            {
                if (Equals(value, _popupPanel)) return;
                _popupPanel = value;
                OnPropertyChanged();
            }
        }

        public bool HasMoreItems
        {
            get { return _hasMoreItems; }
            set
            {
                if (value == _hasMoreItems) return;
                _hasMoreItems = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasMoreItemsVisible));
            }
        }

        public Visibility HasMoreItemsVisible => HasMoreItems ? Visibility.Visible : Visibility.Collapsed;

        private ObservableCollection<AppEntry> _appList;
        public ObservableCollection<AppEntry> AppList
        {
            get
            {
                if (_appList == null)
                {
                    _appList = new ObservableCollection<AppEntry>();

                    var startMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                    var smoothStartup = Path.Combine(startMenu, "Smooth Startup");
                    if (Directory.Exists(smoothStartup))
                    {
                        foreach (var entry in Directory.EnumerateFiles(smoothStartup, "*.lnk"))
                        {
                            _appList.Add(new AppEntry
                            {
                                Title = Path.GetFileNameWithoutExtension(entry),
                                FullPath = entry
                            });
                        }
                    }

                    var startMenuCommon = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
                    var smoothStartupCommon = Path.Combine(startMenuCommon, "Smooth Startup");
                    if (Directory.Exists(smoothStartupCommon))
                    {
                        foreach (var entry in Directory.EnumerateFiles(smoothStartupCommon, "*.lnk"))
                        {
                            _appList.Add(new AppEntry
                            {
                                Title = Path.GetFileNameWithoutExtension(entry),
                                FullPath = entry
                            });
                        }
                    }
                }
                return _appList;
            }
        }

        DispatcherTimer timer;
        public MainWindow()
        {
            InitializeComponent();

            SystemEvents.UserPreferenceChanging += SystemEvents_UserPreferenceChanging;

            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => {

                UpdateAccentColor();

                Natives.EnableBlur(this);
            }));

            timer = new DispatcherTimer(DispatcherPriority.Normal);
            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void UpdateAccentColor()
        {
            File.WriteAllLines(@"F:\Accents.txt", AccentColorSet.ActiveSet.GetAllColorNames().Select(s => {
                var c = AccentColorSet.ActiveSet[s];
                return $"{s}: {c}";
            }));
            var c1 = AccentColorSet.ActiveSet["SystemAccent"];
            var c2 = AccentColorSet.ActiveSet["SystemAccentDark2"];
            c2.A = 192;
            PopupBackground = new SolidColorBrush(c2);
            PopupBorder = new SolidColorBrush(c1);
        }

        private void SystemEvents_UserPreferenceChanging(object sender, UserPreferenceChangingEventArgs e)
        {
            UpdateAccentColor();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            HasMoreItems = ScrollViewer.ExtentWidth > ScrollViewer.ViewportWidth;
        }

        private void Timer_Tick(object sender, EventArgs eventArgs)
        {
            if (DateTime.Now < StartupDate)
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(RemainingTime));
                return;
            }

            AppStatus = AppStatus.Launching;

            // See if the current app is done

            var previous = LaunchPhase.Done;
            bool first = true;
            foreach (var app in AppList)
            {
                if (app.Phase < LaunchPhase.Done)
                {
                    if (previous >= LaunchPhase.Done)
                    {
                        if (app.Phase < LaunchPhase.Next)
                        {
                            BeginAppCooldown(app, first);
                        }

                        if (app.Phase == LaunchPhase.Next)
                        {
                            if (IsCooldownOver(app))
                                StartLaunching(app);
                        }
                        else
                        {
                            if (IsAppReady(app))
                            {
                                app.Process?.Dispose();
                                app.Process = null;
                                app.Phase = LaunchPhase.Done;
                            }
                        }
                    }
                    else if (previous >= LaunchPhase.Launching)
                    {
                        app.Phase = LaunchPhase.Waiting;
                    }
                    else
                    {
                        app.Phase = LaunchPhase.Idle;
                    }
                }

                app.Pulse();

                previous = app.Phase;
                first = false;
            }

            // If nothing remains to launch, close.
            if (previous == LaunchPhase.Done)
            {
                Close();
                return;
            }

            OnPropertyChanged(nameof(StatusText));
        }

        private void BeginAppCooldown(AppEntry app, bool first)
        {
            app.WaitDate = first ? DateTime.Now : DateTime.Now.AddSeconds(5);
            app.Phase = LaunchPhase.Next;
        }

        private bool IsCooldownOver(AppEntry app)
        {
            return DateTime.Now >= app.WaitDate;
        }

        private void StartLaunching(AppEntry app)
        {
            app.Phase = LaunchPhase.Launching;
            app.LaunchDate = DateTime.Now;
            try
            {
                app.Process = Process.Start(new ProcessStartInfo(app.FullPath) { UseShellExecute = true });
            }
            catch(Exception)
            {
                app.Phase = LaunchPhase.Error;
            }
        }

        private bool IsAppReady(AppEntry app)
        {
            if (app.Process == null)
                return true;

            if (app.Process.HasExited)
                return true;

            try
            {
                return app.Process.WaitForInputIdle(0);
            }
            catch(InvalidOperationException)
            {
                return true;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class AppEntry : INotifyPropertyChanged
    {
        private LaunchPhase _phase;
        private TimeSpan _launchTime;

        internal Process Process { get; set; }

        public Visibility Visibility => Phase == LaunchPhase.Done ? Visibility.Collapsed : Visibility.Visible;
        public string Title { get; set; }
        public string FullPath { get; set; }

        public DateTime WaitDate { get; set; }
        public TimeSpan WaitTime => WaitDate - DateTime.Now;
        public Visibility ProgressVisibility => Phase == LaunchPhase.Next ? Visibility.Visible : Visibility.Hidden;
        public double ProgressAngle => 360 * WaitTime.TotalSeconds / 5.0;
        public bool ProgressLarge => WaitTime.TotalSeconds > 2.5;
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
            get => _phase;
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
                switch(Phase)
                {
                    case LaunchPhase.Waiting: return "Waiting...";
                    case LaunchPhase.Next: return $"Starts in {WaitTime:mm':'ss}...";
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
            OnPropertyChanged(nameof(WaitTime));
            OnPropertyChanged(nameof(ProgressAngle));
            OnPropertyChanged(nameof(ProgressLarge));
            OnPropertyChanged(nameof(ProgressPoint));
            OnPropertyChanged(nameof(ProgressVisibility));
            OnPropertyChanged(nameof(LaunchTime));
            OnPropertyChanged(nameof(PhaseTitle));
        }
    }

    public enum LaunchPhase
    {
        Idle,
        Waiting,
        Next,
        Launching,
        Done,
        Error
    }

    public enum AppStatus
    {
        Waiting,
        Launching
    }
}
