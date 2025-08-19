using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace windows_service
{
    partial class FileMonitorService
    {
        private System.ComponentModel.IContainer components = null;
        private System.Diagnostics.EventLog eventLog1;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code
        private void InitializeComponent()
        {
            this.eventLog1 = new System.Diagnostics.EventLog();
            ((System.ComponentModel.ISupportInitialize)(this.eventLog1)).BeginInit();
            // 
            // eventLog1
            // 
            this.eventLog1.Log = "Application";
            this.eventLog1.Source = "FileMonitorService";
            // 
            // FileMonitorService
            // 
            this.ServiceName = "FileMonitorService";
            ((System.ComponentModel.ISupportInitialize)(this.eventLog1)).EndInit();
        }
        #endregion
    }
}
