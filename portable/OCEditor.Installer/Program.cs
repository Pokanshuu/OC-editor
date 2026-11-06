using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Win32;

namespace OCEditor.Installer;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            // Avoid EnableVisualStyles' activation-context temp extraction: in
            // some locked-down Windows profiles that file cannot be read.
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Log("started");

            if (args.Any((arg) => string.Equals(arg, "--uninstall", StringComparison.OrdinalIgnoreCase)))
            {
                Uninstall();
                return;
            }

            var installIndex = Array.FindIndex(
                args,
                (arg) => string.Equals(arg, "--install", StringComparison.OrdinalIgnoreCase));
            var silent = args.Any((arg) => string.Equals(arg, "--silent", StringComparison.OrdinalIgnoreCase));
            var form = new InstallerForm(silent);
            if (installIndex >= 0 && installIndex + 1 < args.Length)
            {
                form.StartInstall(args[installIndex + 1]);
            }
            Log("form created");
            Application.Run(form);
        }
        catch (Exception ex)
        {
            Log(ex.ToString());
            MessageBox.Show(ex.ToString(), "OC editor 安装程序", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    internal static void Log(string message)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(Path.GetTempPath(), "oceditor-setup.log"),
                $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // ignore
        }
    }

    private static void Uninstall()
    {
        var dir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var answer = MessageBox.Show(
            $"确定卸载 OC editor 吗？\n\n安装位置：{dir}\n\n个人创作数据保存在浏览器本地目录中，不会被删除。",
            "卸载 OC editor",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        TryDelete(Path.Combine(desktop, "OC editor.lnk"));
        // 0.18.1 及更早版本的快捷方式名仍是 OC Studio。
        TryDelete(Path.Combine(desktop, "OC Studio.lnk"));

        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        foreach (var folder in new[] { "OC editor", "OC Studio" })
        {
            var menuDir = Path.Combine(programs, folder);
            TryDeleteDirectory(menuDir);
        }

        foreach (var product in new[] { "OCEditor", "OCStudio" })
        {
            Registry.CurrentUser.DeleteSubKeyTree(
                $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{product}",
                throwOnMissingSubKey: false);
        }

        // The running executable cannot remove itself; let cmd finish after exit.
        var command = $"timeout /t 2 >nul & rmdir /s /q \"{dir}\"";
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c {command}")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden,
        });
        Application.Exit();
    }

    internal static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // best effort
        }
    }

    internal static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch
        {
            // best effort
        }
    }
}

internal sealed class InstallerForm : Form
{
    private const string PayloadResource = "OCEditor.payload.zip";

    private readonly TextBox _pathBox = new();
    private readonly CheckBox _desktopShortcut = new();
    private readonly Button _installButton = new();
    private readonly Button _browseButton = new();
    private readonly Button _uninstallButton = new();
    private readonly ProgressBar _progress = new();
    private readonly Label _status = new();
    private readonly bool _silent;

    public InstallerForm(bool silent = false)
    {
        _silent = silent;
        Shown += (_, _) => { if (_silent) Hide(); };
        Text = "OC editor 安装程序";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(560, 300);
        BackColor = Color.FromArgb(247, 244, 239);
        Font = new Font("Segoe UI", 9f);
        Padding = new Padding(24);

        var title = new Label
        {
            Text = "OC editor",
            Font = new Font("Segoe UI", 20f, FontStyle.Bold),
            ForeColor = Color.FromArgb(45, 42, 38),
            AutoSize = true,
            Location = new Point(28, 24),
        };

        var subtitle = new Label
        {
            Text = "本地优先的原创角色与世界观创作工作台",
            ForeColor = Color.FromArgb(125, 118, 108),
            AutoSize = true,
            Location = new Point(31, 62),
        };

        var pathLabel = new Label
        {
            Text = "安装位置",
            ForeColor = Color.FromArgb(70, 65, 58),
            AutoSize = true,
            Location = new Point(31, 105),
        };

        _pathBox.Location = new Point(31, 128);
        _pathBox.Size = new Size(410, 27);
        _pathBox.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "OC editor");

        _browseButton.Text = "更改…";
        _browseButton.Location = new Point(449, 127);
        _browseButton.Size = new Size(80, 29);
        _browseButton.Click += BrowseClicked;

        _desktopShortcut.Text = "在桌面创建快捷方式";
        _desktopShortcut.Checked = true;
        _desktopShortcut.AutoSize = true;
        _desktopShortcut.Location = new Point(31, 168);
        _desktopShortcut.ForeColor = Color.FromArgb(70, 65, 58);

