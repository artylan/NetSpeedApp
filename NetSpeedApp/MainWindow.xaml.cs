using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
                mainWindow.Dispatcher.Invoke((Action)(() => {
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

        public MainWindow()
        {
            InitializeComponent();

            readRegistry();

            alive = true;
            runner = new Runner(DisplayUpload, DisplayDownload, this);
            Thread t = new Thread(new ThreadStart(runner.run));

            NetInterface.ItemsSource = getNetworkInterfaces();
            NetInterface.SelectionChanged += new SelectionChangedEventHandler(MainWindow_SelectionChanged);
            NetInterface.SelectedIndex = findNetworkInterface();

            t.Start();

            Closed += new EventHandler(MainWindow_Closed);
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
}
