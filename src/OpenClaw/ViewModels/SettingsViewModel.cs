// Copyright (c) Lanstack @openclaw. All rights reserved.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpenClaw.Models;
using OpenClaw.Services;

namespace OpenClaw.ViewModels;

/// <summary>
/// ViewModel for the Settings dialog.
/// Manages CRUD operations on gateway environment configurations.
/// </summary>
public class SettingsViewModel : INotifyPropertyChanged
{
    private const string DefaultValidationMessage = "All environments must have a unique name and a valid http(s) Control UI URL. For Cloudflare Tunnel or reverse-proxy deployments, use the public HTTPS Control UI URL and keep gateway.controlUi.allowedOrigins in sync with its exact origin.";
    private readonly Dictionary<EnvironmentConfig, string> _originalNames = [];
    private readonly string? _originalSelectedEnvironmentName = App.Configuration.Settings.SelectedEnvironmentName;
    private EnvironmentConfig? _selectedEnvironment;
    private string _editName = string.Empty;
    private string _editUrl = string.Empty;
    private bool _editIsDefault;
    private bool _isEditing;
    private string _selectedLanguage = "System";
    private string _validationMessage = DefaultValidationMessage;

    public SettingsViewModel()
    {
        // Load a copy of environments so we can cancel without persisting
        foreach (var env in App.Configuration.Settings.Environments)
        {
            var clone = env.Clone();
            Environments.Add(clone);
            _originalNames[clone] = env.Name;
        }

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

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set { _selectedLanguage = value; OnPropertyChanged(); }
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
        _originalNames[env] = env.Name;
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
        Environments.Remove(_selectedEnvironment);
        SelectedEnvironment = Environments.FirstOrDefault();
    }

    /// <summary>
    /// Applies edit field values back to the selected environment.
    /// </summary>
    public bool TryApplyEdit()
    {
        if (_selectedEnvironment is null)
        {
            ValidationMessage = "Select an environment first.";
            return false;
        }

        var draft = new EnvironmentConfig
        {
            Name = EditName,
            GatewayUrl = EditUrl,
            IsDefault = EditIsDefault,
        };

        if (!TryValidateEnvironment(draft, out var errorMessage))
        {
            ValidationMessage = errorMessage;
            return false;
        }

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
        ValidationMessage = DefaultValidationMessage;
        return true;
    }

    /// <summary>
    /// Saves all environments to the configuration service.
    /// Returns true if save was successful.
    /// </summary>
    public bool SaveAll()
    {
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
                ValidationMessage = $"Environment name '{env.Name}' is duplicated. Session isolation now uses environment names, so each environment name must be unique.";
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

        foreach (var env in Environments)
        {
            if (_originalNames.TryGetValue(env, out var originalName))
            {
                WebViewService.TryMoveUserDataFolderToRenamedEnvironment(originalName, env.Name);
                _originalNames[env] = env.Name;
            }
        }

        App.Configuration.Save();
        App.Logger.Info("Settings saved.");
        ValidationMessage = DefaultValidationMessage;
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

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private static bool TryValidateEnvironment(EnvironmentConfig environment, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(environment.Name))
        {
            errorMessage = "Environment name is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(environment.GatewayUrl))
        {
            errorMessage = "Control UI URL is required.";
            return false;
        }

        if (!Uri.TryCreate(environment.GatewayUrl.Trim(), UriKind.Absolute, out var uri))
        {
            errorMessage = "Control UI URL must be an absolute URL.";
            return false;
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            errorMessage = uri.Scheme is "ws" or "wss"
                ? "Use the Control UI page URL here (http/https), not the Gateway WebSocket URL (ws/wss)."
                : "Control UI URL must use http or https.";
            return false;
        }

        errorMessage = DefaultValidationMessage;
        return true;
    }
}
