using System;
using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace windows_service
{
    [RunInstaller(true)]
    public partial class ServiceInstaller : System.Configuration.Install.Installer
    {
        private ServiceProcessInstaller serviceProcessInstaller;
        private System.ServiceProcess.ServiceInstaller serviceInstaller;

        public ServiceInstaller()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.serviceProcessInstaller = new ServiceProcessInstaller();
            this.serviceInstaller = new System.ServiceProcess.ServiceInstaller();

            // Service Process Installer
            this.serviceProcessInstaller.Account = ServiceAccount.LocalService;
            this.serviceProcessInstaller.Password = null;
            this.serviceProcessInstaller.Username = null;

            // Service Installer
            this.serviceInstaller.ServiceName = "FileMonitorService";
            this.serviceInstaller.DisplayName = "File Monitor Service";
            this.serviceInstaller.Description = "Monitors files in Folder1 and moves them to Folder2";
            this.serviceInstaller.StartType = ServiceStartMode.Automatic;
            this.serviceInstaller.DelayedAutoStart = true;

            // Add installers to collection
            this.Installers.AddRange(new System.Configuration.Install.Installer[] {
                this.serviceProcessInstaller,
                this.serviceInstaller});
        }

        protected override void OnBeforeInstall(System.Collections.IDictionary savedState)
        {
            base.OnBeforeInstall(savedState);

            // Create event source if it doesn't exist
            try
            {
                if (!System.Diagnostics.EventLog.SourceExists("FileMonitorService"))
                {
                    System.Diagnostics.EventLog.CreateEventSource("FileMonitorService", "Application");
                    System.Threading.Thread.Sleep(1000); // Allow time for source creation
                }
            }
            catch (System.Security.SecurityException)
            {
                // Cannot create event source - installer is not running with sufficient privileges
                // Service will run without event log support
            }
            catch (Exception)
            {
                // Other error creating event source - service will handle gracefully
            }
        }

        protected override void OnAfterInstall(System.Collections.IDictionary savedState)
        {
            base.OnAfterInstall(savedState);

            // Start the service after installation
            try
            {
                using (ServiceController sc = new ServiceController("FileMonitorService"))
                {
                    sc.Start();
                }
            }
            catch (Exception ex)
            {
                // Log the exception but don't fail the installation
                System.Diagnostics.EventLog.WriteEntry("FileMonitorService",
                    $"Failed to start service after installation: {ex.Message}",
                    System.Diagnostics.EventLogEntryType.Warning);
            }
        }

        protected override void OnBeforeUninstall(System.Collections.IDictionary savedState)
        {
            try
            {
                using (ServiceController sc = new ServiceController("FileMonitorService"))
                {
                    if (sc.Status == ServiceControllerStatus.Running)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.EventLog.WriteEntry("FileMonitorService",
                    $"Failed to stop service before uninstall: {ex.Message}",
                    System.Diagnostics.EventLogEntryType.Warning);
            }

            base.OnBeforeUninstall(savedState);
        }
    }
}
