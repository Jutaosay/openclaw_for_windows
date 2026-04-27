// Copyright (c) Lanstack @openclaw. All rights reserved.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpenClaw.Helpers;
using OpenClaw.Models;
using OpenClaw.Services;
using OpenClaw.Views;

namespace OpenClaw.ViewModels;

/// <summary>
/// ViewModel for the Settings dialog.
/// Manages CRUD operations on gateway environment configurations.
/// </summary>
public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly Dictionary<EnvironmentConfig, string> _originalNames = [];
    private readonly Dictionary<EnvironmentConfig, EnvironmentConfig> _originalSnapshots = [];
    private readonly string? _originalSelectedEnvironmentName = App.Configuration.Settings.SelectedEnvironmentName;
    private readonly string _originalLanguage;
    private EnvironmentConfig? _selectedEnvironment;
    private string _editName = string.Empty;
    private string _editUrl = string.Empty;
    private bool _editIsDefault;
    private bool _isEditing;
    private string _selectedLanguage = "System";
    private bool _enableDevLog;
    private string _validationMessage = string.Empty;

    public SettingsViewModel()
    {
        // Load a copy of environments so we can cancel without persisting
        foreach (var env in App.Configuration.Settings.Environments)
        {
            var clone = env.Clone();
            Environments.Add(clone);
            _originalNames[clone] = env.Name;
            _originalSnapshots[clone] = env.Clone();
        }

        // Load language preference
        _selectedLanguage = App.Configuration.Settings.AppLanguage ?? "System";
        _originalLanguage = _selectedLanguage;
        _enableDevLog = App.Configuration.Settings.Diagnostics.EnableVerboseRecoveryLogging;
        _validationMessage = StringResources.SettingsValidationDefaultMessage;
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

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set { _selectedLanguage = value; OnPropertyChanged(); }
    }

    public bool EnableDevLog
    {
        get => _enableDevLog;
        set { _enableDevLog = value; OnPropertyChanged(); }
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        private set
        {
            _validationMessage = value;
            OnPropertyChanged();
        }
    }

    public bool DidChangeSessionTopology { get; private set; }

    public bool DidChangeEnvironmentState { get; private set; }

    /// <summary>
    /// Adds a new environment with placeholder values.
    /// </summary>
    public void AddEnvironment()
    {
        var env = new EnvironmentConfig
        {
            Name = string.Format(StringResources.SettingsGeneratedEnvironmentName, Environments.Count + 1),
            GatewayUrl = "https://",
            IsDefault = Environments.Count == 0,
        };
        Environments.Add(env);
        _originalNames[env] = env.Name;
        _originalSnapshots[env] = env.Clone();
        DidChangeEnvironmentState = true;
        DidChangeSessionTopology = true;
        SelectedEnvironment = env;
        IsEditing = true;
    }

    /// <summary>
    /// Removes the currently selected environment.
    /// </summary>
    public void RemoveEnvironment()
    {
        if (_selectedEnvironment is null) return;
        _originalNames.Remove(_selectedEnvironment);
        _originalSnapshots.Remove(_selectedEnvironment);
        Environments.Remove(_selectedEnvironment);
        DidChangeEnvironmentState = true;
        DidChangeSessionTopology = true;
        SelectedEnvironment = Environments.FirstOrDefault();
    }

    /// <summary>
    /// Applies edit field values back to the selected environment.
    /// </summary>
    public bool TryApplyEdit()
    {
        if (_selectedEnvironment is null)
        {
            ValidationMessage = StringResources.SettingsValidationSelectEnvironment;
            return false;
        }

        var draft = CreateDraftEnvironment();

        if (!TryValidateEnvironment(draft, out var errorMessage))
        {
            ValidationMessage = errorMessage;
            return false;
        }

        _selectedEnvironment.ApplyFrom(draft);
        DidChangeEnvironmentState |= DidEnvironmentMetadataChange(_selectedEnvironment);
        DidChangeSessionTopology |= DidEnvironmentSessionIdentityChange(_selectedEnvironment);

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

        ValidationMessage = StringResources.SettingsValidationDefaultMessage;
        return true;
    }

    /// <summary>
    /// Saves all environments to the configuration service.
    /// Returns true if save was successful.
    /// </summary>
    public bool SaveAll(out SettingsSaveResult result)
    {
        result = default;
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var env in Environments)
        {
            if (!TryValidateEnvironment(env, out var errorMessage))
            {
                ValidationMessage = errorMessage;
                return false;
            }

            if (!seenNames.Add(env.Name.Trim()))
            {
                ValidationMessage = string.Format(StringResources.SettingsValidationDuplicateEnvironment, env.Name);
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

        var persistedSelection = ResolveSelectedEnvironmentName();
        App.Configuration.Settings.SelectedEnvironmentName = persistedSelection;

        // Save language
        App.Configuration.Settings.AppLanguage = SelectedLanguage;
        App.Configuration.Settings.Diagnostics.EnableVerboseRecoveryLogging = EnableDevLog;

        SyncRenamedEnvironmentProfiles();

        App.Configuration.Save();
        App.Logger.Info("Settings saved.");
        ValidationMessage = StringResources.SettingsValidationDefaultMessage;
        result = new SettingsSaveResult(
            DidChangeEnvironmentState,
            DidChangeSessionTopology,
            !string.Equals(_originalLanguage, SelectedLanguage, StringComparison.Ordinal));
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

    private string? ResolveSelectedEnvironmentName()
    {
        if (!string.IsNullOrEmpty(_originalSelectedEnvironmentName))
        {
            var matchedEnvironment = Environments.FirstOrDefault(env =>
                _originalNames.TryGetValue(env, out var originalName) &&
                string.Equals(originalName, _originalSelectedEnvironmentName, StringComparison.Ordinal));

            if (matchedEnvironment is not null)
            {
                return matchedEnvironment.Name;
            }
        }

        return App.Configuration.Settings.Environments.FirstOrDefault(e => e.IsDefault)?.Name
            ?? App.Configuration.Settings.Environments.FirstOrDefault()?.Name;
    }

    private EnvironmentConfig CreateDraftEnvironment() => new()
    {
        Name = EditName.Trim(),
        GatewayUrl = EditUrl.Trim(),
        IsDefault = EditIsDefault,
    };

    private void SyncRenamedEnvironmentProfiles()
    {
        foreach (var env in Environments)
        {
            if (!_originalNames.TryGetValue(env, out var originalName))
            {
                continue;
            }

            WebViewService.TryMoveUserDataFolderToRenamedEnvironment(originalName, env.Name);
            _originalNames[env] = env.Name;
            _originalSnapshots[env] = env.Clone();
        }
    }

    private bool DidEnvironmentSessionIdentityChange(EnvironmentConfig environment)
    {
        if (!_originalSnapshots.TryGetValue(environment, out var original))
        {
            return true;
        }

        return !string.Equals(original.Name, environment.Name, StringComparison.Ordinal) ||
               !string.Equals(original.GatewayUrl, environment.GatewayUrl, StringComparison.Ordinal);
    }

    private bool DidEnvironmentMetadataChange(EnvironmentConfig environment)
    {
        if (!_originalSnapshots.TryGetValue(environment, out var original))
        {
            return true;
        }

        return !string.Equals(original.Name, environment.Name, StringComparison.Ordinal) ||
               !string.Equals(original.GatewayUrl, environment.GatewayUrl, StringComparison.Ordinal) ||
               original.IsDefault != environment.IsDefault;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private static bool TryValidateEnvironment(EnvironmentConfig environment, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(environment.Name))
        {
            errorMessage = StringResources.SettingsValidationEnvironmentNameRequired;
            return false;
        }

        if (string.IsNullOrWhiteSpace(environment.GatewayUrl))
        {
            errorMessage = StringResources.SettingsValidationControlUiUrlRequired;
            return false;
        }

        if (!Uri.TryCreate(environment.GatewayUrl.Trim(), UriKind.Absolute, out var uri))
        {
            errorMessage = StringResources.SettingsValidationControlUiUrlAbsolute;
            return false;
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            errorMessage = uri.Scheme is "ws" or "wss"
                ? StringResources.SettingsValidationControlUiUrlWs
                : StringResources.SettingsValidationControlUiUrlScheme;
            return false;
        }

        errorMessage = StringResources.SettingsValidationDefaultMessage;
        return true;
    }
}
