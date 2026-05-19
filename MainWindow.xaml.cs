using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace WinOptimizer
{
    public partial class MainWindow : Window
    {
        // ─── Native API for RAM clearing ───────────────────────────────────────
        [DllImport("psapi.dll")]
        static extern bool EmptyWorkingSet(IntPtr hProcess);

        // ─── Observable Collections ────────────────────────────────────────────
        private ObservableCollection<ServiceItem> _allServices = new();
        private ObservableCollection<ServiceItem> _filteredServices = new();
        private ObservableCollection<AppItem> _allApps = new();
        private ObservableCollection<AppItem> _filteredApps = new();
        private ObservableCollection<BloatItem> _bloatItems = new();
        private ObservableCollection<PrivacyItem> _privacyItems = new();

        // ─── Service Safety Database ───────────────────────────────────────────
        private static readonly Dictionary<string, (string Risk, string Recommendation)> ServiceInfo = new()
        {
            { "XblGameSave",      ("🟢 Safe",    "Safe to disable if you don't use Xbox Game Pass.") },
            { "XboxNetApiSvc",    ("🟢 Safe",    "Xbox network service. Disable if no Xbox usage.") },
            { "XblAuthManager",   ("🟢 Safe",    "Xbox authentication. Safe to disable.") },
            { "XboxGipSvc",       ("🟢 Safe",    "Xbox accessory management. Safe to disable.") },
            { "DiagTrack",        ("🟢 Safe",    "Microsoft telemetry. Recommended to disable for privacy.") },
            { "dmwappushservice", ("🟢 Safe",    "WAP push messages. Safe to disable.") },
            { "WSearch",          ("🟡 Medium",  "Windows Search indexing. Disable to save RAM (slower file search).") },
            { "SysMain",          ("🟡 Medium",  "Superfetch. Can cause HDD thrashing. OK to disable on SSDs too.") },
            { "TabletInputService",("🟢 Safe",   "Tablet input. Disable if you don't use a touchscreen or stylus.") },
            { "Fax",              ("🟢 Safe",    "Fax service. Safe to disable — nobody uses fax anymore.") },
            { "MapsBroker",       ("🟢 Safe",    "Downloaded maps broker. Disable if you don't use Maps app.") },
            { "RetailDemo",       ("🟢 Safe",    "Retail demo service. Safe to disable on non-display PCs.") },
            { "RemoteRegistry",   ("🟢 Safe",    "Allows remote registry editing. Disable for security.") },
            { "TrkWks",           ("🟢 Safe",    "Distributed link tracking. Safe to disable on home PCs.") },
            { "WerSvc",           ("🟡 Medium",  "Windows Error Reporting. Safe to disable if you don't report errors.") },
            { "BITS",             ("🔴 Caution", "Background Intelligent Transfer. Used by Windows Update — disable with care.") },
            { "wuauserv",         ("🔴 Caution", "Windows Update. Don't disable unless you manage updates manually.") },
            { "Spooler",          ("🟡 Medium",  "Print Spooler. Disable if you have no printer.") },
            { "lfsvc",            ("🟢 Safe",    "Geolocation service. Disable for privacy.") },
            { "PhoneSvc",         ("🟢 Safe",    "Phone service. Disable if you don't link a phone.") },
            { "PrintNotify",      ("🟡 Medium",  "Printer notifications. Disable if no printer.") },
            { "WMPNetworkSvc",    ("🟢 Safe",    "Windows Media Player network sharing. Safe to disable.") },
            { "icssvc",           ("🟢 Safe",    "Mobile hotspot. Disable if you never use PC as hotspot.") },
        };

        // ─── Bloatware List ────────────────────────────────────────────────────
        private static readonly List<(string Name, string Package, string Description)> BloatwareList = new()
        {
            ("Xbox",              "Microsoft.XboxApp",                  "Xbox gaming hub"),
            ("Xbox Game Bar",     "Microsoft.XboxGamingOverlay",        "In-game overlay"),
            ("Xbox Identity",     "Microsoft.XboxIdentityProvider",     "Xbox sign-in"),
            ("Xbox Speech",       "Microsoft.XboxSpeechToTextOverlay",  "Speech overlay"),
            ("Cortana",           "Microsoft.549981C3F5F10",            "Microsoft AI assistant"),
            ("3D Viewer",         "Microsoft.Microsoft3DViewer",        "3D model viewer"),
            ("Mixed Reality",     "Microsoft.MixedReality.Portal",     "VR/AR portal"),
            ("Paint 3D",          "Microsoft.MSPaint",                  "3D version of Paint"),
            ("Solitaire",         "Microsoft.MicrosoftSolitaireCollection","Card games collection"),
            ("Candy Crush",       "king.com.CandyC rushSaga",          "Mobile game"),
            ("Get Help",          "Microsoft.GetHelp",                  "Microsoft help app"),
            ("Groove Music",      "Microsoft.ZuneMusic",                "Microsoft music player"),
            ("Movies & TV",       "Microsoft.ZuneVideo",                "Microsoft video player"),
            ("News",              "Microsoft.BingNews",                 "Bing news reader"),
            ("Weather",           "Microsoft.BingWeather",              "Bing weather app"),
            ("Finance",           "Microsoft.BingFinance",              "Bing finance app"),
            ("Sports",            "Microsoft.BingSports",               "Bing sports app"),
            ("Maps",              "Microsoft.WindowsMaps",              "Windows Maps app"),
            ("Feedback Hub",      "Microsoft.WindowsFeedbackHub",      "Microsoft feedback"),
            ("Your Phone",        "Microsoft.YourPhone",                "Phone link app"),
            ("Skype",             "Microsoft.SkypeApp",                 "Skype for Windows"),
            ("OneNote",           "Microsoft.Office.OneNote",           "OneNote app"),
            ("People",            "Microsoft.People",                   "Contacts app"),
            ("Microsoft Teams",   "MicrosoftTeams",                    "Teams chat"),
            ("Clipchamp",         "Clipchamp.Clipchamp",               "Video editor"),
            ("Tips",              "Microsoft.Getstarted",               "Windows tips app"),
        };

        // ─── Privacy Tweaks ────────────────────────────────────────────────────
        private static readonly List<(string Name, string Description, string Command)> PrivacyTweaks = new()
        {
            ("Disable Telemetry",       "Stops Microsoft from collecting diagnostic data.",
             "reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection\" /v AllowTelemetry /t REG_DWORD /d 0 /f"),
            ("Disable Activity History", "Stops Windows from recording your activities.",
             "reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System\" /v EnableActivityFeed /t REG_DWORD /d 0 /f"),
            ("Disable Location Tracking","Turns off location access for all apps.",
             "reg add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\location\" /v Value /t REG_SZ /d Deny /f"),
            ("Disable Advertising ID",  "Stops apps from using your advertising ID.",
             "reg add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo\" /v Enabled /t REG_DWORD /d 0 /f"),
            ("Disable Cortana",         "Disables Cortana completely via policy.",
             "reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search\" /v AllowCortana /t REG_DWORD /d 0 /f"),
            ("Disable Start Menu Ads",  "Removes suggested apps from Start menu.",
             "reg add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager\" /v SystemPaneSuggestionsEnabled /t REG_DWORD /d 0 /f"),
            ("Disable Bing Search",     "Removes Bing from Windows Search bar.",
             "reg add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Search\" /v BingSearchEnabled /t REG_DWORD /d 0 /f"),
            ("Disable Timeline",        "Disables Windows Timeline activity feature.",
             "reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System\" /v EnableActivityFeed /t REG_DWORD /d 0 /f"),
            ("Disable App Diagnostics", "Prevents apps from reading diagnostic info.",
             "reg add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\appDiagnostics\" /v Value /t REG_SZ /d Deny /f"),
            ("Disable Feedback Requests","Stops Windows from asking for feedback.",
             "reg add \"HKCU\\SOFTWARE\\Microsoft\\Siuf\\Rules\" /v NumberOfSIUFInPeriod /t REG_DWORD /d 0 /f"),
        };

        // ══════════════════════════════════════════════════════════════════════
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private bool _servicesLoaded = false;
        private bool _appsLoaded = false;
        private bool _bloatLoaded = false;

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadPrivacyTweaks();
            // Show Home page first — load stats in background with skeleton
            await LoadHomeStatsAsync();
        }

        private async Task LoadHomeStatsAsync()
        {
            // Stats load in background, skeletons show until ready
            await Task.Run(() =>
            {
                // RAM free
                var ramFree = GetFreeRam();
                Dispatcher.Invoke(() =>
                {
                    SkeletonRam.Visibility = Visibility.Collapsed;
                    StatRam.Text = FormatBytes(ramFree);
                    StatRam.Visibility = Visibility.Visible;
                });

                // Temp size
                long tempSize = GetFolderSize(Path.GetTempPath()) + GetFolderSize(@"C:\Windows\Temp");
                Dispatcher.Invoke(() =>
                {
                    SkeletonTemp.Visibility = Visibility.Collapsed;
                    StatTemp.Text = FormatBytes(tempSize);
                    StatTemp.Visibility = Visibility.Visible;
                });

                // Services running count
                int running = ServiceController.GetServices().Count(s => s.Status == ServiceControllerStatus.Running);
                Dispatcher.Invoke(() =>
                {
                    SkeletonSvc.Visibility = Visibility.Collapsed;
                    StatServices.Text = running.ToString();
                    StatServices.Visibility = Visibility.Visible;
                });

                // Apps count from registry
                int appCount = CountInstalledApps();
                Dispatcher.Invoke(() =>
                {
                    SkeletonApps.Visibility = Visibility.Collapsed;
                    StatApps.Text = appCount.ToString();
                    StatApps.Visibility = Visibility.Visible;
                });
            });
        }

        private static long GetFreeRam()
        {
            try
            {
                var psi = new ProcessStartInfo("wmic", "OS get FreePhysicalMemory /Value")
                { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using var p = Process.Start(psi);
                string output = p?.StandardOutput.ReadToEnd() ?? "";
                p?.WaitForExit();
                var line = output.Split('\n').FirstOrDefault(l => l.StartsWith("FreePhysicalMemory"));
                if (line != null && long.TryParse(line.Split('=')[1].Trim(), out long kb))
                    return kb * 1024;
            }
            catch { }
            return 0;
        }

        private static int CountInstalledApps()
        {
            int count = 0;
            string[] paths = {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };
            foreach (var path in paths)
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(path);
                    if (key == null) continue;
                    foreach (var sub in key.GetSubKeyNames())
                    {
                        using var s = key.OpenSubKey(sub);
                        if (s?.GetValue("DisplayName") != null &&
                            s?.GetValue("UninstallString") != null) count++;
                    }
                }
                catch { }
            }
            return count;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  NAVIGATION
        // ══════════════════════════════════════════════════════════════════════
        private void ShowPage(Grid page)
        {
            PageHome.Visibility = Visibility.Collapsed;
            PageServices.Visibility = Visibility.Collapsed;
            PageUninstaller.Visibility = Visibility.Collapsed;
            PageCache.Visibility = Visibility.Collapsed;
            PageDebloat.Visibility = Visibility.Collapsed;
            PagePrivacy.Visibility = Visibility.Collapsed;
            page.Visibility = Visibility.Visible;
        }

        private void ResetSidebarStyles()
        {
            BtnHome.Style = (Style)FindResource("SidebarBtn");
            BtnServices.Style = (Style)FindResource("SidebarBtn");
            BtnUninstaller.Style = (Style)FindResource("SidebarBtn");
            BtnCache.Style = (Style)FindResource("SidebarBtn");
            BtnDebloat.Style = (Style)FindResource("SidebarBtn");
            BtnPrivacy.Style = (Style)FindResource("SidebarBtn");
        }

        private void BtnHome_Click(object s, RoutedEventArgs e)
        {
            ResetSidebarStyles();
            BtnHome.Style = (Style)FindResource("SidebarBtnActive");
            ShowPage(PageHome);
        }

        private void BtnServices_Click(object s, RoutedEventArgs e)
        {
            ResetSidebarStyles();
            BtnServices.Style = (Style)FindResource("SidebarBtnActive");
            ShowPage(PageServices);
            if (!_servicesLoaded) { _servicesLoaded = true; _ = LoadServicesAsync(); }
        }

        private void BtnUninstaller_Click(object s, RoutedEventArgs e)
        {
            ResetSidebarStyles();
            BtnUninstaller.Style = (Style)FindResource("SidebarBtnActive");
            ShowPage(PageUninstaller);
            if (!_appsLoaded) { _appsLoaded = true; _ = LoadAppsAsync(); }
        }

        private void BtnCache_Click(object s, RoutedEventArgs e)
        {
            ResetSidebarStyles();
            BtnCache.Style = (Style)FindResource("SidebarBtnActive");
            ShowPage(PageCache);
            UpdateCacheSizes();
        }

        private void BtnDebloat_Click(object s, RoutedEventArgs e)
        {
            ResetSidebarStyles();
            BtnDebloat.Style = (Style)FindResource("SidebarBtnActive");
            ShowPage(PageDebloat);
            if (!_bloatLoaded) { _bloatLoaded = true; LoadBloatwareFast(); }
        }

        private void BtnPrivacy_Click(object s, RoutedEventArgs e)
        {
            ResetSidebarStyles();
            BtnPrivacy.Style = (Style)FindResource("SidebarBtnActive");
            ShowPage(PagePrivacy);
        }

        private async void BtnQuickClean_Click(object s, RoutedEventArgs e)
        {
            // Navigate to cache page and run clear all
            BtnCache_Click(s, e);
            await Task.Delay(300);
            BtnClearAll_Click(s, e);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SERVICES
        // ══════════════════════════════════════════════════════════════════════
        private async Task LoadServicesAsync()
        {
            _allServices.Clear();
            await Task.Run(() =>
            {
                var services = ServiceController.GetServices()
                    .OrderBy(s => s.DisplayName)
                    .ToList();

                foreach (var svc in services)
                {
                    string status = svc.Status.ToString();
                    string color = svc.Status == ServiceControllerStatus.Running ? "#27AE60" : "#888888";
                    string startup = GetStartupType(svc.ServiceName);
                    string risk = "🔵 Unknown";
                    string rec = "No specific recommendation. Research before disabling.";

                    if (ServiceInfo.TryGetValue(svc.ServiceName, out var info))
                    { risk = info.Risk; rec = info.Recommendation; }

                    Dispatcher.Invoke(() => _allServices.Add(new ServiceItem
                    {
                        ServiceName = svc.ServiceName,
                        DisplayName = svc.DisplayName,
                        Status = status,
                        StatusColor = color,
                        StartupType = startup,
                        Risk = risk,
                        Recommendation = rec,
                        Description = GetServiceDescription(svc.ServiceName)
                    }));
                }
            });

            LstServices.ItemsSource = _allServices;
        }

        private string GetStartupType(string serviceName)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    $@"SYSTEM\CurrentControlSet\Services\{serviceName}");
                if (key?.GetValue("Start") is int val)
                    return val switch { 2 => "Automatic", 3 => "Manual", 4 => "Disabled", _ => "Unknown" };
            }
            catch { }
            return "Unknown";
        }

        private string GetServiceDescription(string serviceName)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    $@"SYSTEM\CurrentControlSet\Services\{serviceName}");
                return key?.GetValue("Description")?.ToString()
                       ?? "No description available.";
            }
            catch { return "No description available."; }
        }

        private void LstServices_SelectionChanged(object s, SelectionChangedEventArgs e)
        {
            if (LstServices.SelectedItem is not ServiceItem svc) return;
            TxtSvcName.Text = svc.DisplayName;
            TxtSvcStatus.Text = svc.Status;
            TxtSvcStartup.Text = svc.StartupType;
            TxtSvcRisk.Text = svc.Risk;
            TxtSvcDescription.Text = svc.Description;
            TxtSvcRecommendation.Text = svc.Recommendation;
        }

        private void BtnDisableService_Click(object s, RoutedEventArgs e)
        {
            if (LstServices.SelectedItem is not ServiceItem svc) return;
            if (MessageBox.Show($"Disable '{svc.DisplayName}'?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            RunCmd($"sc config \"{svc.ServiceName}\" start= disabled");
            RunCmd($"sc stop \"{svc.ServiceName}\"");
            MessageBox.Show("Service disabled.", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
            _ = LoadServicesAsync();
        }

        private void BtnEnableService_Click(object s, RoutedEventArgs e)
        {
            if (LstServices.SelectedItem is not ServiceItem svc) return;
            RunCmd($"sc config \"{svc.ServiceName}\" start= demand");
            RunCmd($"sc start \"{svc.ServiceName}\"");
            MessageBox.Show("Service enabled.", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
            _ = LoadServicesAsync();
        }

        private void BtnStartService_Click(object s, RoutedEventArgs e)
        {
            if (LstServices.SelectedItem is not ServiceItem svc) return;
            RunCmd($"sc start \"{svc.ServiceName}\"");
            _ = LoadServicesAsync();
        }

        private void BtnStopService_Click(object s, RoutedEventArgs e)
        {
            if (LstServices.SelectedItem is not ServiceItem svc) return;
            if (MessageBox.Show($"Stop '{svc.DisplayName}'?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            RunCmd($"sc stop \"{svc.ServiceName}\"");
            _ = LoadServicesAsync();
        }

        private void BtnRefreshServices_Click(object s, RoutedEventArgs e) => _ = LoadServicesAsync();

        private void TxtServiceSearch_TextChanged(object s, TextChangedEventArgs e)
        {
            if (LstServices == null || _allServices == null) return;
            string q = TxtServiceSearch.Text.ToLower();
            if (q == "search services...") { LstServices.ItemsSource = _allServices; return; }
            LstServices.ItemsSource = new ObservableCollection<ServiceItem>(
                _allServices.Where(x =>
                    x.DisplayName.ToLower().Contains(q) ||
                    x.ServiceName.ToLower().Contains(q)));
        }

        private void TxtServiceSearch_GotFocus(object s, RoutedEventArgs e)
        { if (TxtServiceSearch.Text == "Search services...") TxtServiceSearch.Text = ""; }

        private void TxtServiceSearch_LostFocus(object s, RoutedEventArgs e)
        { if (string.IsNullOrWhiteSpace(TxtServiceSearch.Text)) TxtServiceSearch.Text = "Search services..."; }

        // ══════════════════════════════════════════════════════════════════════
        //  APP UNINSTALLER
        // ══════════════════════════════════════════════════════════════════════
        private async Task LoadAppsAsync()
        {
            _allApps.Clear();
            await Task.Run(() =>
            {
                var regPaths = new[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };

                foreach (var path in regPaths)
                {
                    using var key = Registry.LocalMachine.OpenSubKey(path);
                    if (key == null) continue;
                    foreach (var subName in key.GetSubKeyNames())
                    {
                        using var sub = key.OpenSubKey(subName);
                        if (sub == null) continue;
                        string name = sub.GetValue("DisplayName")?.ToString() ?? "";
                        string publisher = sub.GetValue("Publisher")?.ToString() ?? "";
                        string uninstall = sub.GetValue("UninstallString")?.ToString() ?? "";
                        string sizeStr = "";

                        if (sub.GetValue("EstimatedSize") is int size)
                            sizeStr = size > 1024 ? $"{size / 1024} MB" : $"{size} KB";

                        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(uninstall)) continue;

                        Dispatcher.Invoke(() => _allApps.Add(new AppItem
                        {
                            Name = name,
                            Publisher = publisher,
                            Size = sizeStr,
                            UninstallString = uninstall,
                            RegistryKey = subName
                        }));
                    }
                }
            });

            _allApps = new ObservableCollection<AppItem>(
                _allApps.OrderBy(a => a.Name));
            LstApps.ItemsSource = _allApps;
        }

        private async void BtnUninstallApp_Click(object s, RoutedEventArgs e)
        {
            if (((Button)s).Tag is not AppItem app) return;
            if (MessageBox.Show($"Deep uninstall '{app.Name}'?\n\nThis will:\n• Run the official uninstaller\n• Remove registry entries\n• Clean leftover folders",
                "Confirm Deep Uninstall", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            AppendLog(TxtUninstallLog, $"[{DateTime.Now:HH:mm:ss}] Starting uninstall: {app.Name}");

            // Step 1: Run uninstaller
            try
            {
                string uninstall = app.UninstallString;
                if (uninstall.StartsWith("msiexec", StringComparison.OrdinalIgnoreCase))
                    RunCmd(uninstall + " /quiet");
                else
                {
                    var psi = new ProcessStartInfo("cmd", $"/c \"{uninstall}\"")
                    { UseShellExecute = true, Verb = "runas" };
                    Process.Start(psi)?.WaitForExit();
                }
                AppendLog(TxtUninstallLog, $"  ✅ Uninstaller ran.");
            }
            catch (Exception ex) { AppendLog(TxtUninstallLog, $"  ⚠ Uninstaller error: {ex.Message}"); }

            await Task.Delay(1500);

            // Step 2: Registry cleanup
            try
            {
                string[] regPaths = {
                    $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{app.RegistryKey}",
                    $@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{app.RegistryKey}",
                    $@"SOFTWARE\{app.Name}",
                    $@"SOFTWARE\WOW6432Node\{app.Name}"
                };
                foreach (var rp in regPaths)
                {
                    try { Registry.LocalMachine.DeleteSubKeyTree(rp, false); } catch { }
                    try { Registry.CurrentUser.DeleteSubKeyTree(rp, false); } catch { }
                }
                AppendLog(TxtUninstallLog, $"  ✅ Registry cleaned.");
            }
            catch { AppendLog(TxtUninstallLog, "  ⚠ Registry cleanup partial."); }

            // Step 3: Folder cleanup
            string safeName = string.Concat(app.Name.Split(Path.GetInvalidFileNameChars()));
            string[] folders = {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), safeName),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), safeName),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), safeName),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), safeName),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), safeName),
            };
            int removed = 0;
            foreach (var f in folders)
            {
                try { if (Directory.Exists(f)) { Directory.Delete(f, true); removed++; } }
                catch { }
            }
            AppendLog(TxtUninstallLog, $"  ✅ Removed {removed} leftover folder(s).");
            AppendLog(TxtUninstallLog, $"[{DateTime.Now:HH:mm:ss}] Done: {app.Name}\n");

            await LoadAppsAsync();
        }

        private void BtnRefreshApps_Click(object s, RoutedEventArgs e) => _ = LoadAppsAsync();

        private void TxtAppSearch_TextChanged(object s, TextChangedEventArgs e)
        {
            if (LstApps == null || _allApps == null) return;
            string q = TxtAppSearch.Text.ToLower();
            if (q == "search apps...") { LstApps.ItemsSource = _allApps; return; }
            LstApps.ItemsSource = new ObservableCollection<AppItem>(
                _allApps.Where(x => x.Name.ToLower().Contains(q) || x.Publisher.ToLower().Contains(q)));
        }

        private void TxtAppSearch_GotFocus(object s, RoutedEventArgs e)
        { if (TxtAppSearch.Text == "Search apps...") TxtAppSearch.Text = ""; }

        private void TxtAppSearch_LostFocus(object s, RoutedEventArgs e)
        { if (string.IsNullOrWhiteSpace(TxtAppSearch.Text)) TxtAppSearch.Text = "Search apps..."; }

        // ══════════════════════════════════════════════════════════════════════
        //  CACHE CLEANER
        // ══════════════════════════════════════════════════════════════════════
        private void UpdateCacheSizes()
        {
            Task.Run(() =>
            {
                // RAM
                var mem = GC.GetTotalMemory(false);
                string ram = $"{Environment.WorkingSet / 1024 / 1024} MB used by this app — system RAM varies";
                Dispatcher.Invoke(() => TxtRamInfo.Text = ram);

                // Temp
                long tempSize = GetFolderSize(Path.GetTempPath()) + GetFolderSize(@"C:\Windows\Temp");
                Dispatcher.Invoke(() => TxtTempInfo.Text = FormatBytes(tempSize));

                // WU Cache
                long wuSize = GetFolderSize(@"C:\Windows\SoftwareDistribution\Download");
                Dispatcher.Invoke(() => TxtWuInfo.Text = FormatBytes(wuSize));

                // Browser
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                long bSize =
                    GetFolderSize(Path.Combine(local, @"Google\Chrome\User Data\Default\Cache")) +
                    GetFolderSize(Path.Combine(local, @"Microsoft\Edge\User Data\Default\Cache")) +
                    GetFolderSize(Path.Combine(local, @"Mozilla\Firefox"));
                Dispatcher.Invoke(() => TxtBrowserInfo.Text = FormatBytes(bSize));
            });
        }

        private void BtnClearRam_Click(object s, RoutedEventArgs e)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            int cleared = 0;
            foreach (var proc in Process.GetProcesses())
            {
                try { EmptyWorkingSet(proc.Handle); cleared++; } catch { }
            }
            AppendLog(TxtCacheLog, $"[{DateTime.Now:HH:mm:ss}] RAM working set cleared for {cleared} processes.");
            UpdateCacheSizes();
        }

        private async void BtnClearTemp_Click(object s, RoutedEventArgs e)
        {
            long freed = await Task.Run(() => ClearFolder(Path.GetTempPath()) + ClearFolder(@"C:\Windows\Temp"));
            AppendLog(TxtCacheLog, $"[{DateTime.Now:HH:mm:ss}] Temp files cleared — freed {FormatBytes(freed)}.");
            UpdateCacheSizes();
        }

        private void BtnFlushDns_Click(object s, RoutedEventArgs e)
        {
            RunCmd("ipconfig /flushdns");
            AppendLog(TxtCacheLog, $"[{DateTime.Now:HH:mm:ss}] DNS cache flushed.");
        }

        private async void BtnClearWUCache_Click(object s, RoutedEventArgs e)
        {
            AppendLog(TxtCacheLog, $"[{DateTime.Now:HH:mm:ss}] Stopping Windows Update service...");
            RunCmd("net stop wuauserv");
            await Task.Delay(1000);
            long freed = await Task.Run(() => ClearFolder(@"C:\Windows\SoftwareDistribution\Download"));
            RunCmd("net start wuauserv");
            AppendLog(TxtCacheLog, $"[{DateTime.Now:HH:mm:ss}] Windows Update cache cleared — freed {FormatBytes(freed)}.");
            UpdateCacheSizes();
        }

        private async void BtnClearBrowser_Click(object s, RoutedEventArgs e)
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            long freed = await Task.Run(() =>
                ClearFolder(Path.Combine(local, @"Google\Chrome\User Data\Default\Cache")) +
                ClearFolder(Path.Combine(local, @"Microsoft\Edge\User Data\Default\Cache")) +
                ClearFolder(Path.Combine(local, @"Mozilla\Firefox\Profiles")));
            AppendLog(TxtCacheLog, $"[{DateTime.Now:HH:mm:ss}] Browser caches cleared — freed {FormatBytes(freed)}.");
            UpdateCacheSizes();
        }

        private async void BtnClearThumbnails_Click(object s, RoutedEventArgs e)
        {
            string thumbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Microsoft\Windows\Explorer");
            long freed = await Task.Run(() =>
            {
                long total = 0;
                if (!Directory.Exists(thumbPath)) return 0L;
                foreach (var f in Directory.GetFiles(thumbPath, "thumbcache_*.db"))
                {
                    try { total += new FileInfo(f).Length; File.Delete(f); } catch { }
                }
                return total;
            });
            AppendLog(TxtCacheLog, $"[{DateTime.Now:HH:mm:ss}] Thumbnail cache cleared — freed {FormatBytes(freed)}.");
        }

        private async void BtnClearAll_Click(object s, RoutedEventArgs e)
        {
            AppendLog(TxtCacheLog, $"[{DateTime.Now:HH:mm:ss}] ⚡ Clearing ALL caches...");
            BtnClearRam_Click(s, e);
            BtnClearTemp_Click(s, e);
            BtnFlushDns_Click(s, e);
            BtnClearWUCache_Click(s, e);
            BtnClearBrowser_Click(s, e);
            BtnClearThumbnails_Click(s, e);
            AppendLog(TxtCacheLog, $"[{DateTime.Now:HH:mm:ss}] ✅ All done!");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  DEBLOAT
        // ══════════════════════════════════════════════════════════════════════
        private void LoadBloatwareFast()
        {
            _bloatItems.Clear();
            // Show all items instantly with "Checking..." status
            foreach (var (name, pkg, desc) in BloatwareList)
                _bloatItems.Add(new BloatItem
                {
                    Name = name,
                    PackageName = pkg,
                    Description = desc,
                    Status = "Checking...",
                    StatusColor = "#888",
                    IsInstalled = false
                });
            LstBloatware.ItemsSource = _bloatItems;

            // Then check all in ONE PowerShell call in background
            Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo("powershell",
                        "-Command \"Get-AppxPackage | Select-Object -ExpandProperty Name\"")
                    { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                    using var p = Process.Start(psi);
                    string output = p?.StandardOutput.ReadToEnd() ?? "";
                    p?.WaitForExit();
                    var installed = new HashSet<string>(
                        output.Split('\n').Select(l => l.Trim().ToLower()));

                    Dispatcher.Invoke(() =>
                    {
                        foreach (var item in _bloatItems)
                        {
                            bool found = installed.Any(l => l.Contains(item.PackageName.ToLower()));
                            item.Status = found ? "Installed" : "Removed";
                            item.StatusColor = found ? "#E74C3C" : "#27AE60";
                            item.IsInstalled = found;
                        }
                        LstBloatware.Items.Refresh();
                    });
                }
                catch { }
            });
        }

        private void LoadBloatware() => LoadBloatwareFast();

        private void BtnRemoveBloat_Click(object s, RoutedEventArgs e)
        {
            if (((Button)s).Tag is not BloatItem item) return;
            RemoveBloat(item);
        }

        private void BtnRemoveSelected_Click(object s, RoutedEventArgs e)
        {
            foreach (var item in _bloatItems.Where(i => i.IsSelected && i.IsInstalled))
                RemoveBloat(item);
        }

        private void RemoveBloat(BloatItem item)
        {
            if (MessageBox.Show($"Remove '{item.Name}'?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            RunPowerShell($"Get-AppxPackage -Name '{item.PackageName}' | Remove-AppxPackage");
            RunPowerShell($"Get-AppxProvisionedPackage -Online | Where-Object DisplayName -like '*{item.PackageName}*' | Remove-AppxProvisionedPackage -Online");
            MessageBox.Show($"'{item.Name}' removed.", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
            LoadBloatware();
        }

        private void BtnRefreshBloat_Click(object s, RoutedEventArgs e) => LoadBloatwareFast();

        // ══════════════════════════════════════════════════════════════════════
        //  PRIVACY
        // ══════════════════════════════════════════════════════════════════════
        private void LoadPrivacyTweaks()
        {
            _privacyItems.Clear();
            foreach (var (name, desc, cmd) in PrivacyTweaks)
                _privacyItems.Add(new PrivacyItem { Name = name, Description = desc, Command = cmd, ActionText = "Apply" });
            LstPrivacy.ItemsSource = _privacyItems;
        }

        private void BtnApplyPrivacy_Click(object s, RoutedEventArgs e)
        {
            if (((Button)s).Tag is not PrivacyItem item) return;
            if (MessageBox.Show($"Apply: {item.Name}?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            RunCmd(item.Command);
            item.ActionText = "✅ Applied";
            LstPrivacy.Items.Refresh();
            MessageBox.Show($"'{item.Name}' applied.\nA restart may be needed.", "Done",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════════════
        private static void RunCmd(string args)
        {
            try
            {
                var psi = new ProcessStartInfo("cmd", $"/c {args}")
                { UseShellExecute = false, CreateNoWindow = true, Verb = "runas" };
                Process.Start(psi)?.WaitForExit();
            }
            catch { }
        }

        private static void RunPowerShell(string cmd)
        {
            try
            {
                var psi = new ProcessStartInfo("powershell",
                    $"-ExecutionPolicy Bypass -Command \"{cmd}\"")
                { UseShellExecute = true, Verb = "runas", CreateNoWindow = false };
                Process.Start(psi)?.WaitForExit();
            }
            catch { }
        }

        private static long GetFolderSize(string path)
        {
            if (!Directory.Exists(path)) return 0;
            try { return new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); }
            catch { return 0; }
        }

        private static long ClearFolder(string path)
        {
            if (!Directory.Exists(path)) return 0;
            long freed = 0;
            foreach (var f in Directory.GetFiles(path))
            {
                try { freed += new FileInfo(f).Length; File.Delete(f); } catch { }
            }
            foreach (var d in Directory.GetDirectories(path))
            {
                try { freed += GetFolderSize(d); Directory.Delete(d, true); } catch { }
            }
            return freed;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024:F1} MB";
            return $"{bytes / 1024.0 / 1024 / 1024:F2} GB";
        }

        private static void AppendLog(TextBlock tb, string msg)
        {
            tb.Text += (string.IsNullOrEmpty(tb.Text) || tb.Text.EndsWith("\n") ? "" : "\n") + msg;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  DATA MODELS
    // ══════════════════════════════════════════════════════════════════════════
    public class ServiceItem : INotifyPropertyChanged
    {
        public string ServiceName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Status { get; set; } = "";
        public string StatusColor { get; set; } = "#888";
        public string StartupType { get; set; } = "";
        public string Risk { get; set; } = "";
        public string Recommendation { get; set; } = "";
        public string Description { get; set; } = "";
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class AppItem : INotifyPropertyChanged
    {
        public string Name { get; set; } = "";
        public string Publisher { get; set; } = "";
        public string Size { get; set; } = "";
        public string UninstallString { get; set; } = "";
        public string RegistryKey { get; set; } = "";
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class BloatItem : INotifyPropertyChanged
    {
        public string Name { get; set; } = "";
        public string PackageName { get; set; } = "";
        public string Description { get; set; } = "";
        public string Status { get; set; } = "";
        public string StatusColor { get; set; } = "#888";
        public bool IsInstalled { get; set; }
        public bool IsSelected { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class PrivacyItem : INotifyPropertyChanged
    {
        private string _actionText = "Apply";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Command { get; set; } = "";
        public string ActionText { get => _actionText; set { _actionText = value; PropertyChanged?.Invoke(this, new(nameof(ActionText))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
