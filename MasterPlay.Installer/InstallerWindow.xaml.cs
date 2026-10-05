using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;

namespace MasterPlay.Installer;

/// <summary>
/// Man chon thu muc cai. Bam Cai dat thi an cua so, chay Setup.exe cua Velopack (no tu
/// hien tien trinh roi mo app); cai khong xong thi hien lai cua so kem loi.
/// </summary>
public partial class InstallerWindow : Window
{
    private const int DwmWindowAttributeCornerPreference = 33;
    private const int DwmWindowCornerRound = 2;

    private readonly string? _existingInstall;

    public InstallerWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => RoundCorners();

        Title = Text("WindowTitle");
        WindowTitleText.Text = Title;
        TitleText.Text = Text("Title");
        SubtitleText.Text = Text("Subtitle");
        LabelText.Text = Text("Label");
        AutomationProperties.SetName(PathBox, Text("Label"));
        BrowseButton.Content = Text("Browse");
        UseDefaultButton.Content = Text("UseDefault");
        FolderNoteText.Text = Text("FolderNote");
        CancelButton.Content = Text("Cancel");
        InstallButton.Content = Text("Install");
        CloseButton.ToolTip = Text("Close");
        AutomationProperties.SetName(CloseButton, Text("Close"));

        // Velopack chi giu mot ban cai cho moi nguoi dung. Cai sang cho khac khi ban cu con
        // thi muc go cai dat tro sang ban moi, thu muc cu nam lai khong ai don.
        _existingInstall = InstallTarget.FindExistingInstall();
        if (_existingInstall != null)
        {
            PathBox.Text = _existingInstall;
            PathBox.IsReadOnly = true;
            BrowseButton.IsEnabled = false;
            UseDefaultButton.Visibility = Visibility.Collapsed;
            ExistingText.Text = Text("Existing");
            ExistingPanel.Visibility = Visibility.Visible;
        }
        else
        {
            PathBox.Text = InstallTarget.DefaultDirectory;
        }

        Loaded += (_, _) =>
        {
            PathBox.Focus();
            PathBox.CaretIndex = PathBox.Text.Length;
        };
    }

    /// <summary>0 khi da cai xong; khac 0 khi huy hoac loi.</summary>
    public int ExitCode { get; private set; } = 1;

    private static string Text(string key) => InstallerStrings.Get(key);

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var picked = FolderPicker.Show(this, Text("BrowseTitle"), ExistingAncestor(PathBox.Text));
        if (picked == null) return;

        PathBox.Text = InstallTarget.FromPickedFolder(picked);
        PathBox.Focus();
        PathBox.CaretIndex = PathBox.Text.Length;
    }

    private void OnUseDefaultClick(object sender, RoutedEventArgs e)
    {
        PathBox.Text = InstallTarget.DefaultDirectory;
        PathBox.Focus();
        PathBox.CaretIndex = PathBox.Text.Length;
    }

    private void OnPathChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => HideError();

    private async void OnInstallClick(object sender, RoutedEventArgs e)
    {
        if (!VelopackSetup.IsBundled)
        {
            ShowError(Text("ErrorMissingPackage"));
            return;
        }

        string directory;
        var created = false;
        if (_existingInstall != null)
        {
            // Cai lai dung cho cu: Velopack tu hoi co ghi de/sua ban dang cai khong.
            directory = _existingInstall;
        }
        else
        {
            var error = InstallTarget.Validate(PathBox.Text, InstallEnvironment.Current(), out directory);
            if (error != InstallPathError.None)
            {
                ShowError(Text("Error" + error));
                PathBox.Focus();
                return;
            }

            if (!InstallTarget.TryPrepare(directory, out created, out var detail))
            {
                ShowError(string.Format(Text("ErrorNotWritable"), detail));
                PathBox.Focus();
                return;
            }
        }

        HideError();
        IsEnabled = false;
        Hide();

        int exitCode;
        try
        {
            exitCode = await VelopackSetup.RunAsync(VelopackSetup.InstallArguments(directory));
        }
        catch (Exception ex)
        {
            if (created) InstallTarget.RemoveIfEmpty(directory);
            ShowAgain(string.Format(Text("ErrorLaunch"), ex.Message));
            return;
        }

        if (exitCode == 0)
        {
            ExitCode = 0;
            Close();
            return;
        }

        if (created) InstallTarget.RemoveIfEmpty(directory);
        ShowAgain(string.Format(Text("ErrorFailed"), exitCode, VelopackSetup.LogPath));
    }

    private void ShowAgain(string error)
    {
        IsEnabled = true;
        Show();
        Activate();
        ShowError(error);
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void HideError() => ErrorText.Visibility = Visibility.Collapsed;

    /// <summary>Thu muc gan nhat con ton tai cua duong dan dang nhap, de hop chon mo dung cho.</summary>
    private static string? ExistingAncestor(string input)
    {
        try
        {
            var path = input.Trim().Trim('"');
            if (path.Length == 0) return null;
            for (var current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
            {
                if (Directory.Exists(current)) return current;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
        }

        return null;
    }

    /// <summary>Windows 11 khong tu bo goc cua so WindowStyle=None; xin DWM bo nhu cua so cua app.</summary>
    private void RoundCorners()
    {
        if (Environment.OSVersion.Version < new Version(10, 0, 22000)) return;

        var handle = new WindowInteropHelper(this).Handle;
        var preference = DwmWindowCornerRound;
        _ = DwmSetWindowAttribute(handle, DwmWindowAttributeCornerPreference, ref preference, sizeof(int));
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);
}
