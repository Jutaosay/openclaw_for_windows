// Copyright (c) Lanstack @openclaw. All rights reserved.

using System.Runtime.InteropServices;

namespace OpenClaw.Services;

internal sealed class TrayIconService : IDisposable
{
    private const string WindowClassPrefix = "OpenClaw.TrayIcon.";
    private const uint TrayIconId = 1;
    private const uint TrayCallbackMessage = WindowMessages.App + 1;
    private const uint NotifyIconVersion4 = 4;
    private const int TooltipMaxLength = 127;

    private const int MenuOpenOpenClaw = 100;
    private const int MenuSettings = 101;
    private const int MenuExit = 102;

    private readonly IAppLogger _logger;
    private readonly WindowProcedure _windowProcedure;
    private readonly string _windowClassName = WindowClassPrefix + Guid.NewGuid().ToString("N");
    private IntPtr _messageWindowHandle;
    private IntPtr _iconHandle;
    private bool _isDisposed;
    private bool _isIconAdded;
    private string _statusText = "WAIT";

    public TrayIconService(string iconPath, IAppLogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(iconPath);
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _windowProcedure = OnWindowMessage;
        Initialize(iconPath);
    }

    public event Action? ToggleVisibilityRequested;

    public event Action? OpenRequested;

    public event Action? OpenSettingsRequested;

    public event Action? ExitRequested;

    public bool IsAvailable => _isIconAdded;

    public void UpdateStatus(string statusText)
    {
        if (_isDisposed)
        {
            return;
        }

        _statusText = string.IsNullOrWhiteSpace(statusText) ? "WAIT" : statusText.Trim();
        if (!IsAvailable)
        {
            return;
        }

        var data = CreateNotifyIconData(NotifyIconFlags.Tip);
        ShellNotifyIcon(NotifyIconMessage.Modify, ref data);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        if (_messageWindowHandle != IntPtr.Zero)
        {
            if (_isIconAdded)
            {
                var data = CreateNotifyIconData(0);
                ShellNotifyIcon(NotifyIconMessage.Delete, ref data);
                _isIconAdded = false;
            }

            DestroyWindow(_messageWindowHandle);
            _messageWindowHandle = IntPtr.Zero;
        }

        if (_iconHandle != IntPtr.Zero)
        {
            DestroyIcon(_iconHandle);
            _iconHandle = IntPtr.Zero;
        }

        UnregisterClass(_windowClassName, GetModuleHandle(null));
    }

    private void Initialize(string iconPath)
    {
        var moduleHandle = GetModuleHandle(null);
        var windowClass = new WindowClassEx
        {
            cbSize = (uint)Marshal.SizeOf<WindowClassEx>(),
            lpfnWndProc = _windowProcedure,
            hInstance = moduleHandle,
            lpszClassName = _windowClassName,
        };

        if (RegisterClassEx(ref windowClass) == 0)
        {
            _logger.Warning($"Failed to register tray window class: {Marshal.GetLastWin32Error()}");
            return;
        }

        _messageWindowHandle = CreateWindowEx(
            0,
            _windowClassName,
            string.Empty,
            0,
            0,
            0,
            0,
            0,
            IntPtr.Zero,
            IntPtr.Zero,
            moduleHandle,
            IntPtr.Zero);
        if (_messageWindowHandle == IntPtr.Zero)
        {
            _logger.Warning($"Failed to create tray message window: {Marshal.GetLastWin32Error()}");
            return;
        }

        _iconHandle = LoadImage(
            IntPtr.Zero,
            iconPath,
            ImageTypes.Icon,
            0,
            0,
            LoadImageFlags.LoadFromFile | LoadImageFlags.DefaultSize);
        if (_iconHandle == IntPtr.Zero)
        {
            _logger.Warning($"Failed to load tray icon: {Marshal.GetLastWin32Error()}");
            return;
        }

        var data = CreateNotifyIconData(NotifyIconFlags.Message | NotifyIconFlags.Icon | NotifyIconFlags.Tip);
        if (!ShellNotifyIcon(NotifyIconMessage.Add, ref data))
        {
            _logger.Warning($"Failed to add tray icon: {Marshal.GetLastWin32Error()}");
            return;
        }

        _isIconAdded = true;
        data.uTimeoutOrVersion = NotifyIconVersion4;
        ShellNotifyIcon(NotifyIconMessage.SetVersion, ref data);
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == TrayCallbackMessage)
        {
            HandleTrayCallback(LowWord(lParam));
            return IntPtr.Zero;
        }

