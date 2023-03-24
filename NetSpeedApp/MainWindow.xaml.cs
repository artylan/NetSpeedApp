using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace NetSpeedApp
{
    public class Runner
    {
        private Label displayUpload;
        private Label displayDownload;
        private MainWindow mainWindow;

        private string networkInterface;

        private bool alive;

        private PerformanceCounter counterUpload;
        private PerformanceCounter counterDownload;

        public Runner(Label displayUpload, Label displayDownload, MainWindow mainWindow)
        {
            this.displayUpload = displayUpload;
            this.displayDownload = displayDownload;
            this.mainWindow = mainWindow;
        }

        public void run()
        {
            alive = true;
            counterUpload = new PerformanceCounter("Network Adapter", "Bytes Sent/sec", networkInterface);
            counterDownload = new PerformanceCounter("Network Adapter", "Bytes Received/sec", networkInterface);

            while (mainWindow.IsAlive())
            {
                mainWindow.Dispatcher.Invoke((Action)(() =>
                {
                    alive = mainWindow.IsAlive();
                    if (alive)
                    {
                        displayUpload.Content = formatCounterValue(counterUpload.NextValue());
                        displayDownload.Content = formatCounterValue(counterDownload.NextValue());
                    }
                }));
                Thread.Sleep(1000);
            }
        }

        private string formatCounterValue(float value)
        {
            return string.Format("{0:#,##0.00}", value * 8 / 1000) + " kbit/s";
        }

        public void setNetworkInterface(string networkInterface)
        {
            this.networkInterface = networkInterface;
            counterUpload = new PerformanceCounter("Network Adapter", "Bytes Sent/sec", networkInterface);
            counterDownload = new PerformanceCounter("Network Adapter", "Bytes Received/sec", networkInterface);
        }
        public string getNetworkInterface()
        {
            return networkInterface;
        }
    }
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Runner runner;
        private bool alive;
        private string initialNetworkInterface;

        // SystemMenu object
        private SystemMenu systemMenu = null;
        // ID constants
        private const int aboutID = 0x100;

        public MainWindow()
        {
            InitializeComponent();
            
            readRegistry();

            try
            {
                IntPtr windowHandle = new WindowInteropHelper(Application.Current.MainWindow).EnsureHandle();

                systemMenu = SystemMenu.Create(windowHandle);
                // Add a separator ...
                systemMenu.AppendSeparator();
                // ... and an "About" entry
                systemMenu.AppendMenu(aboutID, "About...");
            }
            catch (NoSystemMenuException /* err */ )
            {
                // No problem. We go without SystemMenu.
            }

            alive = true;
            runner = new Runner(DisplayUpload, DisplayDownload, this);
            Thread t = new Thread(new ThreadStart(runner.run));

            NetInterface.ItemsSource = getNetworkInterfaces();
            NetInterface.SelectionChanged += new SelectionChangedEventHandler(MainWindow_SelectionChanged);
            NetInterface.SelectedIndex = findNetworkInterface();

            t.Start();

            Loaded += (sender, args) => {
                HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
                source.AddHook(WndProc);
            }; 
            
            Closed += new EventHandler(MainWindow_Closed);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == (int)WindowMessages.wmSysCommand)
            {
                if (wParam.ToInt32() == aboutID)
                {
                    var window = new About();
                    window.Owner = this;
                    window.ShowDialog();
                }
            }
            return IntPtr.Zero;
        }

        private int findNetworkInterface()
        {
            if (initialNetworkInterface == null)
            {
                return 0;
            }
            int idx = 0;
            foreach (string name in NetInterface.Items)
            {
                if (initialNetworkInterface == name)
                {
                    return idx;
                }
                idx++;
            }
            return 0;
        }

        private IEnumerable getNetworkInterfaces()
        {
            List<string> data = new List<string>();
            PerformanceCounterCategory p = new PerformanceCounterCategory("Network Adapter");
            foreach (string name in p.GetInstanceNames())
            {
                data.Add(name);
            }
            return data;
        }

        private void readRegistry()
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey("Software", false);
            RegistryKey appKey = key.OpenSubKey("NetSpeedApp");
            if (appKey == null)
            {
                return;
            }
            initialNetworkInterface = (string)appKey.GetValue("NetInterface");
            object l = appKey.GetValue("Left");
            if (l != null)
            {
                this.Left = int.Parse((string)l);
            }
            object t = appKey.GetValue("Top");
            if (t != null)
            {
                this.Top = int.Parse((string)t);
            }
        }
        private void saveRegistry()
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey("Software", true);
            RegistryKey appKey = key.OpenSubKey("NetSpeedApp", true);
            if (appKey == null)
            {
                appKey = key.CreateSubKey("NetSpeedApp", true);
            }
            appKey.SetValue("NetInterface", runner.getNetworkInterface());
            appKey.SetValue("Left", this.Left);
            appKey.SetValue("Top", this.Top);
        }

        void MainWindow_Closed(object sender, EventArgs e)
        {
            alive = false;

            saveRegistry();
        }

        public bool IsAlive()
        {
            return alive;
        }

        void MainWindow_SelectionChanged(object sender, EventArgs e)
        {
            runner.setNetworkInterface(NetInterface.SelectedItem.ToString());
        }
    }

    // All that folows is taken and adapted from https://www.codeguru.com/csharp/manipulating-the-system-menu-using-c/
    public class NoSystemMenuException : System.Exception
    {
    }

    // Values taken from MSDN.
    public enum ItemFlags
    { // The item ...
        mfUnchecked = 0x00000000,    // ... is not checked
        mfString = 0x00000000,    // ... contains a string as label
        mfDisabled = 0x00000002,    // ... is disabled
        mfGrayed = 0x00000001,    // ... is grayed
        mfChecked = 0x00000008,    // ... is checked
        mfPopup = 0x00000010,    // ... Is a popup menu. Pass the
                                 //     menu handle of the popup
                                 //     menu into the ID parameter.
        mfBarBreak = 0x00000020,    // ... is a bar break
        mfBreak = 0x00000040,    // ... is a break
        mfByPosition = 0x00000400,    // ... is identified by the position
        mfByCommand = 0x00000000,    // ... is identified by its ID
        mfSeparator = 0x00000800     // ... is a seperator (String and
                                     //     ID parameters are ignored).
    }

    public enum WindowMessages
    {
        wmSysCommand = 0x0112
    }

    /// <summary>
    /// A class that helps to manipulate the system menu
    /// of a passed form.
    ///
    /// Written by Florian "nohero" Stinglmayr
    /// </summary>
    public class SystemMenu
    {
        // I havn't found any other solution than using plain old
        // WinAPI to get what I want.
        // If you need further information on these functions, their
        // parameters, and their meanings, you should look them up in
        // the MSDN.

        // All parameters in the [DllImport] should be self explanatory.
        // NOTICE: Use never stdcall as a calling convention, since Winapi
        // is used.
        // If the underlying structure changes, your program might cause
        // errors that are hard to find.

        // First, we need the GetSystemMenu() function.
        // This function does not have an Unicode counterpart
        [DllImport("USER32", EntryPoint = "GetSystemMenu", SetLastError = true,
                   CharSet = CharSet.Unicode, ExactSpelling = true,
                   CallingConvention = CallingConvention.Winapi)]
        private static extern IntPtr apiGetSystemMenu(IntPtr WindowHandle,
                                                      int bReset);

        // And we need the AppendMenu() function. Since .NET uses Unicode,
        // we pick the unicode solution.
        [DllImport("USER32", EntryPoint = "AppendMenuW", SetLastError = true,
                   CharSet = CharSet.Unicode, ExactSpelling = true,
                   CallingConvention = CallingConvention.Winapi)]
        private static extern int apiAppendMenu(IntPtr MenuHandle, int Flags,
                                                 int NewID, String Item);

        // And we also may need the InsertMenu() function.
        [DllImport("USER32", EntryPoint = "InsertMenuW", SetLastError = true,
                   CharSet = CharSet.Unicode, ExactSpelling = true,
                   CallingConvention = CallingConvention.Winapi)]
        private static extern int apiInsertMenu(IntPtr hMenu, int Position,
                                                  int Flags, int NewId,
                                                  String Item);

        private IntPtr sysMenu = IntPtr.Zero;    // Handle to the System Menu

        public SystemMenu()
        {
        }

        // Insert a separator at the given position index starting at zero.
        public bool InsertSeparator(int Pos)
        {
            return (InsertMenu(Pos, ItemFlags.mfSeparator |
                                ItemFlags.mfByPosition, 0, ""));
        }

        // Simplified InsertMenu(), that assumes that Pos is a relative
        // position index starting at zero
        public bool InsertMenu(int Pos, int ID, String Item)
        {
            return (InsertMenu(Pos, ItemFlags.mfByPosition |
                                ItemFlags.mfString, ID, Item));
        }

        // Insert a menu at the given position. The value of the position
        // depends on the value of Flags. See the article for a detailed
        // description.
        public bool InsertMenu(int Pos, ItemFlags Flags, int ID, String Item)
        {
            return (apiInsertMenu(sysMenu, Pos, (Int32)Flags, ID, Item) == 0);
        }

        // Appends a seperator
        public bool AppendSeparator()
        {
            return AppendMenu(0, "", ItemFlags.mfSeparator);
        }

        // This uses the ItemFlags.mfString as default value
        public bool AppendMenu(int ID, String Item)
        {
            return AppendMenu(ID, Item, ItemFlags.mfString);
        }
        // Superseded function.
        public bool AppendMenu(int ID, String Item, ItemFlags Flags)
        {
            return (apiAppendMenu(sysMenu, (int)Flags, ID, Item) == 0);
        }

        // Retrieves a new object from a Form object
        public static SystemMenu Create(IntPtr windowHandle)
        {
            SystemMenu cSysMenu = new SystemMenu();

            cSysMenu.sysMenu = apiGetSystemMenu(windowHandle, 0);
            if (cSysMenu.sysMenu == IntPtr.Zero)
            { // Throw an exception on failure
                throw new NoSystemMenuException();
            }

            return cSysMenu;
        }

        // Checks if an ID for a new system menu item is OK or not
        public static bool VerifyItemID(int ID)
        {
            return (bool)(ID < 0xF000 && ID > 0);
        }
    }
}
