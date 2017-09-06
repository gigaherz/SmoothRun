using SmoothRun.Annotations;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace SmoothRun
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INotifyPropertyChanged
    {
        SmoothConfig mConfig = new SmoothConfig();

        public double ComputedMaxWidth => SystemParameters.WorkArea.Width * 0.8;

        public int StartupTick { get; set; } = Environment.TickCount + 10 * 1000;
        public int RemainingTicks => StartupTick - Environment.TickCount;

        private Brush _popupBackground;
        private Brush _popupBorder;
        private Brush _popupPanel;
        private bool _hasMoreItems;
        private AppStatus _appStatus;

        public AppStatus AppStatus
        {
            get { return _appStatus; }
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
                    case AppStatus.Waiting: return $"The following applications will start launching in {TimeSpan.FromMilliseconds(RemainingTicks):mm':'ss} seconds...";
                    case AppStatus.Launching: return "Starting applications...";
                }
                return "";
            }
        }

        public Brush PopupBackground
        {
            get { return _popupBackground; }
            private set
            {
                if (Equals(value, _popupBackground)) return;
                _popupBackground = value;
                OnPropertyChanged();
            }
        }

        public Brush PopupBorder
        {
            get { return _popupBorder; }
            set
            {
                if (Equals(value, _popupBorder)) return;
                _popupBorder = value;
                OnPropertyChanged();
            }
        }

        public Brush PopupPanel
        {
            get { return _popupPanel; }
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
                    var startMenuCommon = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
                    var smoothStartupCommon = Path.Combine(startMenuCommon, "Smooth Startup");

                    mConfig.LoadFrom(new[] { Path.Combine(smoothStartup, ".smoothconfig"), Path.Combine(smoothStartupCommon, ".smoothconfig") });

                    if (Directory.Exists(smoothStartup))
                    {
                        foreach (var entry in Directory.EnumerateFiles(smoothStartup, "*.lnk"))
                        {
                            _appList.Add(new AppEntry
                            {
                                Title = Path.GetFileNameWithoutExtension(entry),
                                FullPath = entry,
                                Timeout = mConfig.Timeout
                            });
                        }
                    }

                    if (Directory.Exists(smoothStartupCommon))
                    {
                        foreach (var entry in Directory.EnumerateFiles(smoothStartupCommon, "*.lnk"))
                        {
                            _appList.Add(new AppEntry
                            {
                                Title = Path.GetFileNameWithoutExtension(entry),
                                FullPath = entry,
                                Timeout = mConfig.Timeout
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

            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => {
                Natives.EnableBlur(this);
            }));

            timer = new DispatcherTimer(DispatcherPriority.Normal);
            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += Timer_Tick;
            timer.Start();
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
            if (RemainingTicks > 0 && mConfig.FirstIsSpecial)
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(RemainingTicks));
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
                    previous = app.Phase;
                }

                app.Pulse();
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
            if (mConfig.FirstIsSpecial)
                app.ExecuteTick = first ? Environment.TickCount : Environment.TickCount + (1000 * app.Timeout);
            else
                app.ExecuteTick = Environment.TickCount + (1000 * app.Timeout);
            app.Phase = LaunchPhase.Next;
        }

        private bool IsCooldownOver(AppEntry app)
        {
            return app.ExecuteTick != 0 && (Environment.TickCount - app.ExecuteTick) > 0;
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

        private void Launch_Click(object sender, RoutedEventArgs e)
        {
            AppEntry app = (sender as Control)?.DataContext as AppEntry;
            if (app != null)
            {
                StartLaunching(app);
            }
        }

        private void CancelLaunch_Click(object sender, RoutedEventArgs e)
        {
            AppEntry app = (sender as Control)?.DataContext as AppEntry;
            if (app != null)
            {
                app.Phase = LaunchPhase.Done;
            }
        }
    }
}
