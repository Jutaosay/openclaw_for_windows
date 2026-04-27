// Copyright (c) Lanstack @openclaw. All rights reserved.

using System.Text.Json;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using OpenClaw.Helpers;
using OpenClaw.Services;

namespace OpenClaw.Services;

/// <summary>
/// Manages the JavaScript bridge between the hosted Control UI and the native shell.
/// Receives typed events from the page and sends commands back.
/// </summary>
public sealed class HostedUiBridge
{
    private static string? _bridgeScriptResource;

    private static string BridgeScriptResource => _bridgeScriptResource ??= BuildBridgeScript();

    private static string BuildBridgeScript()
    {
        var strings = new Dictionary<string, string>
        {
            ["bridgeGatewayUiLoaded"] = JsString(StringResources.BridgeGatewayUiLoaded),
            ["bridgePageLoading"] = JsString(StringResources.BridgePageLoading),
            ["bridgeTokenMissingSummary"] = JsString(StringResources.BridgeTokenMissingSummary),
            ["bridgeTokenMissingDetail"] = JsString(StringResources.BridgeTokenMissingDetail),
            ["bridgeTokenMismatchSummary"] = JsString(StringResources.BridgeTokenMismatchSummary),
            ["bridgeTokenMismatchDetail"] = JsString(StringResources.BridgeTokenMismatchDetail),
            ["bridgeDeviceTokenMismatchSummary"] = JsString(StringResources.BridgeDeviceTokenMismatchSummary),
            ["bridgeDeviceTokenMismatchDetail"] = JsString(StringResources.BridgeDeviceTokenMismatchDetail),
            ["bridgeOriginRejectedSummary"] = JsString(StringResources.BridgeOriginRejectedSummary),
            ["bridgeOriginRejectedDetail"] = JsString(StringResources.BridgeOriginRejectedDetail),
            ["bridgeTrustedProxyLoopbackSummary"] = JsString(StringResources.BridgeTrustedProxyLoopbackSummary),
            ["bridgeTrustedProxyLoopbackDetail"] = JsString(StringResources.BridgeTrustedProxyLoopbackDetail),
            ["bridgeMixedAuthSummary"] = JsString(StringResources.BridgeMixedAuthSummary),
            ["bridgeMixedAuthDetail"] = JsString(StringResources.BridgeMixedAuthDetail),
            ["bridgeTrustedProxyHeaderSummary"] = JsString(StringResources.BridgeTrustedProxyHeaderSummary),
            ["bridgeTrustedProxyHeaderDetail"] = JsString(StringResources.BridgeTrustedProxyHeaderDetail),
            ["bridgeTrustedProxyOriginSummary"] = JsString(StringResources.BridgeTrustedProxyOriginSummary),
            ["bridgeTrustedProxyOriginDetail"] = JsString(StringResources.BridgeTrustedProxyOriginDetail),
            ["bridgeRateLimitedSummary"] = JsString(StringResources.BridgeRateLimitedSummary),
            ["bridgeRateLimitedDetail"] = JsString(StringResources.BridgeRateLimitedDetail),
            ["bridgeInsecureHttpSummary"] = JsString(StringResources.BridgeInsecureHttpSummary),
            ["bridgeInsecureHttpDetail"] = JsString(StringResources.BridgeInsecureHttpDetail),
            ["bridgePairingSummary"] = JsString(StringResources.BridgePairingSummary),
            ["bridgePairingDetail"] = JsString(StringResources.BridgePairingDetail),
            ["bridgeAuthRequiredSummary"] = JsString(StringResources.BridgeAuthRequiredSummary),
            ["bridgeAuthRequiredDetail"] = JsString(StringResources.BridgeAuthRequiredDetail),
            ["bridgeGatewaySessionNotConnectedSummary"] = JsString(StringResources.BridgeGatewaySessionNotConnectedSummary),
            ["bridgeGatewaySessionNotConnectedDetail"] = JsString(StringResources.BridgeGatewaySessionNotConnectedDetail),
            ["bridgeConnectingSummary"] = JsString(StringResources.BridgeConnectingSummary),
            ["bridgeConnectingDetail"] = JsString(StringResources.BridgeConnectingDetail),
            ["bridgeConnectedSummary"] = JsString(StringResources.BridgeConnectedSummary),
        };

        return $$"""
(() => {
  const KIND = 'openclaw-control-ui-status';
  const SESSION_READY_KIND = 'openclaw-session-ready';
  const GAP_KIND = 'openclaw-event-gap';
  const STRINGS = {
    bridgeGatewayUiLoaded: '{{strings["bridgeGatewayUiLoaded"]}}',
    bridgePageLoading: '{{strings["bridgePageLoading"]}}',
    bridgeTokenMissingSummary: '{{strings["bridgeTokenMissingSummary"]}}',
    bridgeTokenMissingDetail: '{{strings["bridgeTokenMissingDetail"]}}',
    bridgeTokenMismatchSummary: '{{strings["bridgeTokenMismatchSummary"]}}',
    bridgeTokenMismatchDetail: '{{strings["bridgeTokenMismatchDetail"]}}',
    bridgeDeviceTokenMismatchSummary: '{{strings["bridgeDeviceTokenMismatchSummary"]}}',
    bridgeDeviceTokenMismatchDetail: '{{strings["bridgeDeviceTokenMismatchDetail"]}}',
    bridgeOriginRejectedSummary: '{{strings["bridgeOriginRejectedSummary"]}}',
    bridgeOriginRejectedDetail: '{{strings["bridgeOriginRejectedDetail"]}}',
    bridgeTrustedProxyLoopbackSummary: '{{strings["bridgeTrustedProxyLoopbackSummary"]}}',
    bridgeTrustedProxyLoopbackDetail: '{{strings["bridgeTrustedProxyLoopbackDetail"]}}',
    bridgeMixedAuthSummary: '{{strings["bridgeMixedAuthSummary"]}}',
    bridgeMixedAuthDetail: '{{strings["bridgeMixedAuthDetail"]}}',
    bridgeTrustedProxyHeaderSummary: '{{strings["bridgeTrustedProxyHeaderSummary"]}}',
    bridgeTrustedProxyHeaderDetail: '{{strings["bridgeTrustedProxyHeaderDetail"]}}',
    bridgeTrustedProxyOriginSummary: '{{strings["bridgeTrustedProxyOriginSummary"]}}',
    bridgeTrustedProxyOriginDetail: '{{strings["bridgeTrustedProxyOriginDetail"]}}',
    bridgeRateLimitedSummary: '{{strings["bridgeRateLimitedSummary"]}}',
    bridgeRateLimitedDetail: '{{strings["bridgeRateLimitedDetail"]}}',
    bridgeInsecureHttpSummary: '{{strings["bridgeInsecureHttpSummary"]}}',
    bridgeInsecureHttpDetail: '{{strings["bridgeInsecureHttpDetail"]}}',
    bridgePairingSummary: '{{strings["bridgePairingSummary"]}}',
    bridgePairingDetail: '{{strings["bridgePairingDetail"]}}',
    bridgeAuthRequiredSummary: '{{strings["bridgeAuthRequiredSummary"]}}',
    bridgeAuthRequiredDetail: '{{strings["bridgeAuthRequiredDetail"]}}',
    bridgeGatewaySessionNotConnectedSummary: '{{strings["bridgeGatewaySessionNotConnectedSummary"]}}',
    bridgeGatewaySessionNotConnectedDetail: '{{strings["bridgeGatewaySessionNotConnectedDetail"]}}',
    bridgeConnectingSummary: '{{strings["bridgeConnectingSummary"]}}',
    bridgeConnectingDetail: '{{strings["bridgeConnectingDetail"]}}',
    bridgeConnectedSummary: '{{strings["bridgeConnectedSummary"]}}'
  };

  const isVisible = (el) => {
    if (!el) return false;
    const style = window.getComputedStyle(el);
    if (style.display === 'none' || style.visibility === 'hidden') return false;
    const rect = el.getBoundingClientRect();
    return rect.width > 0 && rect.height > 0;
  };

  const textOf = (el) => {
    return [el?.innerText, el?.textContent]
      .filter(Boolean)
      .join(' ')
      .replace(/\s+/g, ' ')
      .trim();
  };

  const labelOf = (el) => {
    return [
      el?.getAttribute?.('aria-label'),
      el?.getAttribute?.('title'),
      textOf(el)
    ].filter(Boolean).join(' ').trim();
  };

  const isEditableElement = (el) => {
    if (!el) return false;
    if (el instanceof HTMLTextAreaElement) return true;
    if (el instanceof HTMLInputElement) {
      const type = (el.type || 'text').toLowerCase();
      return !['button', 'checkbox', 'color', 'file', 'hidden', 'image', 'radio', 'range', 'reset', 'submit'].includes(type);
    }

    const role = el.getAttribute?.('role') || '';
    return el.isContentEditable || role === 'textbox';
  };

  const compactText = (value) => (value || '').replace(/\s+/g, ' ').trim();

  const collectSignalText = () => {
    const selectors = [
      '[role="alert"]', '[role="status"]', '[aria-live]',
      '[data-status]', '[data-state]', '[data-busy]',
      '[class*="auth"]', '[class*="login"]', '[class*="signin"]',
      '[class*="error"]', '[class*="pair"]', '[class*="origin"]',
      '[class*="proxy"]', '[class*="connect"]', '[class*="disconnect"]'
    ];
    const fragments = [];
    const seen = new Set();
    let totalLength = 0;

    for (const selector of selectors) {
      for (const element of document.querySelectorAll(selector)) {
        if (!isVisible(element)) continue;
        const text = compactText(textOf(element)).toLowerCase();
        if (!text) continue;
        const normalized = text.length > 240 ? `${text.slice(0, 240)}...` : text;
        if (seen.has(normalized)) continue;
        seen.add(normalized);
        fragments.push(normalized);
        totalLength += normalized.length;
        if (fragments.length >= 6 || totalLength >= 900) break;
      }
      if (fragments.length >= 6 || totalLength >= 900) break;
    }

    return fragments.join(' ');
  };

  const matchAny = (haystack, needles) => needles.find((needle) => haystack.includes(needle)) || '';

  const modelPattern = /\b(?:gpt|o\d|claude|gemini|qwen|deepseek|llama|mistral|glm|yi|command|grok|codex|kimi|moonshot)[a-z0-9._:+/-]*\b/i;

  const sanitizeModelLabel = (text) => {
    const normalized = compactText(text)
      .replace(/\b(?:current|selected|default)\s+model\b[:\s-]*/ig, '')
      .replace(/\bmodel\b[:\s-]*/ig, '')
      .replace(/\bprovider\b[:\s-]*/ig, '')
      .replace(/\s+\|\s+/g, ' | ')
      .trim();

    if (!normalized) return '';
    if (normalized.length <= 32 && modelPattern.test(normalized)) return normalized;

    const segment = normalized
      .split(/(?:\s{3,}|\n|\||,)/)
      .map((part) => compactText(part))
      .find((part) => modelPattern.test(part));

    if (segment) return segment.length <= 32 ? segment : segment.slice(0, 31).trimEnd();
    const match = normalized.match(modelPattern);
    return match ? match[0] : '';
  };

  const readCurrentModel = () => {
    const candidates = [];
    const selectionBoostOf = (el) => {
      const selected = [
        el?.getAttribute?.('aria-selected'),
        el?.getAttribute?.('aria-checked'),
        el?.getAttribute?.('aria-pressed'),
        el?.getAttribute?.('data-selected'),
        el?.getAttribute?.('data-state')
      ].filter(Boolean).join(' ').toLowerCase();
      return /true|selected|checked|active|current/.test(selected) ? 18 : 0;
    };

    const viewportBoostOf = (el) => {
      if (!el || typeof el.getBoundingClientRect !== 'function') return 0;
      const top = el.getBoundingClientRect().top;
      return Number.isFinite(top) && top >= 0 && top <= 260 ? 8 : 0;
    };

    const pushCandidate = (text, score, el) => {
      const label = sanitizeModelLabel(text);
      if (!label) return;
      candidates.push({ label, score: score + selectionBoostOf(el) + viewportBoostOf(el) });
    };

    Array.from(document.querySelectorAll('[data-current-model], [data-selected-model], [data-model-name]'))
      .filter((el) => isVisible(el))
      .forEach((el) => pushCandidate(textOf(el), 120, el));

    Array.from(document.querySelectorAll('select'))
      .filter((el) => isVisible(el))
      .forEach((el) => {
        const selectedText = Array.from(el.selectedOptions || [])
          .map((option) => option.textContent || '')
          .join(' ');
        const combined = `${labelOf(el)} ${selectedText}`.trim();
        if (/\bmodel\b/i.test(combined) || modelPattern.test(selectedText)) {
          pushCandidate(selectedText || combined, /\bmodel\b/i.test(combined) ? 115 : 90, el);
        }
      });

    Array.from(document.querySelectorAll('[role="combobox"], button[aria-haspopup="listbox"], button, [role="button"], input[type="text"], input:not([type])'))
      .filter((el) => isVisible(el))
      .forEach((el) => {
        const rawValue = 'value' in el && typeof el.value === 'string' ? el.value : '';
        const combined = [labelOf(el), rawValue, el.getAttribute?.('placeholder')].filter(Boolean).join(' ').trim();
        if (!/\bmodel\b/i.test(combined) && !modelPattern.test(rawValue) && !modelPattern.test(textOf(el))) return;
        const score = /\bmodel\b/i.test(combined) ? 105 : 80;
        pushCandidate(rawValue || textOf(el) || combined, score, el);
      });

    if (candidates.length === 0) return '';
    candidates.sort((left, right) => {
      if (right.score !== left.score) return right.score - left.score;
      return left.label.length - right.label.length;
    });
    return candidates[0].label;
  };

  const detectBusyFromApi = () => {
    const candidates = [
      window.chat, window.__openclaw?.chat, window.__OPENCLAW__?.chat,
      window.__APP__?.chat, window.app?.chat
    ];
    const busyKeys = ['isRunning', 'running', 'isBusy', 'busy', 'isStreaming', 'streaming', 'isGenerating', 'generating'];
    return candidates.some((candidate) =>
      candidate && busyKeys.some((key) => typeof candidate[key] === 'boolean' && candidate[key]));
  };

  const inspectControlUi = () => {
    const url = window.location ? window.location.href : '';
    const lowerUrl = url.toLowerCase();
    const text = collectSignalText();
    const activeElement = document.activeElement;
    const inputFocused = isEditableElement(activeElement) && isVisible(activeElement);

    const authMatch = matchAny(text, [
      'authentication required', 'authorization failed', 'unauthorized',
      'access denied', 'token required', 'password required',
      'session expired', 'sign in', 'log in', 'login required'
    ]);
    const tokenMissingMatch = matchAny(text, [
      'auth_token_missing', 'token missing', 'missing shared token'
    ]);
    const tokenMismatchMatch = matchAny(text, [
      'auth_token_mismatch', 'token mismatch', 'shared token did not match',
      'canretrywithdevicetoken'
    ]);
    const deviceTokenMismatchMatch = matchAny(text, [
      'auth_device_token_mismatch', 'device token mismatch',
      'cached per-device token is stale', 'stale or revoked device token'
    ]);
    const pairingMatch = matchAny(text, [
      'pairing required', 'pair this device', 'device approval required',
      'device not paired', 'disconnected (1008)'
    ]);
    const originMatch = matchAny(text, [
      'origin not allowed', 'origin rejected', 'allowed origins',
      'forbidden origin', 'trusted proxy'
    ]);
    const trustedProxyLoopbackMatch = matchAny(text, [
      'trusted_proxy_loopback_source', 'loopback-source trusted-proxy',
      'same-host loopback reverse proxies do not satisfy trusted-proxy auth',
      'same-host loopback reverse proxy', 'trusted-proxy auth rejects loopback-source requests'
    ]);
    const mixedTrustedProxyTokenMatch = matchAny(text, [
      'mixed_trusted_proxy_token', 'mixed token config',
      'both a gateway.auth.token', 'trusted-proxy mode are active at the same time',
      'remove the shared token when using trusted-proxy mode'
    ]);
    const trustedProxyIdentityHeaderMatch = matchAny(text, [
      'trusted_proxy_user_missing', 'trusted_proxy_user_not_allowed',
      'trustedproxy_missing_header', 'missing_header',
      'identity headers', 'required header wasn\'t present'
    ]);
    const trustedProxyOriginRejectedMatch = matchAny(text, [
      'trusted_proxy_origin_not_allowed', 'origin did not pass control ui origin checks'
    ]);
    const rateLimitMatch = matchAny(text, [
      'retry later', 'too many failed auth attempts', 'retry-after',
      'rate limited', 'rate limit'
    ]);
    const gatewayErrorMatch = matchAny(text, [
      'unable to connect', 'connection lost', 'gateway unavailable',
      'failed to connect', 'websocket closed', 'disconnect code'
    ]);
    const connectingMatch = matchAny(text, [
      'connecting to gateway', 'waiting for gateway',
      'reconnecting', 'establishing connection'
    ]);
    const isNonLocalHttp =
      lowerUrl.startsWith('http://') &&
      !/\/\/(?:127\.0\.0\.1|localhost|\[::1\])(?::|\/|$)/.test(lowerUrl);
    const insecureHttpMatch = matchAny(text, [
      'non-secure context', 'webcrypto', 'allowinsecureauth',
      'dangerouslydisabledeviceauth', 'device identity checks',
      'use https', 'tailscale serve'
    ]);

    const shellDetected =
      hasVisibleElement('textarea, input:not([type]), input[type="text"], [contenteditable="true"], [role="textbox"]') ||
      hasVisibleElement('button, [role="button"], nav, aside, [role="navigation"]', (el) => {
        const label = labelOf(el).toLowerCase();
        return /stop|abort|dashboard|settings|sessions|workers|models|new chat|history/.test(label);
      });

    const busyByButton = hasVisibleElement('button, [role="button"], [aria-label], [title]', (el) => {
      const label = labelOf(el).toLowerCase();
      return /\b(stop|abort|cancel)\b/.test(label);
    });
    const busyBySignals = hasVisibleElement(
      '[aria-busy="true"], [role="progressbar"], [data-busy="true"], [data-running="true"], [data-state="running"], [data-state="streaming"], [data-status="running"], [data-status="streaming"]');
    const isBusy = detectBusyFromApi() || busyByButton || busyBySignals;
    const workState = isBusy ? 'busy' : shellDetected ? 'idle' : 'unknown';

    let phase = 'page_loaded';
    let summary = STRINGS.bridgeGatewayUiLoaded;
    let detail = '';

    if (!document.body || document.readyState === 'loading') {
      phase = 'loading';
      summary = STRINGS.bridgePageLoading;
    } else if (tokenMissingMatch) {
      phase = 'auth_required';
      summary = STRINGS.bridgeTokenMissingSummary;
      detail = STRINGS.bridgeTokenMissingDetail;
    } else if (tokenMismatchMatch) {
      phase = 'auth_required';
      summary = STRINGS.bridgeTokenMismatchSummary;
      detail = STRINGS.bridgeTokenMismatchDetail;
    } else if (deviceTokenMismatchMatch) {
      phase = 'auth_required';
      summary = STRINGS.bridgeDeviceTokenMismatchSummary;
      detail = STRINGS.bridgeDeviceTokenMismatchDetail;
    } else if (originMatch) {
      phase = 'origin_rejected';
      summary = STRINGS.bridgeOriginRejectedSummary;
      detail = STRINGS.bridgeOriginRejectedDetail;
    } else if (trustedProxyLoopbackMatch) {
      phase = 'auth_required';
      summary = STRINGS.bridgeTrustedProxyLoopbackSummary;
      detail = STRINGS.bridgeTrustedProxyLoopbackDetail;
    } else if (mixedTrustedProxyTokenMatch) {
      phase = 'auth_required';
      summary = STRINGS.bridgeMixedAuthSummary;
      detail = STRINGS.bridgeMixedAuthDetail;
    } else if (trustedProxyIdentityHeaderMatch) {
      phase = 'auth_required';
      summary = STRINGS.bridgeTrustedProxyHeaderSummary;
      detail = STRINGS.bridgeTrustedProxyHeaderDetail;
    } else if (trustedProxyOriginRejectedMatch) {
      phase = 'origin_rejected';
      summary = STRINGS.bridgeTrustedProxyOriginSummary;
      detail = STRINGS.bridgeTrustedProxyOriginDetail;
    } else if (rateLimitMatch) {
      phase = 'auth_required';
      summary = STRINGS.bridgeRateLimitedSummary;
      detail = STRINGS.bridgeRateLimitedDetail;
    } else if (isNonLocalHttp && insecureHttpMatch) {
      phase = 'gateway_error';
      summary = STRINGS.bridgeInsecureHttpSummary;
      detail = STRINGS.bridgeInsecureHttpDetail;
    } else if (pairingMatch) {
      phase = 'pairing_required';
      summary = STRINGS.bridgePairingSummary;
      detail = STRINGS.bridgePairingDetail;
    } else if (authMatch || /\/(login|signin|auth)(\/|$|\?)/.test(lowerUrl)) {
      phase = 'auth_required';
      summary = STRINGS.bridgeAuthRequiredSummary;
      detail = STRINGS.bridgeAuthRequiredDetail;
    } else if (gatewayErrorMatch) {
      phase = 'gateway_error';
      summary = STRINGS.bridgeGatewaySessionNotConnectedSummary;
      detail = STRINGS.bridgeGatewaySessionNotConnectedDetail;
    } else if (connectingMatch) {
      phase = 'gateway_connecting';
      summary = STRINGS.bridgeConnectingSummary;
      detail = STRINGS.bridgeConnectingDetail;
    } else if (shellDetected) {
      phase = 'connected';
      summary = STRINGS.bridgeConnectedSummary;
    }

    return {
      kind: KIND, phase, summary, detail, url, shellDetected, isBusy, inputFocused, workState,
      currentModel: readCurrentModel()
    };
  };

  const hasVisibleElement = (selector, predicate) => {
    return Array.from(document.querySelectorAll(selector))
      .some((el) => isVisible(el) && (!predicate || predicate(el)));
  };

  const bridgeTargets = () => [
    window.chat,
    window.__openclaw?.chat,
    window.__OPENCLAW__?.chat,
    window.__APP__?.chat,
    window.app?.chat,
    window.__openclaw,
    window.__OPENCLAW__,
    window.__APP__,
    window.app
  ].filter(Boolean);

  const invokeBridgeMethod = async (methodNames, payload) => {
    for (const target of bridgeTargets()) {
      for (const methodName of methodNames) {
        const method = target?.[methodName];
        if (typeof method !== 'function') continue;

        try {
          const result = method.call(target, payload);
          if (result && typeof result.then === 'function') {
            await result;
          }
          return true;
        } catch {
        }
      }
    }

    return false;
  };

  const dispatchBridgeEvent = (command, payload) => {
    const detail = { command, payload };
    let dispatched = false;

    for (const target of [window, document]) {
      if (!target?.dispatchEvent) continue;
      target.dispatchEvent(new CustomEvent('openclaw:host-command', { detail }));
      target.dispatchEvent(new CustomEvent(`openclaw:${command}`, { detail }));
      dispatched = true;
    }

    return dispatched;
  };

  // Bridge state
  let lastSeq = null;
  let lastStateVersion = null;
  let sessionReadyEmitted = false;

  // Post status
  let lastSerialized = '';
  const postStatus = (snapshot = inspectControlUi()) => {
    if (!window.chrome?.webview?.postMessage) return;
    const serialized = JSON.stringify(snapshot);
    if (serialized === lastSerialized) return;
    lastSerialized = serialized;
    window.chrome.webview.postMessage(snapshot);
  };

  // Detect session ready
  const checkSessionReady = (snapshot = inspectControlUi()) => {
    if (sessionReadyEmitted) return;

    if (snapshot.phase === 'connected' && snapshot.shellDetected) {
      sessionReadyEmitted = true;
      window.chrome.webview.postMessage({
        kind: SESSION_READY_KIND,
        detectedAt: new Date().toISOString(),
        model: snapshot.currentModel,
        uri: snapshot.url
      });
    }
  };

  // Detect event gap (if page exposes seq)
  const checkForGap = (currentSeq, stateVersion) => {
    if (lastSeq === null) {
      lastSeq = currentSeq;
      lastStateVersion = stateVersion;
      return null;
    }

    if (currentSeq !== lastSeq + 1) {
      const gap = {
        kind: GAP_KIND,
        expectedSeq: lastSeq + 1,
        gotSeq: currentSeq,
        lastStateVersion: lastStateVersion,
        currentStateVersion: stateVersion,
        detectedAt: new Date().toISOString()
      };
      lastSeq = currentSeq;
      lastStateVersion = stateVersion;
      return gap;
    }

    lastSeq = currentSeq;
    lastStateVersion = stateVersion;
    return null;
  };

  // Expose bridge API to page
  const onCommand = async (message) => {
    const command = message?.command || '';
    const payload = message?.payload;

    switch (command) {
      case 'refresh_session': {
        const handled = await invokeBridgeMethod(
          ['refreshSession', 'reloadSession', 'reconnect', 'connect', 'resume'],
          payload);
        const snapshot = inspectControlUi();
        postStatus(snapshot);
        checkSessionReady(snapshot);
        return handled || dispatchBridgeEvent(command, payload);
      }
      case 'fetch_recent_messages': {
        const handled = await invokeBridgeMethod(
          ['fetchRecentMessages', 'loadRecentMessages', 'syncMessages', 'sync'],
          payload);
        postStatus(inspectControlUi());
        return handled || dispatchBridgeEvent(command, payload);
      }
      case 'lightweight_sync': {
        const handled = await invokeBridgeMethod(
          ['sync', 'refresh', 'refreshSession', 'fetchRecentMessages', 'loadRecentMessages'],
          payload);
        const snapshot = inspectControlUi();
        postStatus(snapshot);
        checkSessionReady(snapshot);
        return handled || dispatchBridgeEvent(command, payload);
      }
      case 'reconnect_intent': {
        const handled = await invokeBridgeMethod(
          ['reconnect', 'connect', 'resume', 'refreshSession'],
          payload);
        postStatus(inspectControlUi());
        return handled || dispatchBridgeEvent(command, payload);
      }
      default:
        return dispatchBridgeEvent(command, payload);
    }
  };

  window.__openClawHostBridge = {
    inspect: inspectControlUi,
    sendStatus: postStatus,
    onCommand,
    reportSeq: (seq, stateVersion) => {
      const gap = checkForGap(seq, stateVersion);
      if (gap) {
        window.chrome.webview.postMessage(gap);
      }
      postStatus();
    },
    reportSessionReady: () => {
      if (!sessionReadyEmitted) {
        sessionReadyEmitted = true;
        const snapshot = inspectControlUi();
        window.chrome.webview.postMessage({
          kind: SESSION_READY_KIND,
          detectedAt: new Date().toISOString(),
          model: snapshot.currentModel,
          uri: snapshot.url
        });
      }
    }
  };

  // Schedule posts
  let scheduledPost = 0;
  const schedule = () => {
    if (scheduledPost) return;
    scheduledPost = window.setTimeout(() => {
      scheduledPost = 0;
      const snapshot = inspectControlUi();
      postStatus(snapshot);
      checkSessionReady(snapshot);
    }, document.visibilityState === 'visible' ? 180 : 1200);
  };

  // Observe DOM changes
  const observer = new MutationObserver(schedule);
  if (document.documentElement) {
    observer.observe(document.documentElement, {
      childList: true, subtree: true,
      attributes: true,
      attributeFilter: ['aria-busy', 'data-busy', 'data-running', 'data-state', 'data-status', 'aria-label', 'title', 'class']
    });
  }

  // History changes
  const wrapHistory = (methodName) => {
    const original = history[methodName];
    if (typeof original !== 'function') return;
    history[methodName] = function (...args) {
      const result = original.apply(this, args);
      schedule();
      return result;
    };
  };
  wrapHistory('pushState');
  wrapHistory('replaceState');

  // Events
  window.addEventListener('popstate', schedule);
  window.addEventListener('load', schedule);
  document.addEventListener('readystatechange', schedule);

  let pollInterval = 8000;
  let pollTimer = 0;
  const tick = () => {
    const snapshot = inspectControlUi();
    postStatus(snapshot);
    checkSessionReady(snapshot);
    pollInterval = snapshot.phase === 'connected' ? 15000 : 4000;
    pollTimer = window.setTimeout(tick, pollInterval);
  };

  const restartPolling = (interval = pollInterval) => {
    if (pollTimer) {
      window.clearTimeout(pollTimer);
    }

    pollInterval = interval;
    pollTimer = window.setTimeout(tick, pollInterval);
  };

  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible') {
      schedule();
      restartPolling(1200);
      return;
    }

    restartPolling(15000);
  });

  window.addEventListener('focus', () => {
    schedule();
    restartPolling(1200);
  });

  window.addEventListener('blur', () => {
    restartPolling(12000);
  });

  restartPolling();
  schedule();
})();
""";
    }

