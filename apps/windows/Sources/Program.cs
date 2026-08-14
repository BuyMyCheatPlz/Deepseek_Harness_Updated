// DeepSeek Harness Windows launcher.
//
// A thin native wrapper around `dsh web`, mirroring the macOS shell in
// apps/macos: it resolves the dsh and node executables, launches the web
// runner as a child process, opens the default browser once the port accepts
// connections, and on quit terminates the child's process tree and verifies
// the port is released before exiting. The server itself is unchanged; this
// file only owns its lifecycle.
//
// Compiled with the .NET Framework csc.exe shipped with Windows (C# 5), so
// the build needs no SDK: keep the code within C# 5 and .NET Framework 4.x
// APIs.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;

namespace DeepSeekHarness
{
    // Settings come from the registry (HKCU\Software\DeepSeek Harness), then
    // command-line overrides, then defaults. Command-line keys mirror the
    // macOS shell's UserDefaults argument domain (-port, -stateDir, ...).
    internal static class Settings
    {
        public const string BundleId = "ai.deepseek.harness";
        public const string RegistryKey = @"Software\DeepSeek Harness";

        public static int Port = 3080;
        public static bool OpenBrowserOnLaunch = false;
        public static bool SingleInstance = true;
        public static string DshPath;  // explicit path to dsh lib/bin.js
        public static string NodePath; // explicit path to node.exe
        public static string StateDir; // lock/log location override (tests)

        // Update check (checked before the server starts). The repo list is
        // semicolon- or comma-separated `owner/repo` values; when empty it falls
        // back to BuildInfo.DefaultUpdateRepos, and npm is an extra version
        // source because upstream publishes the webui to npm rather than to
        // GitHub Releases.
        public static bool CheckUpdates = true;
        public static bool CheckNpm = true;
        public static string UpdateRepos; // null -> BuildInfo.DefaultUpdateRepos

