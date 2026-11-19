using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace OCEditor.Launcher;

internal sealed class MainForm : Form
{
    private const string AppHost = "app.local";
    private const string AppUrl = "https://app.local/index.html";

    private const int WM_NCHITTEST = 0x0084;
    private const int HTCLIENT = 1;
    private const int HTCAPTION = 2;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;
    private const int WM_NCLBUTTONDOWN = 0x00A1;

    private const int ERROR_INSUFFICIENT_BUFFER = 122;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFamilyName(ref int packageFamilyNameLength, StringBuilder? packageFamilyName);

    private static string? GetPackageFamilyName()
    {
        try
        {
            var length = 0;
            var result = GetCurrentPackageFamilyName(ref length, null);
            if (result != ERROR_INSUFFICIENT_BUFFER || length <= 1) return null;
            var buffer = new StringBuilder(length);
            result = GetCurrentPackageFamilyName(ref length, buffer);
            return result == 0 ? buffer.ToString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveUserDataFolder()
    {
        var explicitFolder = Environment.GetEnvironmentVariable("OC_EDITOR_USER_DATA")
            ?? Environment.GetEnvironmentVariable("OC_STUDIO_USER_DATA");
        if (!string.IsNullOrWhiteSpace(explicitFolder)) return explicitFolder;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var familyName = GetPackageFamilyName();
        if (!string.IsNullOrEmpty(familyName))
        {
            return Path.Combine(localAppData, "Packages", familyName, "LocalState", "OC editor", "WebView2");
        }
        return Path.Combine(localAppData, "OC editor", "WebView2");
    }

    private static void CopyDirectory(string source, string target)
    {
        var sourceRoot = new DirectoryInfo(source).FullName;
        Directory.CreateDirectory(target);
        foreach (var dir in Directory.GetDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(dir.Replace(sourceRoot, target));
        }
        foreach (var file in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var destination = file.Replace(sourceRoot, target);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    private static void MigrateLegacyDataIfNeeded(string userDataFolder)
    {
        try
        {
            if (Directory.Exists(userDataFolder) && Directory.EnumerateFileSystemEntries(userDataFolder).Any()) return;

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var sources = new List<string>
            {
                Path.Combine(localAppData, "OC editor", "WebView2"),
                Path.Combine(localAppData, "OC Studio", "WebView2"),
            };
            var familyName = GetPackageFamilyName();
            if (!string.IsNullOrEmpty(familyName))
            {
                sources.Add(Path.Combine(localAppData, "Packages", familyName, "LocalCache", "Local", "OC editor", "WebView2"));
            }

            foreach (var source in sources)
            {
                if (!Directory.Exists(source)) continue;
                if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(userDataFolder), StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    LauncherLog.Write($"migrating data from {source} to {userDataFolder}");
                    CopyDirectory(source, userDataFolder);
                    LauncherLog.Write("data migration finished");
                    return;
                }
                catch (Exception error)
                {
                    LauncherLog.Write($"data migration from {source} failed: {error}");
                }
            }
        }
        catch (Exception error)
        {
            LauncherLog.Write($"data migration check failed: {error}");
        }
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private readonly WebView2 _webView = new();
    private readonly Label _loading = new();
    private CoreWebView2Environment? _environment;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Windows 11 DWM 圆角；Windows 10 会安全地忽略。
        var preference = 2; // DWMWCP_ROUND
        _ = DwmSetWindowAttribute(Handle, 33, ref preference, sizeof(int));
    }

    public MainForm()
    {
        Text = "OC editor";
        var workArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Width = Math.Clamp((int)(workArea.Width * 0.92), 1100, Math.Max(1100, workArea.Width - 24));
        Height = Math.Clamp((int)(workArea.Height * 0.92), 700, Math.Max(700, workArea.Height - 24));
        MinimumSize = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        Padding = new Padding(1);
        BackColor = Color.FromArgb(28, 27, 26);
        ForeColor = Color.FromArgb(227, 223, 216);
        DoubleBuffered = true;

        var iconPath = Path.Combine(AppContext.BaseDirectory, "oceditor.ico");
        if (File.Exists(iconPath))
        {
            Icon = new Icon(iconPath);
        }

        _webView.Dock = DockStyle.Fill;
        _webView.DefaultBackgroundColor = Color.FromArgb(28, 27, 26);
        _webView.AllowExternalDrop = false;
        _webView.Visible = false;

        _loading.Dock = DockStyle.Fill;
        _loading.TextAlign = ContentAlignment.MiddleCenter;
        _loading.Text = "正在启动 OC editor…";
        _loading.Font = new Font("Segoe UI", 12f);
        _loading.BackColor = Color.FromArgb(28, 27, 26);
        _loading.ForeColor = Color.FromArgb(165, 160, 152);

        Controls.Add(_webView);
        Controls.Add(_loading);

        Shown += OnShown;
        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Normal)
            {
                MaximizedBounds = Screen.FromControl(this).WorkingArea;
            }
            PostWindowState();
        };
        FormClosed += (_, _) =>
        {
            _webView.Dispose();
        };
    }

    private async void OnShown(object? sender, EventArgs e)
    {
        try
        {
            LauncherLog.Write("MainForm shown");
            MaximizedBounds = Screen.FromControl(this).WorkingArea;
            var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            if (!File.Exists(Path.Combine(wwwroot, "index.html")))
            {
                ShowFatal("静态资源缺失", $"找不到 wwwroot/index.html：\n{wwwroot}");
                return;
            }

            // WebView2 会把 index.html 缓存在用户数据目录里；安装新版本后同一个 URL
            // 可能仍然加载旧 chunk。用 index.html 的写入时间做 cache-bust。
            var appUrl = $"{AppUrl}?v={File.GetLastWriteTimeUtc(Path.Combine(wwwroot, "index.html")).Ticks}";

            var userDataFolder = ResolveUserDataFolder();
            LauncherLog.Write($"user data folder: {userDataFolder} (packaged={GetPackageFamilyName() is not null})");

            // MSIX/Store 版必须使用包自己的 LocalState；%LOCALAPPDATA%\OC editor
            // 会被文件虚拟化，WebView2 子进程看不到同一个目录。
            MigrateLegacyDataIfNeeded(userDataFolder);
            Directory.CreateDirectory(userDataFolder);

            var options = new CoreWebView2EnvironmentOptions
            {
                AreBrowserExtensionsEnabled = false,
                EnableTrackingPrevention = true,
            };

            LauncherLog.Write("creating WebView2 environment");
            _environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
            LauncherLog.Write("environment ready; ensuring core");
            await _webView.EnsureCoreWebView2Async(_environment);
            LauncherLog.Write("core ready");

            var core = _webView.CoreWebView2;
            LauncherLog.Write($"webview2 runtime {_environment?.BrowserVersionString ?? "unknown"}");
            core.SetVirtualHostNameToFolderMapping(
                AppHost,
                wwwroot,
                CoreWebView2HostResourceAccessKind.Allow);

            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreBrowserAcceleratorKeysEnabled = true;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.Settings.IsWebMessageEnabled = true;

            core.WebMessageReceived += (_, args) => HandleWindowMessage(args.WebMessageAsJson);

            // 离线硬约束：除本地虚拟主机和 WebView 内部 scheme 外，一律拒绝。
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (_, args) =>
            {
                if (IsAllowedUri(args.Request.Uri)) return;
                args.Response = _environment!.CreateWebResourceResponse(
                    Stream.Null,
                    403,
                    "Blocked by OC editor offline policy",
                    "Content-Type: text/plain; charset=utf-8");
            };

            core.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                if (Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri) &&
                    uri.Scheme == Uri.UriSchemeHttps &&
                    string.Equals(uri.Host, AppHost, StringComparison.OrdinalIgnoreCase))
                {
                    _webView.Source = uri;
                }
            };

            core.NavigationCompleted += (_, args) =>
            {
                LauncherLog.Write($"navigation completed: success={args.IsSuccess} status={args.WebErrorStatus}");
                _loading.Visible = false;
                _webView.Visible = true;
                _webView.BringToFront();
                if (!args.IsSuccess)
                {
                    MessageBox.Show(
                        this,
                        $"页面加载失败：{args.WebErrorStatus}\n\n请确认 out 静态资源完整。",
                        "OC editor",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            };

            core.ProcessFailed += (_, args) =>
            {
                MessageBox.Show(
                    this,
                    $"WebView2 渲染进程异常：{args.ProcessFailedKind}",
                    "OC editor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            };

            core.DownloadStarting += (_, args) =>
            {
                var suggested = Path.GetFileName(args.ResultFilePath);
                if (string.IsNullOrWhiteSpace(suggested)) suggested = "oc-editor-backup.zip";
                using var dialog = new SaveFileDialog
                {
                    Title = "保存文件",
                    FileName = suggested,
                    Filter = "OC editor 文件 (*.zip;*.json;*.ocbak;*.ocmap;*.png;*.jpg;*.jpeg)|*.zip;*.json;*.ocbak;*.ocmap;*.png;*.jpg;*.jpeg|所有文件 (*.*)|*.*",
                    AddExtension = true,
                    OverwritePrompt = true,
                };
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    args.ResultFilePath = dialog.FileName;
                }
                else
                {
                    args.Cancel = true;
                }
            };

            LauncherLog.Write($"navigating to {appUrl}");
            _webView.Source = new Uri(appUrl);
            PostWindowState();
        }
        catch (WebView2RuntimeNotFoundException ex)
        {
            LauncherLog.Write($"WebView2 runtime missing: {ex.Message}");
            ShowWebView2Missing();
        }
        catch (COMException ex) when ((uint)ex.HResult == 0x800700AA)
        {
            LauncherLog.Write($"WebView2 user data folder busy: {ex}");
            ShowFatal(
                "WebView2 数据目录被占用",
                "另一个 OC editor 窗口或残留的 WebView2 进程正在使用数据目录。\n\n" +
                "请关闭所有 OC editor 窗口后重试；如果仍然失败，请重启电脑。\n\n" +
                $"系统信息：{ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            LauncherLog.Write($"WebView2 user data folder inaccessible: {ex}");
            ShowFatal(
                "无法访问用户数据目录",
                "OC editor 无法写入以下目录：\n" +
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OC editor",
                    "WebView2") +
                "\n\n请检查文件夹权限、磁盘空间，或以当前用户身份重新登录后重试。");
        }
        catch (Exception ex)
        {
            LauncherLog.Write($"fatal in MainForm: {ex}");
            ShowFatal("启动失败", ex.ToString());
        }
    }

    private void ShowWebView2Missing()
    {
        var answer = MessageBox.Show(
            "OC editor 需要 Microsoft Edge WebView2 Runtime。\n\n" +
            "Windows 11 通常已内置；Windows 10 可能需要单独安装。\n" +
            "是否打开微软官方下载页面？（安装完成后重新打开 OC editor 即可）",
            "缺少 WebView2 Runtime",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://go.microsoft.com/fwlink/p/?LinkId=2124703",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            LauncherLog.Write($"open WebView2 download page failed: {ex.Message}");
        }
    }

    private void HandleWindowMessage(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var type) ||
                type.GetString() != "oc-window" ||
                !root.TryGetProperty("action", out var actionElement))
            {
                return;
            }

            switch (actionElement.GetString())
            {
                case "drag":
                    ReleaseCapture();
                    SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
                    break;
                case "minimize":
                    WindowState = FormWindowState.Minimized;
                    break;
                case "toggleMaximize":
                    WindowState = WindowState == FormWindowState.Maximized
                        ? FormWindowState.Normal
                        : FormWindowState.Maximized;
                    PostWindowState();
                    break;
                case "close":
                    Close();
                    break;
                case "state":
                    PostWindowState();
                    break;
            }
        }
        catch (Exception ex)
        {
            LauncherLog.Write($"window message error: {ex.Message}");
        }
    }

    private void PostWindowState()
    {
        try
        {
            var core = _webView.CoreWebView2;
            if (core is null) return;
            var maximized = WindowState == FormWindowState.Maximized ? "true" : "false";
            core.PostWebMessageAsJson(
                $"{{\"type\":\"oc-window-state\",\"maximized\":{maximized}}}");
        }
        catch
        {
            // 初始化早期 CoreWebView2 尚不可用。
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
        {
            base.WndProc(ref m);
            if ((int)m.Result == HTCLIENT)
            {
                var screenX = (short)((long)m.LParam & 0xFFFF);
                var screenY = (short)(((long)m.LParam >> 16) & 0xFFFF);
                var point = PointToClient(new Point(screenX, screenY));
                const int grip = 7;
                var left = point.X <= grip;
                var right = point.X >= ClientSize.Width - grip;
                var top = point.Y <= grip;
                var bottom = point.Y >= ClientSize.Height - grip;

                if (top && left) m.Result = (IntPtr)HTTOPLEFT;
                else if (top && right) m.Result = (IntPtr)HTTOPRIGHT;
                else if (bottom && left) m.Result = (IntPtr)HTBOTTOMLEFT;
                else if (bottom && right) m.Result = (IntPtr)HTBOTTOMRIGHT;
                else if (left) m.Result = (IntPtr)HTLEFT;
                else if (right) m.Result = (IntPtr)HTRIGHT;
                else if (top) m.Result = (IntPtr)HTTOP;
                else if (bottom) m.Result = (IntPtr)HTBOTTOM;
            }
            return;
        }

        base.WndProc(ref m);
    }

    private static bool IsAllowedUri(string uriText)
    {
        if (string.IsNullOrWhiteSpace(uriText)) return false;
        if (!Uri.TryCreate(uriText, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme is "data" or "blob" or "about" or "devtools") return true;
        return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.Host, AppHost, StringComparison.OrdinalIgnoreCase);
    }

    private void ShowFatal(string title, string message)
    {
        _loading.Visible = false;
        MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        Close();
    }
}
