namespace FileMonitorController
{
    partial class FormMain
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.ListViewProcesses = new System.Windows.Forms.ListView();
            this.ColumnPid = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ColumnArchitecture = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ColumnName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ColumnOwner = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ColumnPath = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ListViewLog = new System.Windows.Forms.ListView();
            this.ColumnProcess = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ColumnTime = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ColumnFilePath = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.Timer_UpdateMonitorLog = new System.Windows.Forms.Timer(this.components);
            this.Splitter = new System.Windows.Forms.SplitContainer();
            this.label1 = new System.Windows.Forms.Label();
            this.ButtonRefreshList = new System.Windows.Forms.Button();
            this.ButtonClearEventLog = new System.Windows.Forms.Button();
            this.Splitter.Panel1.SuspendLayout();
            this.Splitter.Panel2.SuspendLayout();
            this.Splitter.SuspendLayout();
            this.SuspendLayout();
            // 
            // ListViewProcesses
            // 
            this.ListViewProcesses.CheckBoxes = true;
            this.ListViewProcesses.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.ColumnPid,
            this.ColumnArchitecture,
            this.ColumnName,
            this.ColumnOwner,
            this.ColumnPath});
            this.ListViewProcesses.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ListViewProcesses.FullRowSelect = true;
            this.ListViewProcesses.GridLines = true;
            this.ListViewProcesses.Location = new System.Drawing.Point(0, 0);
            this.ListViewProcesses.Name = "ListViewProcesses";
            this.ListViewProcesses.ShowGroups = false;
            this.ListViewProcesses.ShowItemToolTips = true;
            this.ListViewProcesses.Size = new System.Drawing.Size(690, 472);
            this.ListViewProcesses.TabIndex = 0;
            this.ListViewProcesses.UseCompatibleStateImageBehavior = false;
            this.ListViewProcesses.View = System.Windows.Forms.View.Details;
            this.ListViewProcesses.ItemChecked += new System.Windows.Forms.ItemCheckedEventHandler(this.ListViewProcesses_ItemChecked);
            // 
            // ColumnPid
            // 
            this.ColumnPid.Text = "Process ID";
            this.ColumnPid.Width = 67;
            // 
            // ColumnArchitecture
            // 
            this.ColumnArchitecture.Text = "Architecture";
            this.ColumnArchitecture.Width = 78;
            // 
            // ColumnName
            // 
            this.ColumnName.Text = "Process Name";
            this.ColumnName.Width = 144;
            // 
            // ColumnOwner
            // 
            this.ColumnOwner.Text = "Owner";
            this.ColumnOwner.Width = 96;
            // 
            // ColumnPath
            // 
            this.ColumnPath.Text = "Image Path";
            this.ColumnPath.Width = 382;
            // 
            // ListViewLog
            // 
            this.ListViewLog.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.ColumnProcess,
            this.ColumnTime,
            this.ColumnFilePath});
            this.ListViewLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ListViewLog.FullRowSelect = true;
            this.ListViewLog.GridLines = true;
            this.ListViewLog.Location = new System.Drawing.Point(0, 0);
            this.ListViewLog.Name = "ListViewLog";
            this.ListViewLog.ShowGroups = false;
            this.ListViewLog.ShowItemToolTips = true;
            this.ListViewLog.Size = new System.Drawing.Size(686, 472);
            this.ListViewLog.TabIndex = 1;
            this.ListViewLog.UseCompatibleStateImageBehavior = false;
            this.ListViewLog.View = System.Windows.Forms.View.Details;
            // 
            // ColumnProcess
            // 
            this.ColumnProcess.Text = "Process Name";
            this.ColumnProcess.Width = 127;
            // 
            // ColumnTime
            // 
            this.ColumnTime.Text = "Time";
            this.ColumnTime.Width = 108;
            // 
            // ColumnFilePath
            // 
            this.ColumnFilePath.Text = "File";
            this.ColumnFilePath.Width = 531;
            // 
            // Timer_UpdateMonitorLog
            // 
            this.Timer_UpdateMonitorLog.Enabled = true;
            this.Timer_UpdateMonitorLog.Interval = 1000;
            this.Timer_UpdateMonitorLog.Tick += new System.EventHandler(this.Timer_UpdateMonitorLog_Tick);
            // 
            // Splitter
            // 
            this.Splitter.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.Splitter.Location = new System.Drawing.Point(0, 63);
            this.Splitter.Name = "Splitter";
            // 
            // Splitter.Panel1
            // 
            this.Splitter.Panel1.Controls.Add(this.ListViewProcesses);
            // 
            // Splitter.Panel2
            // 
            this.Splitter.Panel2.Controls.Add(this.ListViewLog);
            this.Splitter.Size = new System.Drawing.Size(1381, 472);
            this.Splitter.SplitterDistance = 690;
            this.Splitter.SplitterWidth = 5;
            this.Splitter.TabIndex = 3;
            // 
            // label1
            // 
            this.label1.Dock = System.Windows.Forms.DockStyle.Top;
            this.label1.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(0, 0);
            this.label1.Name = "label1";
            this.label1.Padding = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.label1.Size = new System.Drawing.Size(1381, 31);
            this.label1.TabIndex = 4;
            this.label1.Text = "Place a checkmark beside each process you want to intercept. Then, select (or mul" +
    "ti-select) which process(es) you\'d like to view in the log to the right.";
            this.label1.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // ButtonRefreshList
            // 
            this.ButtonRefreshList.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.ButtonRefreshList.Location = new System.Drawing.Point(555, 34);
            this.ButtonRefreshList.MinimumSize = new System.Drawing.Size(109, 23);
            this.ButtonRefreshList.Name = "ButtonRefreshList";
            this.ButtonRefreshList.Size = new System.Drawing.Size(134, 23);
            this.ButtonRefreshList.TabIndex = 5;
            this.ButtonRefreshList.Text = "Refresh Processes List";
            this.ButtonRefreshList.UseVisualStyleBackColor = true;
            this.ButtonRefreshList.Click += new System.EventHandler(this.ButtonRefreshList_Click);
            // 
            // ButtonClearEventLog
            // 
            this.ButtonClearEventLog.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.ButtonClearEventLog.Location = new System.Drawing.Point(695, 34);
            this.ButtonClearEventLog.MaximumSize = new System.Drawing.Size(109, 23);
            this.ButtonClearEventLog.MinimumSize = new System.Drawing.Size(109, 23);
            this.ButtonClearEventLog.Name = "ButtonClearEventLog";
            this.ButtonClearEventLog.Size = new System.Drawing.Size(109, 23);
            this.ButtonClearEventLog.TabIndex = 6;
            this.ButtonClearEventLog.Text = "Clear Event Log";
            this.ButtonClearEventLog.UseVisualStyleBackColor = true;
            this.ButtonClearEventLog.Click += new System.EventHandler(this.ButtonClearEventLog_Click);
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(253)))), ((int)(((byte)(253)))), ((int)(((byte)(253)))));
            this.ClientSize = new System.Drawing.Size(1381, 535);
            this.Controls.Add(this.ButtonClearEventLog);
            this.Controls.Add(this.ButtonRefreshList);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.Splitter);
            this.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.MinimumSize = new System.Drawing.Size(1397, 574);
            this.Name = "FormMain";
            this.Text = "File Monitor (EasyHook Demo)";
            this.Load += new System.EventHandler(this.FormMain_Load);
            this.Splitter.Panel1.ResumeLayout(false);
            this.Splitter.Panel2.ResumeLayout(false);
            this.Splitter.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListView ListViewProcesses;
        private System.Windows.Forms.ColumnHeader ColumnPid;
        private System.Windows.Forms.ColumnHeader ColumnArchitecture;
        private System.Windows.Forms.ColumnHeader ColumnName;
        private System.Windows.Forms.ColumnHeader ColumnOwner;
        private System.Windows.Forms.ColumnHeader ColumnPath;
        private System.Windows.Forms.ListView ListViewLog;
        private System.Windows.Forms.ColumnHeader ColumnProcess;
        private System.Windows.Forms.ColumnHeader ColumnTime;
        private System.Windows.Forms.ColumnHeader ColumnFilePath;
        private System.Windows.Forms.Timer Timer_UpdateMonitorLog;
        private System.Windows.Forms.SplitContainer Splitter;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button ButtonRefreshList;
        private System.Windows.Forms.Button ButtonClearEventLog;
    }
}