        return DefWindowProc(hwnd, message, wParam, lParam);
    }

    private void HandleTrayCallback(uint message)
    {
        switch (message)
        {
            case WindowMessages.LeftButtonUp:
            case WindowMessages.LeftButtonDoubleClick:
                ToggleVisibilityRequested?.Invoke();
                break;
            case WindowMessages.RightButtonUp:
            case WindowMessages.ContextMenu:
                ShowContextMenu();
                break;
        }
    }

    private void ShowContextMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero)
        {
            _logger.Warning($"Failed to create tray menu: {Marshal.GetLastWin32Error()}");
            return;
        }

        try
        {
            AppendMenu(menu, MenuFlags.String, MenuOpenOpenClaw, "Open OpenClaw");
            AppendMenu(menu, MenuFlags.String, MenuSettings, "Settings");
            AppendMenu(menu, MenuFlags.Separator, 0, null);
            AppendMenu(menu, MenuFlags.String, MenuExit, "Exit");

            if (!GetCursorPos(out var point))
            {
                _logger.Warning($"Failed to locate tray menu cursor position: {Marshal.GetLastWin32Error()}");
                return;
            }

            SetForegroundWindow(_messageWindowHandle);
            var command = TrackPopupMenu(
                menu,
                TrackPopupMenuFlags.ReturnCommand | TrackPopupMenuFlags.RightButton | TrackPopupMenuFlags.Nonotify,
                point.X,
                point.Y,
                0,
                _messageWindowHandle,
                IntPtr.Zero);
            PostMessage(_messageWindowHandle, WindowMessages.Null, IntPtr.Zero, IntPtr.Zero);

            DispatchMenuCommand(command);
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private void DispatchMenuCommand(int command)
    {
        switch (command)
        {
            case MenuOpenOpenClaw:
                OpenRequested?.Invoke();
                break;
            case MenuSettings:
                OpenSettingsRequested?.Invoke();
                break;
            case MenuExit:
                ExitRequested?.Invoke();
                break;
        }
    }

    private NotifyIconData CreateNotifyIconData(uint flags)
    {
        return new NotifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
            hWnd = _messageWindowHandle,
            uID = TrayIconId,
            uFlags = flags,
            uCallbackMessage = TrayCallbackMessage,
            hIcon = _iconHandle,
            szTip = CreateTooltip(),
            szInfo = string.Empty,
            szInfoTitle = string.Empty,
        };
    }

    private string CreateTooltip()
    {
        var tooltip = $"OpenClaw - {_statusText}";
        return tooltip.Length <= TooltipMaxLength
            ? tooltip
            : tooltip[..TooltipMaxLength];
    }

    private static uint LowWord(IntPtr value) =>
        (uint)(value.ToInt64() & 0xFFFF);

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShellNotifyIcon(uint dwMessage, ref NotifyIconData lpData);

    [DllImport("user32.dll", EntryPoint = "RegisterClassExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WindowClassEx lpwcx);

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "UnregisterClassW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "LoadImageW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", EntryPoint = "AppendMenuW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, int uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int TrackPopupMenu(
        IntPtr hMenu,
        uint uFlags,
        int x,
        int y,
        int nReserved,
        IntPtr hWnd,
        IntPtr prcRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr WindowProcedure(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClassEx
    {
        public uint cbSize;
        public uint style;
        public WindowProcedure lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NativePoint(int X, int Y);

    private static class NotifyIconMessage
    {
        public const uint Add = 0;
        public const uint Modify = 1;
        public const uint Delete = 2;
        public const uint SetVersion = 4;
    }

    private static class NotifyIconFlags
    {
        public const uint Message = 0x00000001;
        public const uint Icon = 0x00000002;
        public const uint Tip = 0x00000004;
    }

    private static class WindowMessages
    {
        public const uint Null = 0x0000;
        public const uint ContextMenu = 0x007B;
        public const uint LeftButtonUp = 0x0202;
        public const uint LeftButtonDoubleClick = 0x0203;
        public const uint RightButtonUp = 0x0205;
        public const uint App = 0x8000;
    }

    private static class ImageTypes
    {
        public const uint Icon = 1;
    }

    private static class LoadImageFlags
    {
        public const uint LoadFromFile = 0x00000010;
        public const uint DefaultSize = 0x00000040;
    }

    private static class MenuFlags
    {
        public const uint String = 0x00000000;
        public const uint Grayed = 0x00000001;
        public const uint Separator = 0x00000800;
    }

    private static class TrackPopupMenuFlags
    {
        public const uint RightButton = 0x00000002;
        public const uint ReturnCommand = 0x00000100;
        public const uint Nonotify = 0x00000080;
    }
}
