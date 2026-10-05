using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media;
using MasterPlay.Infrastructure.Configuration;
using Microsoft.Win32;

namespace MasterPlay.Shell;

/// <summary>
/// Man cai dat lan dau, hai buoc: ngon ngu -> thu muc du lieu. Xong thi ghi lua
/// chon (FirstRunSetup.Apply) va tra DialogResult = true; dong cua so la huy.
/// </summary>
public partial class SetupWindow : Window
{
    private const int StepCount = 2;
    private const int DwmWindowAttributeCornerPreference = 33;
    private const int DwmWindowCornerRound = 2;

    private static readonly Brush InfoBrush = new SolidColorBrush(Color.FromRgb(0x9D, 0xB8, 0x92));
    private static readonly Brush WarningBrush = new SolidColorBrush(Color.FromRgb(0xE3, 0xB5, 0x6B));

    private readonly SetupState _state;
    private string _language;
    private int _step;

    internal SetupWindow(SetupState state)
    {
        InitializeComponent();
        _state = state;
        _language = state.Language;
        SourceInitialized += (_, _) => RoundCorners();

        DataPathBox.Text = state.DataDirectory;
        (_language == FirstRunSetup.Vietnamese ? VietnameseOption : EnglishOption).IsChecked = true;

        // Thu muc chon truoc day khong dung duoc nua: vao thang buoc chon thu muc.
        _step = state.Notice == SetupNotice.None ? 0 : 1;
        ShowStep();
    }

    private string Text(string key) => SetupStrings.Get(_language, key);

    private void ShowStep()
    {
        var onDataStep = _step == 1;
        LanguagePage.Visibility = onDataStep ? Visibility.Collapsed : Visibility.Visible;
        DataPage.Visibility = onDataStep ? Visibility.Visible : Visibility.Collapsed;
        BackButton.Visibility = onDataStep ? Visibility.Visible : Visibility.Collapsed;
        ApplyTexts();

        if (onDataStep)
        {
            DataPathBox.Focus();
            DataPathBox.CaretIndex = DataPathBox.Text.Length;
        }
        else
        {
            (VietnameseOption.IsChecked == true ? VietnameseOption : EnglishOption).Focus();
        }
    }

    private void ApplyTexts()
    {
        var onDataStep = _step == 1;
        Title = Text("WindowTitle");
        WindowTitleText.Text = Title;
        StepText.Text = string.Format(Text("Step"), _step + 1, StepCount);
        TitleText.Text = Text(onDataStep ? "DataTitle" : "LanguageTitle");
        SubtitleText.Text = Text(onDataStep ? "DataSubtitle" : "LanguageSubtitle");
        DataLabelText.Text = Text("DataLabel");
        AutomationProperties.SetName(DataPathBox, Text("DataLabel"));
        BrowseButton.Content = Text("Browse");
        UseDefaultButton.Content = Text("UseDefault");
        TrayNoteText.Text = Text("TrayNote");
        BackButton.Content = Text("Back");
        NextButton.Content = Text(onDataStep ? "Finish" : "Next");
        CloseButton.ToolTip = Text("Close");
        AutomationProperties.SetName(CloseButton, Text("Close"));
        UpdateNotice();
    }

    private void OnLanguageChecked(object sender, RoutedEventArgs e)
    {
        _language = sender == VietnameseOption ? FirstRunSetup.Vietnamese : FirstRunSetup.English;
        // Checked chay ngay trong InitializeComponent/constructor, truoc khi co du control.
        if (IsInitialized) ApplyTexts();
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        if (_step == 0)
        {
            _step = 1;
            ShowStep();
            return;
        }

        Finish();
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        _step = 0;
        HideError();
        ShowStep();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = Text("BrowseTitle"),
            InitialDirectory = ExistingAncestor(DataPathBox.Text) ?? string.Empty,
        };
        if (dialog.ShowDialog(this) != true) return;