        public static void Load()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKey))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("port");
                        if (value is int) Port = (int)value;
                        value = key.GetValue("openBrowserOnLaunch");
                        if (value is int) OpenBrowserOnLaunch = ((int)value) != 0;
                        value = key.GetValue("singleInstance");
                        if (value is int) SingleInstance = ((int)value) != 0;
                        DshPath = key.GetValue("dshPath") as string;
                        NodePath = key.GetValue("nodePath") as string;
                        StateDir = key.GetValue("stateDir") as string;
                        value = key.GetValue("checkUpdates");
                        if (value is int) CheckUpdates = ((int)value) != 0;
                        value = key.GetValue("checkNpm");
                        if (value is int) CheckNpm = ((int)value) != 0;
                        UpdateRepos = key.GetValue("updateRepos") as string;
                    }
                }
            }
            catch (Exception)
            {
            }

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == "-port" && i + 1 < args.Length)
                {
                    int parsed;
                    if (int.TryParse(args[++i], out parsed)) Port = parsed;
                }
                else if (arg == "-openBrowserOnLaunch" && i + 1 < args.Length)
                {
                    OpenBrowserOnLaunch = args[++i] != "0";
                }
                else if (arg == "-singleInstance" && i + 1 < args.Length)
                {
                    SingleInstance = args[++i] != "0";
                }
                else if (arg == "-dshPath" && i + 1 < args.Length)
                {
                    DshPath = args[++i];
                }
                else if (arg == "-nodePath" && i + 1 < args.Length)
                {
                    NodePath = args[++i];
                }
                else if (arg == "-stateDir" && i + 1 < args.Length)
                {
                    StateDir = args[++i];
                }
                else if (arg == "-checkUpdates" && i + 1 < args.Length)
                {
                    CheckUpdates = args[++i] != "0";
                }
                else if (arg == "-checkNpm" && i + 1 < args.Length)
                {
                    CheckNpm = args[++i] != "0";
                }
                else if (arg == "-updateRepos" && i + 1 < args.Length)
                {
                    UpdateRepos = args[++i];
                }
            }
        }

        public static bool HasArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == name) return true;
            }
            return false;
        }

        public static string StatePath()
        {
            if (StateDir != null) return StateDir;
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DeepSeek Harness");
        }

        public static string ServerLogPath() { return Path.Combine(StatePath(), "server.log"); }
        public static string ServerLockPath() { return Path.Combine(StatePath(), "server.pid"); }
        public static string AppLockPath() { return Path.Combine(StatePath(), "app.pid"); }
    }

    internal static class Resolver
    {
        private static List<string> CandidateDirs()
        {
            List<string> dirs = new List<string>();
            string path = Environment.GetEnvironmentVariable("PATH");
            if (path != null) dirs.AddRange(path.Split(Path.PathSeparator));
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            dirs.Add(Path.Combine(appData, "npm"));
            dirs.Add(Path.Combine(programFiles, "nodejs"));
            dirs.Add(Path.Combine(localData, "Programs", "nodejs"));
            dirs.Add(Path.Combine(localData, "Volta", "bin"));
            dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bun", "bin"));
            try
            {
                string nvm = Path.Combine(appData, "nvm");
                if (Directory.Exists(nvm))
                {
                    foreach (string version in Directory.GetDirectories(nvm)) dirs.Add(version);
                }
            }
            catch (Exception)
            {
            }
            try
            {
                string npx = Path.Combine(localData, "npm-cache", "_npx");
                if (Directory.Exists(npx))
                {
                    foreach (string hash in Directory.GetDirectories(npx))
                    {
                        dirs.Add(Path.Combine(hash, "node_modules", ".bin"));
                    }
                }
            }
            catch (Exception)
            {
            }
            return dirs;
        }

        public static string FindNode()
        {
            if (Settings.NodePath != null && File.Exists(Settings.NodePath)) return Settings.NodePath;
            foreach (string dir in CandidateDirs())
            {
                string candidate = Path.Combine(dir, "node.exe");
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }

        public static string FindDsh()
        {
            if (Settings.DshPath != null && File.Exists(Settings.DshPath)) return Settings.DshPath;
            string bundled = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "dsh", "node_modules", "@deepseek-ai", "dsh", "lib", "bin.js");
            if (File.Exists(bundled)) return bundled;
            foreach (string dir in CandidateDirs())
            {
                string candidate = Path.Combine(dir, "node_modules", "@deepseek-ai", "dsh", "lib", "bin.js");
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }
    }

    internal static class Net
    {
        public static bool PortOpen(int port)
        {
            try
            {
                TcpClient client = new TcpClient();
                try
                {
                    IAsyncResult result = client.BeginConnect(IPAddress.Loopback, port, null, null);
                    bool opened = result.AsyncWaitHandle.WaitOne(300);
                    if (opened && client.Connected)
                    {
                        client.EndConnect(result);
                        return true;
                    }
                    return false;
                }
                finally
                {
                    client.Close();
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    internal class Server
    {
        private static readonly object LogLock = new object();

        public Process Child;
        public int Pid;
        public string DshPath = "";
        public string NodePath = "";
        public bool Quitting;

        // Resolves both executables; recovery and start both need them.
        public void Resolve()
        {
            DshPath = Resolver.FindDsh() ?? "";
            NodePath = Resolver.FindNode() ?? "";
        }

        public string Start()
        {
            if (DshPath.Length == 0 || NodePath.Length == 0)
            {
                return "Cannot find dsh and/or node. Install them, set the dshPath/nodePath registry values "
                    + @"(HKCU\Software\DeepSeek Harness), or use --bundle-dsh at build time.";
            }
            if (Net.PortOpen(Settings.Port))
            {
                return "Port " + Settings.Port
                    + " is already in use by another process. Quit that process, or choose another port "
                    + "(registry value port or -port <n>).";
            }
            try
            {
                Directory.CreateDirectory(Settings.StatePath());
            }
            catch (Exception)
            {
            }

            Process process = new Process();
            process.StartInfo.FileName = NodePath;
            process.StartInfo.Arguments = "\"" + DshPath + "\" web";
            if (Settings.Port != 3080) process.StartInfo.Arguments += " --port " + Settings.Port;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WorkingDirectory =
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            string nodeDir = Path.GetDirectoryName(NodePath);
            process.StartInfo.EnvironmentVariables["PATH"] =
                (nodeDir == null ? "" : nodeDir + ";") + path;

            try
            {
                if (!process.Start()) return "Failed to start the server process.";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            Child = process;
            Pid = process.Id;
            process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null) AppendLog(e.Data);
            };
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null) AppendLog(e.Data);
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            WriteServerLock();
            return null;
        }

        private static void AppendLog(string line)
        {
            lock (LogLock)
            {
                try
                {
                    File.AppendAllText(Settings.ServerLogPath(), line + Environment.NewLine);
                }
                catch (Exception)
                {
                }
            }
        }

        public bool ChildAlive()
        {
            try
            {
                return Child != null && !Child.HasExited;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void WriteServerLock()
        {
            try
            {
                File.WriteAllText(Settings.ServerLockPath(),
                    Pid + " " + Settings.Port + Environment.NewLine);
            }
            catch (Exception)
            {
            }
        }

        public void RemoveServerLock()
        {
            try
            {
                if (File.Exists(Settings.ServerLockPath())) File.Delete(Settings.ServerLockPath());
            }
            catch (Exception)
            {
            }
        }

        public void WriteAppLock()
        {
            try
            {
                File.WriteAllText(Settings.AppLockPath(),
                    Process.GetCurrentProcess().Id + Environment.NewLine);
            }
            catch (Exception)
            {
            }
        }

        public void RemoveAppLock()
        {
            try
            {
                if (File.Exists(Settings.AppLockPath())) File.Delete(Settings.AppLockPath());
            }
            catch (Exception)
            {
            }
        }

        // Terminates the server's process tree and verifies the port is
        // released. Windows has no SIGTERM (Node's process.kill maps it to a
        // hard kill), so this is a tree kill via taskkill.
        public void Terminate()
        {
            if (Pid <= 0) return;
            Quitting = true;
            KillTree(Pid);
            int waited = 0;
            while (waited < 6000 && Net.PortOpen(Settings.Port))
            {
                Thread.Sleep(100);
                waited += 100;
            }
            if (Net.PortOpen(Settings.Port)) KillTree(Pid);
            RemoveServerLock();
            Pid = 0;
            Quitting = false;
        }

        private static void KillTree(int pid)
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = "taskkill";
                info.Arguments = "/PID " + pid + " /T /F";
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                Process killer = Process.Start(info);
                if (killer != null) killer.WaitForExit(5000);
            }
            catch (Exception)
            {
            }
        }

        // Recovers an orphaned server from a hard-killed previous instance:
        // only the exact pid recorded in the lock whose command line contains
        // the resolved dsh path is terminated, then the stale lock drops.
        public void RecoverStale()
        {
            if (!File.Exists(Settings.ServerLockPath()))
            {
                return;
            }
            int stalePid = 0;
            try
            {
                string content = File.ReadAllText(Settings.ServerLockPath());
                string[] parts = content.Split(' ');
                if (parts.Length >= 1) int.TryParse(parts[0], out stalePid);
            }
            catch (Exception)
            {
            }
            if (stalePid > 0 && DshPath.Length > 0)
            {
                try
                {
                    Process stale = Process.GetProcessById(stalePid);
                    string commandLine = CommandLine(stalePid);
                    if (commandLine != null
                        && commandLine.IndexOf(DshPath, StringComparison.OrdinalIgnoreCase) >= 0
                        && commandLine.IndexOf(" web", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        KillTree(stalePid);
                    }
                }
                catch (Exception)
                {
                    // The recorded pid is no longer alive.
                }
            }
            RemoveServerLock();
        }

        private static string CommandLine(int pid)
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + pid))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        object value = obj["CommandLine"];
                        if (value != null) return value.ToString();
                    }
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        public string LogTail(int count)
        {
            try
            {
                if (!File.Exists(Settings.ServerLogPath())) return "(no log yet)";
                string[] lines = File.ReadAllLines(Settings.ServerLogPath());
                int start = Math.Max(0, lines.Length - count);
                StringBuilder builder = new StringBuilder();
                for (int i = start; i < lines.Length; i++)
                {
                    builder.AppendLine(lines[i]);
                }
                return builder.ToString().TrimEnd();
            }
            catch (Exception)
            {
                return "(log unreadable)";
            }
        }
    }

    internal class MainForm : Form
    {
        private readonly Server server = new Server();
        private WebView2 web;
        private System.Windows.Forms.Timer lifeTimer;
        private Label statusLabel;
        private Label urlLabel;
        private Button openButton;
        private Button restartButton;
        private bool ready;
        private DateTime readyDeadline;
        private string lastFailure;

        public MainForm()
        {
            Text = "DeepSeek Harness";
            ClientSize = new Size(1100, 720);
            MinimumSize = new Size(640, 420);
            StartPosition = FormStartPosition.CenterScreen;

            // The embedded UI: the same page a browser would load at the
            // served URL. WebView2 runs on the Edge runtime installed with
            // Windows; the control reports a missing runtime through
            // CoreWebView2InitializationCompleted.
            web = new WebView2();
            web.Dock = DockStyle.Fill;
            web.CoreWebView2InitializationCompleted += delegate(object sender,
                CoreWebView2InitializationCompletedEventArgs e)
            {
                if (!e.IsSuccess)
                {
                    MessageBox.Show(this,
                        "The Microsoft Edge WebView2 runtime is missing. Install it from "
                        + "https://developer.microsoft.com/microsoft-edge/webview2/",
                        "WebView2 runtime required", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            Panel bar = new Panel();
            bar.Dock = DockStyle.Bottom;
            bar.Height = 36;

            statusLabel = new Label();
            statusLabel.Text = "Starting server…";
            statusLabel.AutoSize = true;
            statusLabel.Location = new Point(12, 10);
            bar.Controls.Add(statusLabel);

            urlLabel = new Label();
            urlLabel.Text = "http://127.0.0.1:" + Settings.Port;
            urlLabel.AutoSize = true;
            urlLabel.ForeColor = SystemColors.GrayText;
            urlLabel.Location = new Point(12, 26);
            bar.Controls.Add(urlLabel);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Right;
            buttons.WrapContents = false;
            buttons.Height = 36;
            buttons.Padding = new Padding(0, 5, 8, 0);

            openButton = new Button();
            openButton.Text = "Open in Browser";
            openButton.Width = 120;
            openButton.Enabled = false;
            openButton.Click += delegate { Browser.Open(Settings.Port); };

            restartButton = new Button();
            restartButton.Text = "Restart";
            restartButton.Width = 80;
            restartButton.Enabled = false;
            restartButton.Click += delegate { Restart(); };

            Button logsButton = new Button();
            logsButton.Text = "Open Logs";
            logsButton.Width = 90;
            logsButton.Click += delegate { OpenLogs(); };

            Button quitButton = new Button();
            quitButton.Text = "Quit";
            quitButton.Width = 74;
            quitButton.Click += delegate { Close(); };

            buttons.Controls.Add(openButton);
            buttons.Controls.Add(restartButton);
            buttons.Controls.Add(logsButton);
            buttons.Controls.Add(quitButton);
            bar.Controls.Add(buttons);

            Controls.Add(web);
            Controls.Add(bar);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Application.ApplicationExit += delegate { server.Terminate(); };
            server.WriteAppLock();
            server.Resolve();
            if (server.DshPath.Length == 0 || server.NodePath.Length == 0)
            {
                Fail("Cannot find dsh and/or node. Install them, set the dshPath/nodePath registry values "
                    + @"(HKCU\Software\DeepSeek Harness), or use --bundle-dsh at build time.");
                return;
            }
            server.RecoverStale();
            if (Settings.CheckUpdates)
            {
                BeginUpdateCheck();
            }
            else
            {
                StartServer();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (lifeTimer != null) lifeTimer.Stop();
            server.Terminate();
            server.RemoveAppLock();
            base.OnFormClosing(e);
        }

        private void LifeTick(object sender, EventArgs e)
        {
            if (server.Quitting) return;
            if (!server.ChildAlive())
            {
                lifeTimer.Stop();
                Fail("The server stopped (process exited).\n\n" + server.LogTail(30));
                return;
            }
            if (!ready)
            {
                if (Net.PortOpen(Settings.Port))
                {
                    ready = true;
                    statusLabel.Text = "Running";
                    urlLabel.Text = "http://127.0.0.1:" + Settings.Port;
                    openButton.Enabled = true;
                    restartButton.Enabled = true;
                    LoadWebView();
                    if (Settings.OpenBrowserOnLaunch) Browser.Open(Settings.Port);
                }
                else if (DateTime.Now > readyDeadline)
                {
                    lifeTimer.Stop();
                    Fail("The server did not start within 90 seconds.\n\n" + server.LogTail(30));
                }
            }
        }

        private void LoadWebView()
        {
            try
            {
                web.Source = new Uri("http://127.0.0.1:" + Settings.Port);
            }
            catch (Exception)
            {
            }
        }

        private void StartServer()
        {
            ready = false;
            statusLabel.Text = "Starting server…";
            urlLabel.Text = "http://127.0.0.1:" + Settings.Port;
            openButton.Enabled = false;
            restartButton.Enabled = false;
            readyDeadline = DateTime.Now.AddSeconds(90);
            string error = server.Start();
            if (error != null)
            {
                Fail(error);
                return;
            }
            if (lifeTimer == null)
            {
                lifeTimer = new System.Windows.Forms.Timer();
                lifeTimer.Interval = 300;
                lifeTimer.Tick += LifeTick;
            }
            lifeTimer.Start();
        }

        private void Restart()
        {
            server.Terminate();
            StartServer();
        }

        // Runs the version check on a background thread so the window appears
        // immediately; the server only starts after the check (and any
        // confirmed update) finishes.
        private void BeginUpdateCheck()
        {
            statusLabel.Text = "Checking for updates…";
            urlLabel.Text = "http://127.0.0.1:" + Settings.Port;
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                string current = VersionReader.Current(server.DshPath);
                string latest = "";
                string source = "";
                string error = UpdateChecker.Latest(out latest, out source);
                try { BeginInvoke(new Action(delegate { OnUpdateCheckDone(current, latest, source, error); })); }
                catch (Exception) { }
            });
        }

        private void OnUpdateCheckDone(string current, string latest, string source, string error)
        {
            if (IsDisposed) return;
            if (error != null)
            {
                Log("Update check failed: " + error);
                StartServer();
                return;
            }
            Log("Version check: current=" + (current.Length > 0 ? current : "<unknown>")
                + " latest=" + latest + " source=" + source);
            if (!string.IsNullOrEmpty(current) && Semver.Compare(latest, current) > 0)
            {
                DialogResult result = MessageBox.Show(this,
                    "A newer DeepSeek Harness is available.\n\nCurrent: " + current
                    + "\nNew: " + latest + " (via " + source + ")"
                    + "\n\nUpdate now? The web UI updates in place; this app itself stays installed.",
                    "Update available", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (result == DialogResult.Yes)
                {
                    RunUpdate(latest);
                    return;
                }
            }
            StartServer();
        }

        private void RunUpdate(string version)
        {
            UpdateDialog dialog = new UpdateDialog(version);
            dialog.Show(this);
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                StringWriter log = new StringWriter();
                string error = SelfUpdater.Update(version, log);
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        dialog.Close();
                        if (error != null)
                        {
                            Log("Update failed: " + error);
                            DialogResult result = MessageBox.Show(this,
                                error + "\n\nLog:\n" + Tail(log.ToString(), 20),
                                "Update failed", MessageBoxButtons.RetryCancel, MessageBoxIcon.Error);
                            if (result == DialogResult.Retry)
                            {
                                RunUpdate(version);
                                return;
                            }
                        }
                        else
                        {
                            statusLabel.Text = "Updated to " + version;
                            Log("Updated webui to " + version);
                        }
                        StartServer();
                    }));
                }
                catch (Exception) { }
            });
        }

        private static void Log(string line)
        {
            try
            {
                File.AppendAllText(Settings.ServerLogPath(), line + Environment.NewLine);
            }
            catch (Exception)
            {
            }
        }

        private static string Tail(string text, int count)
        {
            string[] lines = text.Split('\n');
            int start = Math.Max(0, lines.Length - count);
            StringBuilder builder = new StringBuilder();
            for (int i = start; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length > 0) builder.AppendLine(line);
            }
            return builder.ToString().TrimEnd();
        }

        private void Fail(string message)
        {
            statusLabel.Text = "Stopped";
            urlLabel.Text = "";
            openButton.Enabled = false;
            restartButton.Enabled = true;
            if (message == lastFailure) return;
            lastFailure = message;
            DialogResult result = MessageBox.Show(this, message,
                "DeepSeek Harness could not start the server",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Error);
            if (result == DialogResult.Yes)
            {
                Restart();
            }
            else if (result == DialogResult.No)
            {
                OpenLogs();
            }
            else
            {
                Close();
            }
        }

        private static void OpenLogs()
        {
            try
            {
                Directory.CreateDirectory(Settings.StatePath());
                Process.Start(Settings.StatePath());
            }
            catch (Exception)
            {
            }
        }
    }

    internal static class Browser
    {
        public static void Open(int port)
        {
            try
            {
                Process.Start("http://127.0.0.1:" + port);
            }
            catch (Exception)
            {
            }
        }
    }

    // Build-time constants. Program.cs declares defaults so the file compiles
    // on its own; build.ps1 generates BuildInfo.g.cs (same partial class, a
    // static constructor overriding these) so the shipped exe carries the exact
    // version and default update repositories.
    internal static partial class BuildInfo
    {
        public static string Version = "0.0.0";
        public static string DefaultUpdateRepos = "deepseek-ai/deepseek-harness";
    }

    // Minimal semantic-version comparison for version checks. Tags arrive as
    // `dsh-v0.2.0` or `v0.2.0` and versions as `0.1.0-rc.5`.
    internal static class Semver
    {
        public static string Normalize(string value)
        {
            if (value == null) return "";
            string v = value.Trim();
            if (v.StartsWith("dsh-v", StringComparison.OrdinalIgnoreCase)) v = v.Substring(5);
            else if (v.StartsWith("v", StringComparison.OrdinalIgnoreCase)) v = v.Substring(1);
            return v;
        }

        public static int Compare(string a, string b)
        {
            a = Normalize(a);
            b = Normalize(b);
            string aCore = a, aPre = "", bCore = b, bPre = "";
            int idx = a.IndexOf('-');
            if (idx >= 0) { aCore = a.Substring(0, idx); aPre = a.Substring(idx + 1); }
            idx = aCore.IndexOf('+');
            if (idx >= 0) aCore = aCore.Substring(0, idx);
            idx = b.IndexOf('-');
            if (idx >= 0) { bCore = b.Substring(0, idx); bPre = b.Substring(idx + 1); }
            idx = bCore.IndexOf('+');
            if (idx >= 0) bCore = bCore.Substring(0, idx);

            string[] pa = aCore.Split('.');
            string[] pb = bCore.Split('.');
            int count = Math.Max(pa.Length, pb.Length);
            for (int i = 0; i < count; i++)
            {
                int va = i < pa.Length ? ParseNum(pa[i]) : 0;
                int vb = i < pb.Length ? ParseNum(pb[i]) : 0;
                if (va != vb) return va < vb ? -1 : 1;
            }

            bool aHasPre = aPre.Length > 0;
            bool bHasPre = bPre.Length > 0;
            if (!aHasPre && bHasPre) return 1;
            if (aHasPre && !bHasPre) return -1;
            if (!aHasPre && !bHasPre) return 0;
            return ComparePrerelease(aPre, bPre);
        }

        private static int ParseNum(string value)
        {
            int result;
            return int.TryParse(value, out result) ? result : 0;
        }

        private static int ComparePrerelease(string a, string b)
        {
            string[] pa = a.Split('.');
            string[] pb = b.Split('.');
            int count = Math.Max(pa.Length, pb.Length);
            for (int i = 0; i < count; i++)
            {
                if (i >= pa.Length) return -1;
                if (i >= pb.Length) return 1;
                string x = pa[i], y = pb[i];
                int vx, vy;
                bool nx = int.TryParse(x, out vx);
                bool ny = int.TryParse(y, out vy);
                if (nx && ny)
                {
                    if (vx != vy) return vx < vy ? -1 : 1;
                }
                else if (nx)
                {
                    return -1;
                }
                else if (ny)
                {
                    return 1;
                }
                else
                {
                    int c = string.CompareOrdinal(x, y);
                    if (c != 0) return c < 0 ? -1 : 1;
                }
            }
            return 0;
        }
    }

    // A single short GET request with a bounded timeout. TLS 1.2 is set
    // explicitly because the .NET Framework default (1.0/1.1) is rejected by
    // GitHub and npm.
    internal static class Http
    {
        public static string Get(string url, int timeoutSeconds)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Timeout = timeoutSeconds * 1000;
                request.UserAgent = "DeepSeek-Harness-App/" + BuildInfo.Version;
                request.Accept = "application/json";
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    // Reads the currently installed webui version from the resolved dsh's
    // package.json (the file one directory above lib/bin.js), which works for
    // both a bundled dsh\ and a global npm install.
    internal static class VersionReader
    {
        public static string Current(string dshPath)
        {
            if (string.IsNullOrEmpty(dshPath)) return "";
            string dir = Path.GetDirectoryName(dshPath);
            if (dir == null) return "";
            string pkg = Path.GetFullPath(Path.Combine(dir, "..", "package.json"));
            if (!File.Exists(pkg)) return "";
            try
            {
                string json = File.ReadAllText(pkg);
                Match match = Regex.Match(json, "\"version\"\\s*:\\s*\"([^\"]+)\"");
                if (match.Success) return Semver.Normalize(match.Groups[1].Value);
            }
            catch (Exception)
            {
            }
            return "";
        }
    }

    // Finds the newest published version across the configured GitHub repos and
    // (optionally) npm. Returns null on success, or an error message.
    internal static class UpdateChecker
    {
        public static string Latest(out string version, out string source)
        {
            version = "";
            source = "";
            string best = "";
            string bestSource = "";

            foreach (string repo in Repos())
            {
                string json = Http.Get("https://api.github.com/repos/" + repo + "/releases/latest", 10);
                string tag = json == null ? null : JsonField(json, "tag_name");
                if (string.IsNullOrEmpty(tag)) continue;
                string v = Semver.Normalize(tag);
                if (v.Length == 0) continue;
                if (best.Length == 0 || Semver.Compare(v, best) > 0)
                {
                    best = v;
                    bestSource = "GitHub " + repo;
                }
            }

            if (Settings.CheckNpm)
            {
                string json = Http.Get("https://registry.npmjs.org/@deepseek-ai/dsh/latest", 10);
                string v = json == null ? null : JsonField(json, "version");
                if (!string.IsNullOrEmpty(v))
                {
                    string normalized = Semver.Normalize(v);
                    if (normalized.Length > 0 && (best.Length == 0 || Semver.Compare(normalized, best) > 0))
                    {
                        best = normalized;
                        bestSource = "npm @deepseek-ai/dsh";
                    }
                }
            }

            if (best.Length == 0) return "No version could be retrieved from GitHub or npm.";
            version = best;
            source = bestSource;
            return null;
        }

        private static List<string> Repos()
        {
            List<string> list = new List<string>();
            string raw = Settings.UpdateRepos;
            if (string.IsNullOrEmpty(raw)) raw = BuildInfo.DefaultUpdateRepos;
            foreach (string item in raw.Split(new char[] { ';', ',' }))
            {
                string trimmed = item.Trim();
                if (trimmed.Length > 0 && !list.Contains(trimmed)) list.Add(trimmed);
            }
            return list;
        }

        private static string JsonField(string json, string field)
        {
            Match match = Regex.Match(json, "\"" + Regex.Escape(field) + "\"\\s*:\\s*\"([^\"]*)\"");
            return match.Success ? match.Groups[1].Value : null;
        }
    }

    // Reinstalls @deepseek-ai/dsh at a target version using the resolved node's
    // npm. A bundled dsh\ folder is swapped atomically (staging -> rename), so
    // the running exe never replaces itself and future webui updates only touch
    // that folder; without a bundled install the global package is refreshed.
    internal static class SelfUpdater
    {
        public static string Update(string version, TextWriter log)
        {
            string nodePath = Resolver.FindNode();
            if (nodePath == null) return "Cannot find node.exe to run the update.";
            string npmCli = FindNpmCli(nodePath);
            if (npmCli == null) return "Cannot find npm (npm-cli.js) beside node to run the update.";

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string bundled = Path.Combine(baseDir, "dsh", "node_modules", "@deepseek-ai", "dsh", "lib", "bin.js");
            bool bundledInstall = File.Exists(bundled);

            if (bundledInstall)
            {
                string dshDir = Path.Combine(baseDir, "dsh");
                string staging = Path.Combine(baseDir, "dsh.new." + Process.GetCurrentProcess().Id);
                string backup = Path.Combine(baseDir, "dsh.old." + Process.GetCurrentProcess().Id);
                try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch (Exception) { }
                try { if (Directory.Exists(backup)) Directory.Delete(backup, true); } catch (Exception) { }
                Directory.CreateDirectory(staging);
                int code = RunNpm(nodePath, npmCli,
                    "install --prefix \"" + staging + "\" @deepseek-ai/dsh@" + version + " --no-audit --no-fund --no-progress",
                    log);
                if (code != 0)
                {
                    try { Directory.Delete(staging, true); } catch (Exception) { }
                    return "npm install failed (exit code " + code + ").";
                }
                try
                {
                    Directory.Move(dshDir, backup);
                    Directory.Move(staging, dshDir);
                }
                catch (Exception ex)
                {
                    try { if (Directory.Exists(staging)) Directory.Move(staging, dshDir); } catch (Exception) { }
                    return "Could not replace the dsh folder: " + ex.Message;
                }
                try { Directory.Delete(backup, true); } catch (Exception) { }
                return null;
            }

            int globalCode = RunNpm(nodePath, npmCli,
                "install -g @deepseek-ai/dsh@" + version + " --no-audit --no-fund", log);
            if (globalCode != 0) return "npm install -g failed (exit code " + globalCode + ").";
            return null;
        }

        private static int RunNpm(string nodePath, string npmCli, string args, TextWriter log)
        {
            try
            {
                Process process = new Process();
                process.StartInfo.FileName = nodePath;
                process.StartInfo.Arguments = "\"" + npmCli + "\" " + args;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null && log != null) log.WriteLine(e.Data);
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null && log != null) log.WriteLine(e.Data);
                };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                return process.ExitCode;
            }
            catch (Exception ex)
            {
                if (log != null) log.WriteLine("npm error: " + ex.Message);
                return -1;
            }
        }

        private static string FindNpmCli(string nodePath)
        {
            string dir = Path.GetDirectoryName(nodePath);
            if (dir == null) return null;
            string direct = Path.Combine(dir, "node_modules", "npm", "bin", "npm-cli.js");
            if (File.Exists(direct)) return direct;
            string sibling = Path.Combine(dir, "..", "node_modules", "npm", "bin", "npm-cli.js");
            if (File.Exists(sibling)) return sibling;
            return null;
        }
    }

    // Non-modal progress window shown while the self-update runs on a
    // background thread, so the message pump stays alive.
    internal class UpdateDialog : Form
    {
        public UpdateDialog(string version)
        {
            Text = "Updating DeepSeek Harness";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(430, 110);
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            Label label = new Label();
            label.Text = "Downloading DeepSeek Harness " + version + "…\nThis can take a minute.";
            label.Location = new Point(16, 18);
            label.AutoSize = true;
            Controls.Add(label);
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Settings.Load();
            if (Settings.HasArg("--resolve"))
            {
                string dsh = Resolver.FindDsh() ?? "<not found>";
                string node = Resolver.FindNode() ?? "<not found>";
                Console.WriteLine("dsh=" + dsh);
                Console.WriteLine("node=" + node);
                Console.WriteLine("port=" + Settings.Port);
                Console.WriteLine("portOpen=" + Net.PortOpen(Settings.Port).ToString().ToLowerInvariant());
                return;
            }

            // Headless diagnostics for the update flow: report what a startup
            // check would see, or run a self-update, then exit without a UI.
            if (Settings.HasArg("--check-update"))
            {
                string current = VersionReader.Current(Resolver.FindDsh() ?? "");
                string latest = "";
                string source = "";
                string error = UpdateChecker.Latest(out latest, out source);
                Console.WriteLine("current=" + (current.Length > 0 ? current : "<unknown>"));
                Console.WriteLine("latest=" + (latest.Length > 0 ? latest : "<none>"));
                Console.WriteLine("source=" + (source.Length > 0 ? source : "<none>"));
                if (error != null) Console.WriteLine("error=" + error);
                return;
            }
            if (Settings.HasArg("--update"))
            {
                string version = "";
                string[] argv = Environment.GetCommandLineArgs();
                for (int i = 1; i < argv.Length; i++)
                {
                    if (argv[i] == "--update" && i + 1 < argv.Length) version = argv[i + 1];
                }
                if (version.Length == 0)
                {
                    string source = "";
                    string error = UpdateChecker.Latest(out version, out source);
                    if (error != null)
                    {
                        Console.WriteLine("error=" + error);
                        return;
                    }
                }
                Console.WriteLine("updating to " + version + "…");
                string updateError = SelfUpdater.Update(version, Console.Out);
                if (updateError != null)
                {
                    Console.WriteLine("error=" + updateError);
                    return;
                }
                Console.WriteLine("updated=" + version);
                return;
            }

            bool createdNew = false;
            Mutex mutex = null;
            if (Settings.SingleInstance)
            {
                mutex = new Mutex(true, Settings.BundleId + ".single", out createdNew);
                if (!createdNew)
                {
                    MessageBox.Show("DeepSeek Harness is already running.");
                    return;
                }
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            if (mutex != null) mutex.ReleaseMutex();
        }
    }
}
