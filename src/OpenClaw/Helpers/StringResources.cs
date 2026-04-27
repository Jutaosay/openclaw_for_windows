// Copyright (c) Lanstack @openclaw. All rights reserved.

using Microsoft.Windows.ApplicationModel.Resources;

namespace OpenClaw.Helpers;

/// <summary>
/// Provides typed access to .resw string resources.
/// Centralizes all user-facing strings for future i18n support.
/// </summary>
public static class StringResources
{
    private static ResourceLoader? _loader;

    public static void Initialize()
    {
        _ = TryGetLoader();
    }

    private static ResourceLoader? TryGetLoader()
    {
        try
        {
            return _loader ??= new ResourceLoader();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets a string resource by key.
    /// </summary>
    public static string Get(string key)
    {
        try
        {
            return TryGetLoader()?.GetString(key) ?? key;
        }
        catch
        {
            return key; // Fallback to key name
        }
    }

    // --- Shared ---
    public static string Close => Get("Close");
    public static string Retry => Get("Retry");
    public static string DevTools => Get("DevTools");
    public static string ConnectionIssue => Get("ConnectionIssue");
    public static string DiagnosticsTitle => Get("DiagnosticsTitle");
    public static string SelectEnvironment => Get("SelectEnvironment");
    public static string MainModelLabel => Get("MainModelLabel");
    public static string MainStatusLabel => Get("MainStatusLabel");
    public static string ThemeSystem => Get("ThemeSystem");
    public static string ThemeLight => Get("ThemeLight");
    public static string ThemeDark => Get("ThemeDark");

    // --- Top Bar ---
    public static string Reload => Get("Reload");
    public static string Stop => Get("Stop");
    public static string Settings => Get("Settings");
    public static string ClearSession => Get("ClearSession");

    // --- Status Bar ---
    public static string StatusConnected => Get("StatusConnected");
    public static string StatusLoading => Get("StatusLoading");
    public static string StatusGatewayConnecting => Get("StatusGatewayConnecting");
    public static string StatusReconnecting => Get("StatusReconnecting");
    public static string StatusAuthFailed => Get("StatusAuthFailed");
    public static string StatusError => Get("StatusError");
    public static string StatusOffline => Get("StatusOffline");
    public static string StatusHeartbeatFailed => Get("StatusHeartbeatFailed");
    public static string HeartbeatOk => Get("HeartbeatOk");
    public static string HeartbeatWait => Get("HeartbeatWait");
    public static string HeartbeatBlocked => Get("HeartbeatBlocked");
    public static string HeartbeatFailed => Get("HeartbeatFailed");

    // --- Recovery States ---
    public static string RecoveryConnecting => Get("RecoveryConnecting");
    public static string RecoveryReconnecting => Get("RecoveryReconnecting");
    public static string RecoveryResyncing => Get("RecoveryResyncing");
    public static string RecoveryRefreshing => Get("RecoveryRefreshing");
    public static string RecoveryDegraded => Get("RecoveryDegraded");
    public static string RecoveryFailed => Get("RecoveryFailed");

    // --- Settings Dialog ---
    public static string SettingsTitle => Get("SettingsTitle");
    public static string SettingsNavLanguage => Get("SettingsNavLanguage");
    public static string SettingsNavEnvironments => Get("SettingsNavEnvironments");
    public static string SettingsNavSessions => Get("SettingsNavSessions");
    public static string SettingsNavDevTools => Get("SettingsNavDevTools");
    public static string SettingsLanguageDescription => Get("SettingsLanguageDescription");
    public static string SettingsLanguageSystem => Get("SettingsLanguageSystem");
    public static string SettingsLanguageEnglish => Get("SettingsLanguageEnglish");
    public static string SettingsLanguageChineseSimplified => Get("SettingsLanguageChineseSimplified");
    public static string SettingsLanguageRestartHint => Get("SettingsLanguageRestartHint");
    public static string SettingsEnvironmentsTitle => Get("SettingsEnvironmentsTitle");
    public static string SettingsEnvironmentsDescription => Get("SettingsEnvironmentsDescription");
    public static string EnvironmentName => Get("EnvironmentName");
    public static string GatewayUrl => Get("GatewayUrl");
    public static string SettingsAddTooltip => Get("SettingsAddTooltip");
    public static string SettingsRemoveTooltip => Get("SettingsRemoveTooltip");
    public static string SettingsEnvironmentPlaceholder => Get("SettingsEnvironmentPlaceholder");
    public static string SettingsControlUiUrl => Get("SettingsControlUiUrl");
    public static string SettingsControlUiUrlHint1 => Get("SettingsControlUiUrlHint1");
    public static string SettingsControlUiUrlHint2 => Get("SettingsControlUiUrlHint2");
    public static string SettingsControlUiUrlHint3 => Get("SettingsControlUiUrlHint3");
    public static string SettingsControlUiUrlHint4 => Get("SettingsControlUiUrlHint4");
    public static string SetAsDefault => Get("SetAsDefault");
    public static string SettingsApply => Get("SettingsApply");
    public static string SettingsValidationError => Get("SettingsValidationError");
    public static string SettingsSessionsTitle => Get("SettingsSessionsTitle");
    public static string SettingsSessionsDescription => Get("SettingsSessionsDescription");
    public static string SettingsSessionReset => Get("SettingsSessionReset");
    public static string SettingsClearSessionAction => Get("SettingsClearSessionAction");
    public static string SettingsDevToolsTitle => Get("SettingsDevToolsTitle");
    public static string SettingsDevToolsDescription => Get("SettingsDevToolsDescription");
    public static string SettingsEnableDevLog => Get("SettingsEnableDevLog");
    public static string SettingsEnableDevLogDescription => Get("SettingsEnableDevLogDescription");
    public static string SettingsRunDiagnostics => Get("SettingsRunDiagnostics");
    public static string SettingsViewLogs => Get("SettingsViewLogs");
    public static string SettingsOpenDevTools => Get("SettingsOpenDevTools");
    public static string AddEnvironment => Get("AddEnvironment");
    public static string RemoveEnvironment => Get("RemoveEnvironment");
    public static string Save => Get("Save");
    public static string Cancel => Get("Cancel");
    public static string NoEnvironments => Get("NoEnvironments");
    public static string SettingsSessionResetSelectEnvironment => Get("SettingsSessionResetSelectEnvironment");
    public static string SettingsSessionResetCompleted => Get("SettingsSessionResetCompleted");
    public static string SettingsValidationDefaultMessage => Get("SettingsValidationDefaultMessage");
    public static string SettingsValidationSelectEnvironment => Get("SettingsValidationSelectEnvironment");
    public static string SettingsValidationDuplicateEnvironment => Get("SettingsValidationDuplicateEnvironment");
    public static string SettingsValidationEnvironmentNameRequired => Get("SettingsValidationEnvironmentNameRequired");
    public static string SettingsValidationControlUiUrlRequired => Get("SettingsValidationControlUiUrlRequired");
    public static string SettingsValidationControlUiUrlAbsolute => Get("SettingsValidationControlUiUrlAbsolute");
    public static string SettingsValidationControlUiUrlWs => Get("SettingsValidationControlUiUrlWs");
    public static string SettingsValidationControlUiUrlScheme => Get("SettingsValidationControlUiUrlScheme");
    public static string SettingsGeneratedEnvironmentName => Get("SettingsGeneratedEnvironmentName");

    // --- About Dialog ---
    public static string AboutTitle => Get("AboutTitle");
    public static string AboutDescription => Get("AboutDescription");
    public static string AboutRepository => Get("AboutRepository");
    public static string AboutDevelopedBy => Get("AboutDevelopedBy");
    public static string AboutCopyright => Get("AboutCopyright");

    // --- Log Viewer ---
    public static string LogsTitle => Get("LogsTitle");
    public static string RefreshLogs => Get("RefreshLogs");
    public static string OpenLogFolder => Get("OpenLogFolder");
    public static string LogFileLabelFormat => Get("LogFileLabelFormat");
    public static string LogShowingLastLinesFormat => Get("LogShowingLastLinesFormat");
    public static string LogNotFoundToday => Get("LogNotFoundToday");
    public static string LogReadFailedFormat => Get("LogReadFailedFormat");

    // --- Diagnostics ---
    public static string DiagnosticWebView2RuntimeLabel => Get("DiagnosticWebView2RuntimeLabel");
    public static string DiagnosticNetworkConnectivityLabel => Get("DiagnosticNetworkConnectivityLabel");
    public static string DiagnosticSessionStatusLabel => Get("DiagnosticSessionStatusLabel");
    public static string DiagnosticWebViewRuntimeNotFound => Get("DiagnosticWebViewRuntimeNotFound");
    public static string DiagnosticWebViewRuntimeNotFoundDetail => Get("DiagnosticWebViewRuntimeNotFoundDetail");
    public static string DiagnosticWebViewRuntimeCheckFailed => Get("DiagnosticWebViewRuntimeCheckFailed");
    public static string DiagnosticWebViewRuntimeCheckFailedDetailFormat => Get("DiagnosticWebViewRuntimeCheckFailedDetailFormat");
    public static string DiagnosticNoGatewayUrlConfigured => Get("DiagnosticNoGatewayUrlConfigured");
    public static string DiagnosticNonLocalHttp => Get("DiagnosticNonLocalHttp");
    public static string DiagnosticNonLocalHttpDetail => Get("DiagnosticNonLocalHttpDetail");
    public static string DiagnosticControlUiReachableActive => Get("DiagnosticControlUiReachableActive");
    public static string DiagnosticControlUiLoading => Get("DiagnosticControlUiLoading");
    public static string DiagnosticControlUiLoadingDetail => Get("DiagnosticControlUiLoadingDetail");
    public static string DiagnosticControlUiEstablishing => Get("DiagnosticControlUiEstablishing");
    public static string DiagnosticControlUiAuthRequired => Get("DiagnosticControlUiAuthRequired");
    public static string DiagnosticControlUiPairingRequired => Get("DiagnosticControlUiPairingRequired");
    public static string DiagnosticControlUiPairingRequiredDetailFormat => Get("DiagnosticControlUiPairingRequiredDetailFormat");
    public static string DiagnosticControlUiOriginRejected => Get("DiagnosticControlUiOriginRejected");
    public static string DiagnosticControlUiOriginRejectedDetailFormat => Get("DiagnosticControlUiOriginRejectedDetailFormat");
    public static string DiagnosticControlUiGatewayWsFailing => Get("DiagnosticControlUiGatewayWsFailing");
    public static string DiagnosticControlUiStateUnavailable => Get("DiagnosticControlUiStateUnavailable");
    public static string DiagnosticInstrumentationLabel => Get("DiagnosticInstrumentationLabel");
    public static string DiagnosticHttpReachableFormat => Get("DiagnosticHttpReachableFormat");
    public static string DiagnosticHttpReachableDetail => Get("DiagnosticHttpReachableDetail");
    public static string DiagnosticAccessRejectedFormat => Get("DiagnosticAccessRejectedFormat");
    public static string DiagnosticAccessRejectedDetail => Get("DiagnosticAccessRejectedDetail");
    public static string DiagnosticGatewayWaitingApproval => Get("DiagnosticGatewayWaitingApproval");
    public static string DiagnosticGatewayWaitingApprovalDetail => Get("DiagnosticGatewayWaitingApprovalDetail");
    public static string DiagnosticAuthRateLimited => Get("DiagnosticAuthRateLimited");
    public static string DiagnosticAuthRateLimitedDetail => Get("DiagnosticAuthRateLimitedDetail");
    public static string DiagnosticMethodRejected => Get("DiagnosticMethodRejected");
    public static string DiagnosticMethodRejectedDetail => Get("DiagnosticMethodRejectedDetail");
    public static string DiagnosticRedirectedFormat => Get("DiagnosticRedirectedFormat");
    public static string DiagnosticRedirectedDetail => Get("DiagnosticRedirectedDetail");
    public static string DiagnosticPathNotFound => Get("DiagnosticPathNotFound");
    public static string DiagnosticPathNotFoundDetail => Get("DiagnosticPathNotFoundDetail");
    public static string DiagnosticGatewayReturnedFormat => Get("DiagnosticGatewayReturnedFormat");
    public static string DiagnosticGatewayReturnedServerFailureDetail => Get("DiagnosticGatewayReturnedServerFailureDetail");
    public static string DiagnosticGatewayReturnedDetail => Get("DiagnosticGatewayReturnedDetail");
    public static string DiagnosticGatewayTimeout => Get("DiagnosticGatewayTimeout");
    public static string DiagnosticGatewayTimeoutDetail => Get("DiagnosticGatewayTimeoutDetail");
    public static string DiagnosticGatewayUnreachable => Get("DiagnosticGatewayUnreachable");
    public static string DiagnosticNetworkProbeFailed => Get("DiagnosticNetworkProbeFailed");
    public static string DiagnosticWebViewNotInitialized => Get("DiagnosticWebViewNotInitialized");
    public static string DiagnosticGatewaySessionAppearsActive => Get("DiagnosticGatewaySessionAppearsActive");
    public static string DiagnosticPageLoadedButEstablishing => Get("DiagnosticPageLoadedButEstablishing");
    public static string DiagnosticCurrentDeviceApprovalDetailFormat => Get("DiagnosticCurrentDeviceApprovalDetailFormat");
    public static string DiagnosticOriginRejectedFailDetailFormat => Get("DiagnosticOriginRejectedFailDetailFormat");
    public static string DiagnosticNoPageLoaded => Get("DiagnosticNoPageLoaded");

    // --- Hosted UI Bridge ---
    public static string BridgeGatewayUiLoaded => Get("BridgeGatewayUiLoaded");
    public static string BridgePageLoading => Get("BridgePageLoading");
    public static string BridgeTokenMissingSummary => Get("BridgeTokenMissingSummary");
    public static string BridgeTokenMissingDetail => Get("BridgeTokenMissingDetail");
    public static string BridgeTokenMismatchSummary => Get("BridgeTokenMismatchSummary");
    public static string BridgeTokenMismatchDetail => Get("BridgeTokenMismatchDetail");
    public static string BridgeDeviceTokenMismatchSummary => Get("BridgeDeviceTokenMismatchSummary");
    public static string BridgeDeviceTokenMismatchDetail => Get("BridgeDeviceTokenMismatchDetail");
    public static string BridgeOriginRejectedSummary => Get("BridgeOriginRejectedSummary");
    public static string BridgeOriginRejectedDetail => Get("BridgeOriginRejectedDetail");
    public static string BridgeTrustedProxyLoopbackSummary => Get("BridgeTrustedProxyLoopbackSummary");
    public static string BridgeTrustedProxyLoopbackDetail => Get("BridgeTrustedProxyLoopbackDetail");
    public static string BridgeMixedAuthSummary => Get("BridgeMixedAuthSummary");
    public static string BridgeMixedAuthDetail => Get("BridgeMixedAuthDetail");
    public static string BridgeTrustedProxyHeaderSummary => Get("BridgeTrustedProxyHeaderSummary");
    public static string BridgeTrustedProxyHeaderDetail => Get("BridgeTrustedProxyHeaderDetail");
    public static string BridgeTrustedProxyOriginSummary => Get("BridgeTrustedProxyOriginSummary");
    public static string BridgeTrustedProxyOriginDetail => Get("BridgeTrustedProxyOriginDetail");
    public static string BridgeRateLimitedSummary => Get("BridgeRateLimitedSummary");
    public static string BridgeRateLimitedDetail => Get("BridgeRateLimitedDetail");
    public static string BridgeInsecureHttpSummary => Get("BridgeInsecureHttpSummary");
    public static string BridgeInsecureHttpDetail => Get("BridgeInsecureHttpDetail");
    public static string BridgePairingSummary => Get("BridgePairingSummary");
    public static string BridgePairingDetail => Get("BridgePairingDetail");
    public static string BridgeAuthRequiredSummary => Get("BridgeAuthRequiredSummary");
    public static string BridgeAuthRequiredDetail => Get("BridgeAuthRequiredDetail");
    public static string BridgeGatewaySessionNotConnectedSummary => Get("BridgeGatewaySessionNotConnectedSummary");
    public static string BridgeGatewaySessionNotConnectedDetail => Get("BridgeGatewaySessionNotConnectedDetail");
    public static string BridgeConnectingSummary => Get("BridgeConnectingSummary");
    public static string BridgeConnectingDetail => Get("BridgeConnectingDetail");
    public static string BridgeConnectedSummary => Get("BridgeConnectedSummary");
}
