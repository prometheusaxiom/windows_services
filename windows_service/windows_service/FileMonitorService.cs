using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.ServiceProcess;
using System.Timers;
using NLog;

namespace windows_service
{
    public partial class FileMonitorService : ServiceBase
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private FileSystemWatcher fileWatcher;
        private string sourceFolder;
        private string destinationFolder;
        private Timer retryTimer;
        private bool eventLogAvailable = false;

        public FileMonitorService()
        {
            InitializeComponent();

            // Set service properties
            this.CanStop = true;
            this.CanPauseAndContinue = false;
            this.AutoLog = true;

            // Check if event log is available
            try
            {
                if (!EventLog.SourceExists("FileMonitorService"))
                {
                    // Try to create it, but don't fail if we can't
                    EventLog.CreateEventSource("FileMonitorService", "Application");
                }
                eventLogAvailable = true;
            }
            catch (SecurityException)
            {
                // Event log not available - continue without it
                eventLogAvailable = false;
                logger.Warn("Event Log source could not be created. Service will continue without Event Log support. Run as administrator to create event source.");
            }
            catch (Exception ex)
            {
                eventLogAvailable = false;
                logger.Warn($"Event Log initialization failed: {ex.Message}");
            }
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                // Read configuration
                sourceFolder = ConfigurationManager.AppSettings["SourceFolder"] ?? @"C:\Folder1";
                destinationFolder = ConfigurationManager.AppSettings["DestinationFolder"] ?? @"C:\Folder2";

                // Ensure directories exist
                if (!Directory.Exists(sourceFolder))
                {
                    Directory.CreateDirectory(sourceFolder);
                    logger.Info($"Created source directory: {sourceFolder}");
                }

                if (!Directory.Exists(destinationFolder))
                {
                    Directory.CreateDirectory(destinationFolder);
                    logger.Info($"Created destination directory: {destinationFolder}");
                }

                // Initialize FileSystemWatcher
                InitializeFileWatcher();

                // Initialize retry timer for failed operations
                retryTimer = new Timer(30000); // 30 seconds
                retryTimer.Elapsed += RetryTimer_Elapsed;
                retryTimer.Start();

                logger.Info($"FileMonitorService started successfully. Monitoring: {sourceFolder}");
                WriteEventLog("FileMonitorService started successfully", EventLogEntryType.Information);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to start FileMonitorService");
                WriteEventLog($"Failed to start FileMonitorService: {ex.Message}", EventLogEntryType.Error);
                throw;
            }
        }

        private void WriteEventLog(string message, EventLogEntryType type)
        {
            try
            {
                if (eventLogAvailable)
                {
                    eventLog1.WriteEntry(message, type);
                }
            }
            catch (Exception ex)
            {
                logger.Warn($"Failed to write to Event Log: {ex.Message}");
            }
        }
        

        protected override void OnStop()
        {
            try
            {
                if (fileWatcher != null)
                {
                    fileWatcher.EnableRaisingEvents = false;
                    fileWatcher.Dispose();
                }

                if (retryTimer != null)
                {
                    retryTimer.Stop();
                    retryTimer.Dispose();
                }

                logger.Info("FileMonitorService stopped successfully");
                WriteEventLog("FileMonitorService stopped", EventLogEntryType.Information);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error stopping FileMonitorService");
                WriteEventLog($"Error stopping FileMonitorService: {ex.Message}", EventLogEntryType.Error);
            }
        }

        private void InitializeFileWatcher()
        {
            fileWatcher = new FileSystemWatcher
            {
                Path = sourceFolder,
                NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.FileName,
                Filter = "*.*",
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };

            fileWatcher.Created += OnFileCreated;
            fileWatcher.Error += OnFileWatcherError;
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            try
            {
                logger.Info($"File created: {e.FullPath}");

                // Wait a moment to ensure file is fully written
                System.Threading.Thread.Sleep(100);

                MoveFile(e.FullPath, e.Name);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error processing file creation event for: {e.FullPath}");
                WriteEventLog($"Error processing file: {e.FullPath}. Error: {ex.Message}", EventLogEntryType.Warning);
            }
        }

        private void MoveFile(string sourceFilePath, string fileName)
        {
            string destinationPath = Path.Combine(destinationFolder, fileName);
            int retryCount = 0;
            const int maxRetries = 3;

            while (retryCount < maxRetries)
            {
                try
                {
                    // Check if source file exists and is not locked
                    if (!File.Exists(sourceFilePath))
                    {
                        logger.Warn($"Source file no longer exists: {sourceFilePath}");
                        return;
                    }

                    // Handle duplicate file names
                    if (File.Exists(destinationPath))
                    {
                        string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                        string extension = Path.GetExtension(fileName);
                        int counter = 1;

                        do
                        {
                            string newFileName = $"{nameWithoutExt}_{counter}{extension}";
                            destinationPath = Path.Combine(destinationFolder, newFileName);
                            counter++;
                        } while (File.Exists(destinationPath));
                    }

                    // Attempt to move the file
                    File.Move(sourceFilePath, destinationPath);

                    logger.Info($"Successfully moved file from {sourceFilePath} to {destinationPath}");
                    WriteEventLog($"File moved: {fileName} from {sourceFolder} to {destinationFolder}", EventLogEntryType.Information);
                    return;
                }
                catch (IOException ioEx)
                {
                    retryCount++;
                    logger.Warn($"IO Exception moving file (attempt {retryCount}/{maxRetries}): {sourceFilePath}. Error: {ioEx.Message}");

                    if (retryCount >= maxRetries)
                    {
                        logger.Error($"Failed to move file after {maxRetries} attempts: {sourceFilePath}");
                        WriteEventLog($"Failed to move file after {maxRetries} attempts: {fileName}. Error: {ioEx.Message}", EventLogEntryType.Error);
                        return;
                    }

                    // Wait before retry
                    System.Threading.Thread.Sleep(1000 * retryCount);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Unexpected error moving file: {sourceFilePath}");
                    WriteEventLog($"Unexpected error moving file: {fileName}. Error: {ex.Message}", EventLogEntryType.Error);
                    return;
                }
            }
        }

        private void OnFileWatcherError(object sender, ErrorEventArgs e)
        {
            logger.Error(e.GetException(), "FileSystemWatcher error occurred");
            WriteEventLog($"FileSystemWatcher error: {e.GetException().Message}", EventLogEntryType.Error);

            try
            {
                // Reinitialize the watcher
                if (fileWatcher != null)
                {
                    fileWatcher.Dispose();
                }
                InitializeFileWatcher();
                logger.Info("FileSystemWatcher reinitialized after error");
            }
            catch (Exception ex)
            {
                logger.Fatal(ex, "Failed to reinitialize FileSystemWatcher");
                WriteEventLog($"Failed to reinitialize FileSystemWatcher: {ex.Message}", EventLogEntryType.Error);
            }
        }

        private void RetryTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // Cleanup and retry logic for any remaining files in source folder
            try
            {
                if (Directory.Exists(sourceFolder))
                {
                    string[] files = Directory.GetFiles(sourceFolder);
                    foreach (string file in files)
                    {
                        FileInfo fileInfo = new FileInfo(file);
                        // Only process files that are at least 5 seconds old to avoid conflicts
                        if (DateTime.Now - fileInfo.CreationTime > TimeSpan.FromSeconds(5))
                        {
                            logger.Info($"Retry processing file: {file}");
                            MoveFile(file, fileInfo.Name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error during retry timer operation");
            }
        }

        // Method to run as console app for debugging
        public void RunAsConsole()
        {
            OnStart(null);
            Console.WriteLine("Service started successfully!");
            Console.WriteLine($"Monitoring: {sourceFolder}");
            Console.WriteLine($"Moving files to: {destinationFolder}");
            Console.WriteLine("\nService is running. Try creating files in the source folder.");
            Console.WriteLine("Press any key to stop the service...\n");
            Console.Read();
            OnStop();
            Console.WriteLine("Service stopped.");
        }

        // Public method to stop service (for console mode)
        public new void Stop()
        {
            OnStop();
        }
    }
}