    private static string JsString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }

    private WebView2? _webView;
    private CoreWebView2? _coreWebView;
    private bool _isInitialized;

    /// <summary>
    /// Raised when the hosted UI reports session ready.
    /// </summary>
    public event Action<SessionReadyEventArgs>? SessionReady;

    /// <summary>
    /// Raised when an event gap is detected.
    /// </summary>
    public event Action<EventGapEventArgs>? EventGapDetected;

    /// <summary>
    /// Gets whether the bridge is initialized.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Gets the last known event sequence number.
    /// </summary>
    public long? LastKnownEventSeq { get; private set; }

    /// <summary>
    /// Gets the last known state version.
    /// </summary>
    public string? LastKnownStateVersion { get; private set; }

    /// <summary>
    /// Gets whether session ready has been emitted.
    /// </summary>
    public bool IsSessionReadyEmitted { get; private set; }

    /// <summary>
    /// Initializes the bridge by injecting the JavaScript payload.
    /// </summary>
    public async Task InitializeAsync(WebView2 webView)
    {
        var previousCoreWebView = GetCoreWebView();
        if (previousCoreWebView is not null)
        {
            previousCoreWebView.WebMessageReceived -= OnWebMessageReceived;
        }

        _webView = webView;
        _coreWebView = null;
        LastKnownEventSeq = null;
        LastKnownStateVersion = null;
        IsSessionReadyEmitted = false;

        try
        {
            var coreWebView = TryGetCoreWebView2(_webView);
            if (coreWebView is null)
            {
                throw new InvalidOperationException("Bridge cannot initialize before CoreWebView2 is available.");
            }

            _coreWebView = coreWebView;

            await coreWebView.AddScriptToExecuteOnDocumentCreatedAsync(BridgeScriptResource);
            coreWebView.WebMessageReceived += OnWebMessageReceived;
            _isInitialized = true;
            App.Logger.Info("HostedUiBridge initialized.");
        }
        catch (Exception ex)
        {
            App.Logger.Error($"HostedUiBridge initialization failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Sends a command to the hosted UI.
    /// </summary>
    public async Task<bool> SendCommandAsync(string command, object? payload = null)
    {
        var coreWebView = GetCoreWebView();
        if (coreWebView is null)
        {
            App.Logger.Warning($"HostedUiBridge command skipped before initialization: {command}");
            return false;
        }

        try
        {
            var message = new { kind = "command", command, payload };
            var json = JsonSerializer.Serialize(message);
            var script = $"(async () => await window.__openClawHostBridge?.onCommand?.({json}) ?? false)()";
            var raw = await coreWebView.ExecuteScriptAsync(script);
            return bool.TryParse(raw?.Trim('"'), out var handled) && handled;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            App.Logger.Warning($"HostedUiBridge command '{command}' failed while WebView2 was unavailable: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Requests the hosted UI to refresh its session.
    /// </summary>
    public async Task<bool> RequestSessionRefreshAsync()
    {
        return await SendCommandAsync("refresh_session");
    }

    /// <summary>
    /// Requests the hosted UI to fetch recent messages.
    /// </summary>
    public async Task<bool> RequestRecentMessagesAsync()
    {
        return await SendCommandAsync("fetch_recent_messages");
    }

    /// <summary>
    /// Requests the hosted UI to perform a lightweight sync.
    /// </summary>
    public async Task<bool> RequestLightweightSyncAsync()
    {
        return await SendCommandAsync("lightweight_sync");
    }

    /// <summary>
    /// Notifies the hosted UI of reconnect intent.
    /// </summary>
    public async Task<bool> NotifyReconnectIntentAsync()
    {
        return await SendCommandAsync("reconnect_intent");
    }

    private void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            var message = args.WebMessageAsJson;
            using var document = JsonDocument.Parse(message);
            var root = document.RootElement;

            var kind = root.TryGetProperty("kind", out var kindProp) ? kindProp.GetString() : null;

            if (string.Equals(kind, "openclaw-session-ready", StringComparison.Ordinal))
            {
                IsSessionReadyEmitted = true;
                var eventArgs = ParseSessionReadyEventArgs(root);
                SessionReady?.Invoke(eventArgs);
            }
            else if (string.Equals(kind, "openclaw-event-gap", StringComparison.Ordinal))
            {
                var eventArgs = ParseEventGapEventArgs(root);
                LastKnownEventSeq = eventArgs.GotSeq;
                LastKnownStateVersion = eventArgs.CurrentStateVersion;
                EventGapDetected?.Invoke(eventArgs);
            }
        }
        catch (Exception ex)
        {
            App.Logger.Warning($"Failed to process bridge message: {ex.Message}");
        }
    }

    private static SessionReadyEventArgs ParseSessionReadyEventArgs(JsonElement root)
    {
        var detectedAt = GetString(root, "detectedAt");
        var model = GetString(root, "model");
        var uri = GetString(root, "uri");

        return new SessionReadyEventArgs(detectedAt, model, uri);
    }

    private static EventGapEventArgs ParseEventGapEventArgs(JsonElement root)
    {
        var expectedSeq = root.TryGetProperty("expectedSeq", out var prop) ? prop.GetInt64() : 0L;
        var gotSeq = root.TryGetProperty("gotSeq", out prop) ? prop.GetInt64() : 0L;
        var lastStateVersion = GetString(root, "lastStateVersion");
        var currentStateVersion = GetString(root, "currentStateVersion");
        var detectedAt = GetString(root, "detectedAt");

        return new EventGapEventArgs(expectedSeq, gotSeq, lastStateVersion, currentStateVersion, detectedAt);
    }

    private static string GetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    /// <summary>
    /// Cleans up the bridge resources.
    /// </summary>
    public void Dispose()
    {
        var coreWebView = GetCoreWebView();
        if (coreWebView is not null)
        {
            coreWebView.WebMessageReceived -= OnWebMessageReceived;
        }
        _webView = null;
        _coreWebView = null;
        _isInitialized = false;
        LastKnownEventSeq = null;
        LastKnownStateVersion = null;
        IsSessionReadyEmitted = false;
    }

    private static CoreWebView2? TryGetCoreWebView2(WebView2? webView)
    {
        if (webView is null)
        {
            return null;
        }

        try
        {
            return webView.CoreWebView2;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            return null;
        }
    }

    private CoreWebView2? GetCoreWebView()
    {
        if (_coreWebView is not null)
        {
            return _coreWebView;
        }

        _coreWebView = TryGetCoreWebView2(_webView);
        return _coreWebView;
    }
}

/// <summary>
/// Event args for session ready event.
/// </summary>
public record SessionReadyEventArgs(
    string DetectedAt,
    string Model,
    string Uri
);

/// <summary>
/// Event args for event gap detection.
/// </summary>
public record EventGapEventArgs(
    long ExpectedSeq,
    long GotSeq,
    string? LastStateVersion,
    string? CurrentStateVersion,
    string DetectedAt
);
