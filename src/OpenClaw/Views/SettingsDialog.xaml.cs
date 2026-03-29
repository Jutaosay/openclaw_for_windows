// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OpenClaw.Models;
using OpenClaw.ViewModels;
using Windows.Graphics;

namespace OpenClaw.Views;

/// <summary>
/// Settings window with Windows Settings-style sidebar navigation.
/// Resizable, Mica-backed, independent window.
/// </summary>
public sealed partial class SettingsDialog : Window
{
    public SettingsViewModel ViewModel { get; } = new();

    /// <summary>
    /// Gets the main view model for developer tools commands.
    /// </summary>
    public MainViewModel? MainViewModel { get; set; }

    /// <summary>
    /// Raised when settings are saved, so MainWindow can refresh.
    /// </summary>
    public event Action? SettingsSaved;

    public SettingsDialog()
    {
        this.InitializeComponent();

        // Window chrome
        Title = "Settings";
        SystemBackdrop = new MicaBackdrop();
        AppWindow.SetIcon("Assets\\WindowIcon.ico");

        // Default size
        AppWindow.Resize(new SizeInt32(720, 520));

        // Initialize data
        EnvironmentList.ItemsSource = ViewModel.Environments;
        if (ViewModel.Environments.Count > 0)
        {
            EnvironmentList.SelectedIndex = 0;
        }
        SetLanguageSelection(ViewModel.SelectedLanguage);

        // Select first nav item (Language)
        NavList.SelectedIndex = 0;
    }

    // --- Sidebar navigation ---

    private void OnNavSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is ListViewItem item && item.Tag is string tag)
        {
            ShowPanel(tag);
        }
    }

    private void ShowPanel(string tag)
    {
        PanelLanguage.Visibility = tag == "Language" ? Visibility.Visible : Visibility.Collapsed;
        PanelEnvironments.Visibility = tag == "Environments" ? Visibility.Visible : Visibility.Collapsed;
        PanelSessions.Visibility = tag == "Sessions" ? Visibility.Visible : Visibility.Collapsed;
        PanelDevTools.Visibility = tag == "DevTools" ? Visibility.Visible : Visibility.Collapsed;
    }

    // --- Language ---

    private void SetLanguageSelection(string language)
    {
        for (int i = 0; i < LanguageComboBox.Items.Count; i++)
        {
            if (LanguageComboBox.Items[i] is ComboBoxItem item && item.Tag is string tag && tag == language)
            {
                LanguageComboBox.SelectedIndex = i;
                return;
            }
        }
        LanguageComboBox.SelectedIndex = 0; // Default to System
    }

    private void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string langTag)
        {
            ViewModel.SelectedLanguage = langTag;
        }
    }

    // --- Environment CRUD ---

    private void OnEnvironmentSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EnvironmentList.SelectedItem is EnvironmentConfig env)
        {
            ViewModel.SelectedEnvironment = env;
        }
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        ViewModel.AddEnvironment();
        EnvironmentList.SelectedItem = ViewModel.SelectedEnvironment;
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        ViewModel.RemoveEnvironment();
        if (ViewModel.Environments.Count > 0)
        {
            EnvironmentList.SelectedIndex = 0;
        }
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.TryApplyEdit())
        {
            ShowEnvironmentMessage("Validation Error", InfoBarSeverity.Error);
            return;
        }

        ValidationInfoBar.IsOpen = false;
        var selected = EnvironmentList.SelectedIndex;
        EnvironmentList.ItemsSource = null;
        EnvironmentList.ItemsSource = ViewModel.Environments;
        if (selected >= 0 && selected < ViewModel.Environments.Count)
        {
            EnvironmentList.SelectedIndex = selected;
        }
    }

    // --- Save / Cancel ---

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsEditing)
        {
            if (!ViewModel.TryApplyEdit())
            {
                ShowEnvironmentMessage("Validation Error", InfoBarSeverity.Error);
                return;
            }
        }

        if (ViewModel.SaveAll())
        {
            // Apply language override immediately (full effect after restart)
            App.ApplyLanguage(ViewModel.SelectedLanguage);
            SettingsSaved?.Invoke();
            this.Close();
        }
        else
        {
            ShowEnvironmentMessage("Validation Error", InfoBarSeverity.Error);
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    // --- Developer Tools ---

    private void OnRunDiagnosticsClick(object sender, RoutedEventArgs e)
    {
        MainViewModel?.RunDiagnosticsCommand.Execute(null);
        this.Close();
    }

    private void OnViewLogsClick(object sender, RoutedEventArgs e)
    {
        MainViewModel?.ViewLogsCommand.Execute(null);
    }

    private void OnDevToolsClick(object sender, RoutedEventArgs e)
    {
        MainViewModel?.DevToolsCommand.Execute(null);
        this.Close();
    }

    private async void OnClearEnvironmentSessionClick(object sender, RoutedEventArgs e)
    {
        if (MainViewModel is null ||
            sender is not Button button ||
            button.Tag is not string environmentName ||
            string.IsNullOrWhiteSpace(environmentName))
        {
            SessionInfoBar.Title = "Session Reset";
            SessionInfoBar.Severity = InfoBarSeverity.Warning;
            SessionInfoBar.Message = "Select an environment to clear its session.";
            SessionInfoBar.IsOpen = true;
            return;
        }

        await MainViewModel.ClearSessionForEnvironmentAsync(environmentName);
        SessionInfoBar.Title = "Session Reset";
        SessionInfoBar.Severity = InfoBarSeverity.Informational;
        SessionInfoBar.Message = $"Cleared the saved session for '{environmentName}'.";
        SessionInfoBar.IsOpen = true;
    }

    private void ShowEnvironmentMessage(string title, InfoBarSeverity severity, string? message = null)
    {
        ValidationInfoBar.Title = title;
        ValidationInfoBar.Severity = severity;
        ValidationInfoBar.Message = message ?? ViewModel.ValidationMessage;
        ValidationInfoBar.IsOpen = true;
    }
}
