namespace VirtualMonitorHelper
{
    using System;
    using System.Runtime.InteropServices;
    using System.IO;
    using System.Security.AccessControl;
    using System.ServiceProcess;
    using Microsoft.Win32;
    using System.Windows.Forms;

    class Program
    {
        [DllImport("user32.dll")]
        public static extern bool EnumDisplayDevices(
            string lpDevice,
            uint iDevNum,
            ref DISPLAY_DEVICE lpDisplayDevice,
            uint dwFlags
        );

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DISPLAY_DEVICE
        {
            public uint cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
        }

        public const uint EDD_GET_DEVICE_INTERFACE_NAME = 0x00000001;
        public static class GlobalVariables
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public static string previousDeviceName;
            public static string currentDeviceName;
            public static string targetDeviceString = "IddSampleDriver Device";
        }

        static NotifyIcon notifyIcon;
        static ContextMenuStrip contextMenuStrip;

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Create and initialize the NotifyIcon.
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = Properties.Resources.icon;

            // Create and initialize the ContextMenuStrip.
            contextMenuStrip = new ContextMenuStrip();
            ToolStripMenuItem quitMenuItem = new ToolStripMenuItem("Quit");
            quitMenuItem.Click += new EventHandler(QuitMenuItem_Click);
            contextMenuStrip.Items.Add(quitMenuItem);

            // Set up the NotifyIcon.
            notifyIcon.ContextMenuStrip = contextMenuStrip;
            notifyIcon.Visible = true;
            notifyIcon.Text = "Virtual Monitor Helper";

            // Your existing logic goes here...
            DISPLAY_DEVICE displayDevice = new DISPLAY_DEVICE();
            displayDevice.cb = (uint)Marshal.SizeOf(displayDevice);
            bool foundDevice = false;

            uint deviceIndex = 0;
            while (EnumDisplayDevices(null, deviceIndex, ref displayDevice, EDD_GET_DEVICE_INTERFACE_NAME))
            {
                if (string.Equals(displayDevice.DeviceString, GlobalVariables.targetDeviceString, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Device String: {displayDevice.DeviceString}");
                    Console.WriteLine($"Associated Device Name: {displayDevice.DeviceName}");

                    // Update the config file with the "Device Name."
                    UpdateConfigFileWithDeviceName(displayDevice.DeviceName);

                    // Set write permissions for "Everyone" on the config file.
                    SetFilePermissionsForEveryone();

                    // Restart the "SunshineService" Windows service.
                    RestartSunshineService();

                    //Record the current DisplayName for future comparison.
                    GlobalVariables.previousDeviceName = displayDevice.DeviceName;

                    foundDevice = true;
                }
                deviceIndex++;
            }
            if (foundDevice == false)
            {
                Console.WriteLine($"Virtual display device ({GlobalVariables.targetDeviceString}) not found!");
            }

            SystemEvents.DisplaySettingsChanged += new
                EventHandler(SystemEvents_DisplaySettingsChanged);
            Console.WriteLine("Waiting.");
            Application.Run();  // This line replaces the previous Application.Run(new MainForm());
        }

        private static void QuitMenuItem_Click(object sender, EventArgs e)
        {
            // Handle Quit menu item click event.
            Application.Exit();
        }


        private static void UpdateConfigFileWithDeviceName(string deviceName)
        {
            try
            {
                // Construct the path to the config file using the PROGRAMFILES variable.
                string configFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Sunshine", "config", "sunshine.conf");

                // Read the content of the config file.
                string[] lines = File.ReadAllLines(configFilePath);

                // Find and replace the "output_name =" line.
                bool found = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith("output_name ="))
                    {
                        lines[i] = "output_name = " + deviceName;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    // If "output_name =" is not found, add it as a new line
                    Array.Resize(ref lines, lines.Length + 1);
                    lines[lines.Length - 1] = "output_name = " + deviceName;
                }

                // Write the modified content back to the config file.
                File.WriteAllLines(configFilePath, lines);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating config file: {ex.Message}");
            }
        }

        private static void SetFilePermissionsForEveryone()
        {
            try
            {
                // Construct the path to the config file.
                string configFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Sunshine", "config", "sunshine.conf");

                // Get the current file security settings.
                FileSecurity fileSecurity = File.GetAccessControl(configFilePath);

                // Add an access rule for "Everyone" with write permissions.
                FileSystemAccessRule everyoneAccessRule = new FileSystemAccessRule(
                    "Everyone",
                    FileSystemRights.FullControl,
                    AccessControlType.Allow
                );

                // Apply the access rule to the file.
                fileSecurity.AddAccessRule(everyoneAccessRule);

                // Set the modified file security settings.
                File.SetAccessControl(configFilePath, fileSecurity);

                Console.WriteLine("Permissions modified on the Sunshine config file.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting file permissions: {ex.Message}");
            }
        }

        // This method is called when the display settings change.
        private static void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            Console.WriteLine("The display settings changed.  Checking to see if Display Name is the same.");

            DISPLAY_DEVICE displayDevice = new DISPLAY_DEVICE();
            displayDevice.cb = (uint)Marshal.SizeOf(displayDevice);
            bool foundDevice = false;

            uint deviceIndex = 0;
            while (EnumDisplayDevices(null, deviceIndex, ref displayDevice, EDD_GET_DEVICE_INTERFACE_NAME))
            {
                if (string.Equals(displayDevice.DeviceString, GlobalVariables.targetDeviceString, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"New Device String: {displayDevice.DeviceString}");
                    Console.WriteLine($"New Associated Device Name: {displayDevice.DeviceName}");
                    Console.WriteLine($"Old Associated Device Name: {GlobalVariables.previousDeviceName}");
                    GlobalVariables.currentDeviceName = displayDevice.DeviceName;

                    foundDevice = true;
                }
                deviceIndex++;
            }
            if (foundDevice == false)
            {
                Console.WriteLine($"{GlobalVariables.targetDeviceString} has been lost!  Restarting Sunshine service and waiting...");
                RestartSunshineService();
                return;
            }
            
            if (GlobalVariables.currentDeviceName != GlobalVariables.previousDeviceName)
            {
                Console.WriteLine("Display Name has changed!  Running process.");
                // Update the config file with the "Device Name."
                UpdateConfigFileWithDeviceName(GlobalVariables.currentDeviceName);

                // Set write permissions for "Everyone" on the config file.
                SetFilePermissionsForEveryone();

                // Restart the "SunshineService" Windows service.
                RestartSunshineService();

                //Record the current DisplayName for future comparison.
                GlobalVariables.previousDeviceName = GlobalVariables.currentDeviceName;
            }
            else if (GlobalVariables.currentDeviceName == GlobalVariables.previousDeviceName)
            {
                Console.WriteLine("Display Name has not changed.  No need to run process.");
            }
        }

        private static void RestartSunshineService()
        {
            try
            {
                // Specify the service name.
                string serviceName = "SunshineService";

                // Create a ServiceController for the service.
                using (ServiceController serviceController = new ServiceController(serviceName))
                {
                    if (serviceController.Status == ServiceControllerStatus.Running)
                    {
                        // Stop the service.
                        serviceController.Stop();
                        serviceController.WaitForStatus(ServiceControllerStatus.Stopped);

                        // Start the service.
                        serviceController.Start();
                        serviceController.WaitForStatus(ServiceControllerStatus.Running);

                        Console.WriteLine($"Sunshine service restarted.");
                    }
                    else
                    {
                        Console.WriteLine($"Sunshine service is not running.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error restarting service: {ex.Message}");
            }
        }

    }

    // Define your main form class here (if not already defined).
    public class MainForm : Form
    {

    }
}