        // Chon "D:\Games" thi luu vao D:\Games\QuestLog, khong rai config/CSDL ra giua
        // thu muc nguoi dung. Tru khi chon dung thu muc du lieu QuestLog co san.
        var selected = dialog.FolderName;
        DataPathBox.Text = AppPaths.ContainsData(selected) ||
                           Path.GetFileName(Path.TrimEndingDirectorySeparator(selected))
                               .Equals("QuestLog", StringComparison.OrdinalIgnoreCase)
            ? selected
            : Path.Combine(selected, "QuestLog");
        DataPathBox.Focus();
        DataPathBox.CaretIndex = DataPathBox.Text.Length;
    }

    private void OnUseDefaultClick(object sender, RoutedEventArgs e)
    {
        DataPathBox.Text = AppPaths.DefaultDataDirectory;
        DataPathBox.Focus();
        DataPathBox.CaretIndex = DataPathBox.Text.Length;
    }

    private void OnDataPathChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        HideError();
        UpdateNotice();
    }

    /// <summary>Canh bao vi tri cu hong/mat, hoac bao thu muc dang nhap da co du lieu.</summary>
    private void UpdateNotice()
    {
        var path = DataPathBox.Text.Trim().Trim('"');
        var isPreviousChoice = path.Length > 0 &&
                               Path.IsPathFullyQualified(path) &&
                               AppPaths.IsSamePath(path, _state.DataDirectory);

        if (_state.Notice == SetupNotice.MissingFolder && isPreviousChoice && !Directory.Exists(path))
        {
            ShowNotice("MissingFolder", "\uE7BA", WarningBrush);
        }
        else if (_state.Notice == SetupNotice.UnreadableLocation && isPreviousChoice)
        {
            ShowNotice("UnreadableLocation", "\uE7BA", WarningBrush);
        }
        else if (path.Length > 0 && Path.IsPathFullyQualified(path) && SafeContainsData(path))
        {
            ShowNotice("ExistingData", "\uE930", InfoBrush);
        }
        else
        {
            NoticePanel.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowNotice(string key, string glyph, Brush brush)
    {
        NoticeIcon.Text = glyph;
        NoticeIcon.Foreground = brush;
        NoticeText.Text = Text(key);
        NoticeText.Foreground = brush;
        NoticePanel.Visibility = Visibility.Visible;
    }

    private void Finish()
    {
        var error = FirstRunSetup.ValidateDataDirectory(DataPathBox.Text, out var directory, out var detail);
        if (error != SetupPathError.None)
        {
            ShowError(error switch
            {
                SetupPathError.Required => Text("ErrorRequired"),
                SetupPathError.NotFullPath => Text("ErrorNotFullPath"),
                SetupPathError.InsideInstall => Text("ErrorInsideInstall"),
                _ => string.Format(Text("ErrorNotWritable"), detail),
            });
            DataPathBox.Focus();
            return;
        }

        try
        {
            FirstRunSetup.Apply(
                directory,
                _language,
                dataDirectoryChanged: !AppPaths.IsSamePath(directory, _state.DataDirectory));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException or ArgumentException)
        {
            ShellLog.Write($"Khong luu duoc lua chon cai dat: {ex}");
            ShowError(string.Format(Text("ErrorSaveFailed"), ex.Message));
            return;
        }

        ShellLog.Write($"Cai dat lan dau: ngon ngu {_language}, du lieu {directory}.");
        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void HideError() => ErrorText.Visibility = Visibility.Collapsed;

    private static bool SafeContainsData(string path)
    {
        try
        {
            return AppPaths.ContainsData(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            return false;
        }
    }

    /// <summary>Thu muc gan nhat con ton tai cua duong dan dang nhap, de hop chon mo dung cho.</summary>
    private static string? ExistingAncestor(string input)
    {
        try
        {
            var path = input.Trim().Trim('"');
            if (!Path.IsPathFullyQualified(path)) return null;
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

    /// <summary>Windows 11 khong tu bo goc cua so WindowStyle=None; xin DWM bo nhu cua so chinh.</summary>
    private void RoundCorners()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;

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
