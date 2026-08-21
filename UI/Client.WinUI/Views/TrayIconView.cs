using System.Runtime.InteropServices;
using FAP.Application.ViewModels;
using FAP.Application.Views;
using Microsoft.UI.Xaml.Controls;

namespace Fap.Client.WinUI.Views;

/// <summary>
/// Tray icon via Shell_NotifyIcon (avoids WinForms/WinUI XAML conflicts).
/// </summary>
public sealed class TrayIconView : WinUiViewBase, ITrayIconView
{
    private readonly IntPtr _hwnd;
    private readonly uint _callbackMessage;
    private TrayIconViewModel? _model;
    private bool _showIcon;
    private bool _added;
    private bool _ownsIcon;
    private NOTIFYICONDATA _data;

    public TrayIconView()
    {
        _callbackMessage = 0x8001; // WM_APP + 1
        _hwnd = CreateMessageWindow();
        _data = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_TIP | NIF_ICON,
            uCallbackMessage = _callbackMessage,
            szTip = "FAP - File Acceleration Protocol"
        };

        _data.hIcon = LoadAppIcon();
        if (_data.hIcon == IntPtr.Zero)
            _data.hIcon = LoadIcon(IntPtr.Zero, (IntPtr)32512); // IDI_APPLICATION fallback
        else
            _ownsIcon = true;

        Content = new TextBlock { Visibility = Microsoft.UI.Xaml.Visibility.Collapsed };
        DataContextChanged += (_, _) => _model = DataContext as TrayIconViewModel;
    }

    public bool ShowIcon
    {
        get => _showIcon;
        set
        {
            _showIcon = value;
            if (value)
            {
                if (!_added)
                    _added = Shell_NotifyIcon(NIM_ADD, ref _data);
                else
                    Shell_NotifyIcon(NIM_MODIFY, ref _data);
            }
            else if (_added)
            {
                Shell_NotifyIcon(NIM_DELETE, ref _data);
                _added = false;
            }
        }
    }

    public void Dispose()
    {
        if (_added)
        {
            Shell_NotifyIcon(NIM_DELETE, ref _data);
            _added = false;
        }

        if (_ownsIcon && _data.hIcon != IntPtr.Zero)
        {
            DestroyIcon(_data.hIcon);
            _data.hIcon = IntPtr.Zero;
            _ownsIcon = false;
        }
    }

    private static IntPtr LoadAppIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (!File.Exists(iconPath))
                return IntPtr.Zero;

            // Prefer the small icon for the notification area.
            ExtractIconEx(iconPath, 0, out var large, out var small, 1);
            if (small != IntPtr.Zero)
            {
                if (large != IntPtr.Zero)
                    DestroyIcon(large);
                return small;
            }

            if (large != IntPtr.Zero)
                return large;

            return LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private IntPtr CreateMessageWindow()
    {
        var className = "FapTrayMsgWnd";
        _wndProc = WndProc;
        var wc = new WNDCLASS
        {
            lpszClassName = className,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc)
        };
        var atom = RegisterClass(ref wc);
        if (atom == 0)
        {
            var err = Marshal.GetLastWin32Error();
            if (err != 1410) // ERROR_CLASS_ALREADY_EXISTS
                return IntPtr.Zero;
        }

        return CreateWindowEx(0, className, string.Empty, 0, 0, 0, 0, 0, HWND_MESSAGE, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
    }

    private WndProcDelegate? _wndProc;

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == _callbackMessage)
        {
            var mouse = (uint)lParam.ToInt64() & 0xFFFF;
            if (mouse is WM_LBUTTONDBLCLK or WM_LBUTTONUP)
                CommandHelpers.TryExecute(_model?.Open);
            else if (mouse == WM_RBUTTONUP)
                ShowContextMenu();
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        var menu = CreatePopupMenu();
        AppendMenu(menu, MF_STRING, 1, "Open");
        AppendMenu(menu, MF_STRING, 2, "Chat");
        AppendMenu(menu, MF_STRING, 3, "Search");
        AppendMenu(menu, MF_STRING, 4, "Queue");
        AppendMenu(menu, MF_STRING, 5, "Shares");
        AppendMenu(menu, MF_STRING, 6, "Compare");
        AppendMenu(menu, MF_STRING, 7, "Settings");
        AppendMenu(menu, MF_SEPARATOR, 0, string.Empty);
        AppendMenu(menu, MF_STRING, 8, "Exit");

        GetCursorPos(out var pt);
        SetForegroundWindow(_hwnd);
        var cmd = (int)TrackPopupMenu(menu, TPM_RETURNCMD | TPM_LEFTALIGN, pt.X, pt.Y, 0, _hwnd, IntPtr.Zero);
        DestroyMenu(menu);

        switch (cmd)
        {
            case 1: CommandHelpers.TryExecute(_model?.Open); break;
            case 2: CommandHelpers.TryExecute(_model?.Chat); break;
            case 3: CommandHelpers.TryExecute(_model?.Search); break;
            case 4: CommandHelpers.TryExecute(_model?.Queue); break;
            case 5: CommandHelpers.TryExecute(_model?.Shares); break;
            case 6: CommandHelpers.TryExecute(_model?.Compare); break;
            case 7: CommandHelpers.TryExecute(_model?.Settings); break;
            case 8: CommandHelpers.TryExecute(_model?.Exit); break;
        }
    }

    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;
    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_LBUTTONDBLCLK = 0x0203;
    private const uint WM_RBUTTONUP = 0x0205;
    private const uint MF_STRING = 0x00000000;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint TPM_RETURNCMD = 0x0100;
    private const uint TPM_LEFTALIGN = 0x0000;
    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x00000010;
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASS
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIcons);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName, int dwStyle,
        int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
