// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenClaw.Models;

namespace OpenClaw.Views;

public sealed partial class SettingsDialog
{
    private void OnNavSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is ListViewItem item && item.Tag is string tag)
        {
            ShowPanel(tag);
        }
    }

    private void ShowPanel(string tag)
    {
        PanelLanguage.Visibility = ToPanelVisibility(tag == LanguagePanelTag);
        PanelEnvironments.Visibility = ToPanelVisibility(tag == EnvironmentsPanelTag);
        PanelSessions.Visibility = ToPanelVisibility(tag == SessionsPanelTag);
        PanelDevTools.Visibility = ToPanelVisibility(tag == DevToolsPanelTag);
    }

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

        LanguageComboBox.SelectedIndex = 0;
    }

    private void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string langTag)
        {
            ViewModel.SelectedLanguage = langTag;
        }
    }

    private void OnEnvironmentSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EnvironmentList.SelectedItem is EnvironmentConfig environment)
        {
            ViewModel.SelectedEnvironment = environment;
        }
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        ViewModel.AddEnvironment();
        SelectEnvironment(ViewModel.SelectedEnvironment);
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        ViewModel.RemoveEnvironment();
        SelectFirstEnvironment();
    }

    private static Visibility ToPanelVisibility(bool isVisible) =>
        isVisible ? Visibility.Visible : Visibility.Collapsed;

    private void SelectEnvironment(EnvironmentConfig? environment)
    {
        EnvironmentList.SelectedItem = environment;
    }

    private void SelectFirstEnvironment()
    {
        if (ViewModel.Environments.Count <= 0)
        {
            EnvironmentList.SelectedItem = null;
            return;
        }

        EnvironmentList.SelectedIndex = 0;
    }
}
