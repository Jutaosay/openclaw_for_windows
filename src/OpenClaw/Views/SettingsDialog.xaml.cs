// Copyright (c) OpenClaw. All rights reserved.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenClaw.Models;
using OpenClaw.ViewModels;

namespace OpenClaw.Views;

/// <summary>
/// Settings dialog for managing gateway environment configurations.
/// </summary>
public sealed partial class SettingsDialog : ContentDialog
{
    public SettingsViewModel ViewModel { get; } = new();

    /// <summary>
    /// Gets the main view model so we can invoke diagnostic/dev tools commands.
    /// This resolves the connection to the MainWindow's view model.
    /// </summary>
    public MainViewModel? MainViewModel { get; set; }

    public SettingsDialog()
    {
        this.InitializeComponent();
        EnvironmentList.ItemsSource = ViewModel.Environments;

        // Select the first item if available
        if (ViewModel.Environments.Count > 0)
        {
            EnvironmentList.SelectedIndex = 0;
        }
    }

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
        ViewModel.ApplyEdit();

        // Force refresh of the list
        var selected = EnvironmentList.SelectedIndex;
        EnvironmentList.ItemsSource = null;
        EnvironmentList.ItemsSource = ViewModel.Environments;
        if (selected >= 0 && selected < ViewModel.Environments.Count)
        {
            EnvironmentList.SelectedIndex = selected;
        }
    }

    private void OnSaveClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Apply any pending edit first
        if (ViewModel.IsEditing)
        {
            ViewModel.ApplyEdit();
        }

        if (!ViewModel.SaveAll())
        {
            args.Cancel = true;
            ValidationInfoBar.IsOpen = true;
        }
    }

    private void OnRunDiagnosticsClick(object sender, RoutedEventArgs e)
    {
        MainViewModel?.RunDiagnosticsCommand.Execute(null);
        this.Hide(); // Close settings when running diagnostics
    }

    private void OnViewLogsClick(object sender, RoutedEventArgs e)
    {
        MainViewModel?.ViewLogsCommand.Execute(null);
        // Do not close settings dialog
    }

    private void OnDevToolsClick(object sender, RoutedEventArgs e)
    {
        MainViewModel?.DevToolsCommand.Execute(null);
        this.Hide(); // Developer tools opens external window
    }

    private void OnClearSessionClick(object sender, RoutedEventArgs e)
    {
        MainViewModel?.ClearSessionCommand.Execute(null);
        this.Hide(); // User needs to interact with main window/reload
    }
}
