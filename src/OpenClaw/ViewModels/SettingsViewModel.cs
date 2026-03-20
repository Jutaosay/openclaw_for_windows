// Copyright (c) OpenClaw. All rights reserved.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpenClaw.Models;

namespace OpenClaw.ViewModels;

/// <summary>
/// ViewModel for the Settings dialog.
/// Manages CRUD operations on gateway environment configurations.
/// </summary>
public class SettingsViewModel : INotifyPropertyChanged
{
    private EnvironmentConfig? _selectedEnvironment;
    private string _editName = string.Empty;
    private string _editUrl = string.Empty;
    private bool _editIsDefault;
    private bool _isEditing;
    private string _selectedTheme = "System";
    private string _selectedLanguage = "System";

    public SettingsViewModel()
    {
        // Load a copy of environments so we can cancel without persisting
        foreach (var env in App.Configuration.Settings.Environments)
        {
            Environments.Add(env.Clone());
        }

        // Load theme preference
        _selectedTheme = App.Configuration.Settings.AppTheme ?? "System";

        // Load language preference
        _selectedLanguage = App.Configuration.Settings.AppLanguage ?? "System";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<EnvironmentConfig> Environments { get; } = [];

    public EnvironmentConfig? SelectedEnvironment
    {
        get => _selectedEnvironment;
        set
        {
            _selectedEnvironment = value;
            OnPropertyChanged();
            LoadEditFields();
        }
    }

    public string EditName
    {
        get => _editName;
        set { _editName = value; OnPropertyChanged(); }
    }

    public string EditUrl
    {
        get => _editUrl;
        set { _editUrl = value; OnPropertyChanged(); }
    }

    public bool EditIsDefault
    {
        get => _editIsDefault;
        set { _editIsDefault = value; OnPropertyChanged(); }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set { _isEditing = value; OnPropertyChanged(); }
    }

    public string SelectedTheme
    {
        get => _selectedTheme;
        set { _selectedTheme = value; OnPropertyChanged(); }
    }

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set { _selectedLanguage = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Adds a new environment with placeholder values.
    /// </summary>
    public void AddEnvironment()
    {
        var env = new EnvironmentConfig
        {
            Name = $"Environment {Environments.Count + 1}",
            GatewayUrl = "https://",
            IsDefault = Environments.Count == 0,
        };
        Environments.Add(env);
        SelectedEnvironment = env;
        IsEditing = true;
    }

    /// <summary>
    /// Removes the currently selected environment.
    /// </summary>
    public void RemoveEnvironment()
    {
        if (_selectedEnvironment is null) return;
        Environments.Remove(_selectedEnvironment);
        SelectedEnvironment = Environments.FirstOrDefault();
    }

    /// <summary>
    /// Applies edit field values back to the selected environment.
    /// </summary>
    public void ApplyEdit()
    {
        if (_selectedEnvironment is null) return;

        _selectedEnvironment.Name = EditName;
        _selectedEnvironment.GatewayUrl = EditUrl;

        // If setting as default, clear others
        if (EditIsDefault)
        {
            foreach (var env in Environments)
            {
                env.IsDefault = env == _selectedEnvironment;
            }
        }
        else
        {
            _selectedEnvironment.IsDefault = false;
        }

        IsEditing = false;

        // Notify to refresh display
        OnPropertyChanged(nameof(Environments));
    }

    /// <summary>
    /// Saves all environments to the configuration service.
    /// Returns true if save was successful.
    /// </summary>
    public bool SaveAll()
    {
        // Validate
        foreach (var env in Environments)
        {
            if (string.IsNullOrWhiteSpace(env.Name) || string.IsNullOrWhiteSpace(env.GatewayUrl))
            {
                return false;
            }
        }

        App.Configuration.Settings.Environments = [.. Environments];

        // Ensure at least one default
        if (!App.Configuration.Settings.Environments.Any(e => e.IsDefault) &&
            App.Configuration.Settings.Environments.Count > 0)
        {
            App.Configuration.Settings.Environments[0].IsDefault = true;
        }

        // Save theme and language
        App.Configuration.Settings.AppTheme = SelectedTheme;
        App.Configuration.Settings.AppLanguage = SelectedLanguage;

        App.Configuration.Save();
        App.Logger.Info("Settings saved.");
        return true;
    }

    private void LoadEditFields()
    {
        if (_selectedEnvironment is not null)
        {
            EditName = _selectedEnvironment.Name;
            EditUrl = _selectedEnvironment.GatewayUrl;
            EditIsDefault = _selectedEnvironment.IsDefault;
            IsEditing = true;
        }
        else
        {
            EditName = string.Empty;
            EditUrl = string.Empty;
            EditIsDefault = false;
            IsEditing = false;
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