        _progress.Location = new Point(31, 204);
        _progress.Size = new Size(498, 8);
        _progress.Style = ProgressBarStyle.Continuous;

        _status.Text = "准备安装";
        _status.ForeColor = Color.FromArgb(125, 118, 108);
        _status.AutoSize = true;
        _status.Location = new Point(31, 220);

        _installButton.Text = "安装";
        _installButton.Location = new Point(397, 248);
        _installButton.Size = new Size(132, 34);
        _installButton.BackColor = Color.FromArgb(82, 72, 62);
        _installButton.ForeColor = Color.White;
        _installButton.FlatStyle = FlatStyle.Flat;
        _installButton.FlatAppearance.BorderSize = 0;
        _installButton.Click += InstallClicked;

        _uninstallButton.Text = "卸载已安装版本";
        _uninstallButton.Location = new Point(31, 248);
        _uninstallButton.Size = new Size(150, 34);
        _uninstallButton.FlatStyle = FlatStyle.Flat;
        _uninstallButton.FlatAppearance.BorderColor = Color.FromArgb(196, 186, 174);
        _uninstallButton.ForeColor = Color.FromArgb(96, 88, 78);
        _uninstallButton.Click += UninstallClicked;

        Controls.AddRange(new Control[]
        {
            title,
            subtitle,
            pathLabel,
            _pathBox,
            _browseButton,
            _desktopShortcut,
            _progress,
            _status,
            _installButton,
            _uninstallButton,
        });
    }

    public void StartInstall(string target)
    {
        _pathBox.Text = target;
        // 命令行安装（--install）发生在 Application.Run 之前，
        // BeginInvoke 需要窗口句柄，这里先强制创建，否则静默安装会直接抛异常。
        _ = Handle;
        BeginInvoke(() => InstallClicked(this, EventArgs.Empty));
    }

    private void BrowseClicked(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择 OC editor 的安装位置",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_pathBox.Text)
                ? _pathBox.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ShowNewFolderButton = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _pathBox.Text = Path.Combine(dialog.SelectedPath, "OC editor");
        }
    }

    /// <summary>
    /// 覆盖安装前先清掉旧版：同目录直接删程序文件，别的目录（例如旧版 OC Studio）连快捷方式、
    /// 注册表卸载项一起清掉；个人数据不在安装目录里，不会被删。
    /// </summary>
    private static void RemoveInstalledVersions(string? keepTarget)
    {
        foreach (var product in new[] { "OCEditor", "OCStudio" })
        {
            string? location = null;
            using (var key = Registry.CurrentUser.OpenSubKey(
                       $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{product}"))
            {
                location = key?.GetValue("InstallLocation") as string;
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                try
                {
                    Program.TryDeleteDirectory(location);
                    Program.Log($"removed previous install: {location}");
                }
                catch (Exception ex)
                {
                    Program.Log($"remove previous failed ({location}): {ex.Message}");
                }
            }

            if (string.Equals(keepTarget, location, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(
                    $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{product}",
                    throwOnMissingSubKey: false);
            }
            catch (Exception ex)
            {
                Program.Log($"remove uninstall key failed ({product}): {ex.Message}");
            }
        }

        RemoveShortcuts();
    }

    private static void RemoveShortcuts()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        foreach (var name in new[] { "OC editor.lnk", "OC Studio.lnk" })
        {
            Program.TryDelete(Path.Combine(desktop, name));
        }

        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        foreach (var folder in new[] { "OC editor", "OC Studio" })
        {
            Program.TryDeleteDirectory(Path.Combine(programs, folder));
        }
    }

    private static void StopRunningApp()
    {
        foreach (var name in new[] { "OCeditor", "OCStudio" })
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
                catch
                {
                    // 关闭失败就交给后面的删除逻辑重试
                }
            }
        }
    }

    private void UninstallClicked(object? sender, EventArgs e)
    {
        var answer = MessageBox.Show(
            this,
            "确定卸载已安装的 OC editor 吗？\n\n" +
            "只会删除程序、快捷方式和卸载项；\n" +
            "个人创作数据（%LOCALAPPDATA%\\OC editor\\WebView2）会保留。",
            "卸载 OC editor",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;

        try
        {
            _uninstallButton.Enabled = false;
            _status.Text = "正在卸载…";
            StopRunningApp();
            RemoveInstalledVersions(null);
            _status.Text = "已卸载";
            MessageBox.Show(this, "已卸载 OC editor，个人数据已保留。", "OC editor",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Program.Log($"uninstall failed: {ex}");
            MessageBox.Show(this, $"卸载失败：\n{ex.Message}", "OC editor",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _uninstallButton.Enabled = true;
        }
    }

    private async void InstallClicked(object? sender, EventArgs e)
    {
        var target = _pathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            MessageBox.Show(this, "请选择安装位置。", "OC editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _status.Text = "正在清理旧版本…";
            StopRunningApp();
            RemoveInstalledVersions(target);

            Directory.CreateDirectory(target);
            _installButton.Enabled = false;
            _browseButton.Enabled = false;
            _pathBox.Enabled = false;
            _status.Text = "正在安装，请稍候…";

            await Task.Run(() => ExtractPayload(target, progress => BeginInvoke(() =>
            {
                _progress.Value = Math.Clamp(progress, 0, 100);
                _status.Text = $"正在安装… {progress}%";
            })));

            var exe = Path.Combine(target, "OCeditor.exe");
            var uninstaller = Path.Combine(target, "Uninstall.exe");
            var self = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(self) && File.Exists(self))
            {
                File.Copy(self, uninstaller, overwrite: true);
            }

            try
            {
                CreateShortcuts(target, exe, uninstaller);
            }
            catch (Exception shortcutError)
            {
                Program.Log($"shortcut failed: {shortcutError}");
            }

            try
            {
                WriteUninstallEntry(target, exe, uninstaller);
            }
            catch (Exception registryError)
            {
                Program.Log($"registry failed: {registryError}");
            }

            _progress.Value = 100;
            _status.Text = "安装完成";
            if (_silent)
            {
                Close();
                return;
            }

            MessageBox.Show(this, "OC editor 安装完成。", "OC editor", MessageBoxButtons.OK, MessageBoxIcon.Information);

            if (MessageBox.Show(this, "现在启动 OC editor 吗？", "OC editor", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = target, UseShellExecute = true });
            }
            Close();
        }
        catch (UnauthorizedAccessException)
        {
            _status.Text = "没有写入权限";
            MessageBox.Show(this, "当前安装位置没有写入权限，请选择其他文件夹。", "OC editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _installButton.Enabled = true;
            _browseButton.Enabled = true;
            _pathBox.Enabled = true;
        }
        catch (Exception ex)
        {
            _status.Text = "安装失败";
            MessageBox.Show(this, $"安装失败：\n{ex.Message}", "OC editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _installButton.Enabled = true;
            _browseButton.Enabled = true;
            _pathBox.Enabled = true;
        }
    }

    private static void ExtractPayload(string target, Action<int> progress)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResource)
            ?? throw new InvalidOperationException("安装包资源缺失。");
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entries = archive.Entries;
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var destination = Path.GetFullPath(Path.Combine(target, entry.FullName));
            if (!destination.StartsWith(Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destination);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entry.ExtractToFile(destination, overwrite: true);
            }
            if (i % 20 == 0 || i == entries.Count - 1)
            {
                progress((int)((i + 1) * 100.0 / entries.Count));
            }
        }
    }

    private void CreateShortcuts(string target, string exe, string uninstaller)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("无法创建快捷方式。");
        dynamic shell = Activator.CreateInstance(shellType)!;

        if (_desktopShortcut.Checked)
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            CreateShortcut(shell, Path.Combine(desktop, "OC editor.lnk"), exe, target);
        }

        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var menuDir = Path.Combine(programs, "OC editor");
        Directory.CreateDirectory(menuDir);
        CreateShortcut(shell, Path.Combine(menuDir, "OC editor.lnk"), exe, target);

        dynamic uninstallLink = shell.CreateShortcut(Path.Combine(menuDir, "卸载 OC editor.lnk"));
        uninstallLink.TargetPath = uninstaller;
        uninstallLink.Arguments = "--uninstall";
        uninstallLink.WorkingDirectory = target;
        uninstallLink.Save();
    }

    private static void CreateShortcut(dynamic shell, string linkPath, string exe, string workingDirectory)
    {
        dynamic link = shell.CreateShortcut(linkPath);
        link.TargetPath = exe;
        link.WorkingDirectory = workingDirectory;
        link.IconLocation = $"{exe},0";
        link.Description = "OC editor 本地优先创作工作台";
        link.Save();
    }

    private static void WriteUninstallEntry(string target, string exe, string uninstaller)
    {
        using var key = Registry.CurrentUser.CreateSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\OCEditor");
        key.SetValue("DisplayName", "OC editor");
        key.SetValue("DisplayVersion", typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.19.1");
        key.SetValue("Publisher", "OC editor");
        key.SetValue("InstallLocation", target);
        key.SetValue("DisplayIcon", $"{exe},0");
        key.SetValue("UninstallString", $"\"{uninstaller}\" --uninstall");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }
}
