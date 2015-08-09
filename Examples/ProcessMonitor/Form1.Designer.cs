namespace ProcessMonitor
{
    partial class Form1
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
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.CHK_IsMonitoring = new System.Windows.Forms.CheckBox();
            this.BTN_Clear = new System.Windows.Forms.Button();
            this.LIST_Accesses = new System.Windows.Forms.ListView();
            this.PID = new System.Windows.Forms.ColumnHeader();
            this.Time = new System.Windows.Forms.ColumnHeader();
            this.Access = new System.Windows.Forms.ColumnHeader();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.LIST_Processes = new System.Windows.Forms.ListView();
            this.Activated = new System.Windows.Forms.ColumnHeader();
            this.EnumPID = new System.Windows.Forms.ColumnHeader();
            this.Wow64 = new System.Windows.Forms.ColumnHeader();
            this.ProcName = new System.Windows.Forms.ColumnHeader();
            this.Path = new System.Windows.Forms.ColumnHeader();
            this.TIMER = new System.Windows.Forms.Timer(this.components);
            this.UserName = new System.Windows.Forms.ColumnHeader();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.CHK_IsMonitoring);
            this.groupBox1.Controls.Add(this.BTN_Clear);
            this.groupBox1.Controls.Add(this.LIST_Accesses);
            this.groupBox1.Location = new System.Drawing.Point(12, 213);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(763, 312);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Intercepted accesses:";
            // 
            // CHK_IsMonitoring
            // 
            this.CHK_IsMonitoring.AutoSize = true;
            this.CHK_IsMonitoring.Checked = true;
            this.CHK_IsMonitoring.CheckState = System.Windows.Forms.CheckState.Checked;
            this.CHK_IsMonitoring.Location = new System.Drawing.Point(133, 0);
            this.CHK_IsMonitoring.Name = "CHK_IsMonitoring";
            this.CHK_IsMonitoring.Size = new System.Drawing.Size(150, 17);
            this.CHK_IsMonitoring.TabIndex = 5;
            this.CHK_IsMonitoring.Text = "Should monitor accesses?";
            this.CHK_IsMonitoring.UseVisualStyleBackColor = true;
            this.CHK_IsMonitoring.CheckedChanged += new System.EventHandler(this.CHK_IsMonitoring_CheckedChanged);
            // 
            // BTN_Clear
            // 
            this.BTN_Clear.Location = new System.Drawing.Point(682, 0);
            this.BTN_Clear.Name = "BTN_Clear";
            this.BTN_Clear.Size = new System.Drawing.Size(75, 23);
            this.BTN_Clear.TabIndex = 4;
            this.BTN_Clear.Text = "Clear";
            this.BTN_Clear.UseVisualStyleBackColor = true;
            this.BTN_Clear.Click += new System.EventHandler(this.BTN_Clear_Click);
            // 
            // LIST_Accesses
            // 
            this.LIST_Accesses.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.LIST_Accesses.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.PID,
            this.Time,
            this.Access});
            this.LIST_Accesses.GridLines = true;
            this.LIST_Accesses.HideSelection = false;
            this.LIST_Accesses.Location = new System.Drawing.Point(6, 29);
            this.LIST_Accesses.MultiSelect = false;
            this.LIST_Accesses.Name = "LIST_Accesses";
            this.LIST_Accesses.Size = new System.Drawing.Size(751, 277);
            this.LIST_Accesses.TabIndex = 0;
            this.LIST_Accesses.UseCompatibleStateImageBehavior = false;
            this.LIST_Accesses.View = System.Windows.Forms.View.Details;
            // 
            // PID
            // 
            this.PID.Text = "PID";
            this.PID.Width = 52;
            // 
            // Time
            // 
            this.Time.Text = "Time";
            this.Time.Width = 91;
            // 
            // Access
            // 
            this.Access.Text = "Access";
            this.Access.Width = 580;
            // 
            // groupBox2
            // 
            this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox2.Controls.Add(this.LIST_Processes);
            this.groupBox2.Location = new System.Drawing.Point(12, 12);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(763, 195);
            this.groupBox2.TabIndex = 2;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Available processes:";
            // 
            // LIST_Processes
            // 
            this.LIST_Processes.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.LIST_Processes.CheckBoxes = true;
            this.LIST_Processes.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.Activated,
            this.EnumPID,
            this.Wow64,
            this.ProcName,
            this.UserName,
            this.Path});
            this.LIST_Processes.FullRowSelect = true;
            this.LIST_Processes.GridLines = true;
            this.LIST_Processes.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.LIST_Processes.HideSelection = false;
            this.LIST_Processes.Location = new System.Drawing.Point(6, 19);
            this.LIST_Processes.MultiSelect = false;
            this.LIST_Processes.Name = "LIST_Processes";
            this.LIST_Processes.Size = new System.Drawing.Size(751, 170);
            this.LIST_Processes.TabIndex = 0;
            this.LIST_Processes.UseCompatibleStateImageBehavior = false;
            this.LIST_Processes.View = System.Windows.Forms.View.Details;
            this.LIST_Processes.ItemChecked += new System.Windows.Forms.ItemCheckedEventHandler(this.LIST_Processes_ItemChecked);
            // 
            // Activated
            // 
            this.Activated.Text = "";
            this.Activated.Width = 27;
            // 
            // EnumPID
            // 
            this.EnumPID.Text = "PID";
            this.EnumPID.Width = 70;
            // 
            // Wow64
            // 
            this.Wow64.Text = "";
            this.Wow64.Width = 39;
            // 
            // ProcName
            // 
            this.ProcName.Text = "Name";
            this.ProcName.Width = 161;
            // 
            // Path
            // 
            this.Path.Text = "Path";
            this.Path.Width = 601;
            // 
            // TIMER
            // 
            this.TIMER.Enabled = true;
            this.TIMER.Interval = 2000;
            this.TIMER.Tick += new System.EventHandler(this.TIMER_Tick);
            // 
            // UserName
            // 
            this.UserName.Text = "User";
            this.UserName.Width = 132;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(787, 537);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Name = "Form1";
            this.Text = "Interactive Process Monitor powered by EasyHook        -       Copyrights 2008 by" +
                " Christoph Husse,  released under MIT-License";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.ListView LIST_Accesses;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.ListView LIST_Processes;
        private System.Windows.Forms.Button BTN_Clear;
        private System.Windows.Forms.ColumnHeader PID;
        private System.Windows.Forms.ColumnHeader Time;
        private System.Windows.Forms.ColumnHeader Access;
        private System.Windows.Forms.ColumnHeader EnumPID;
        private System.Windows.Forms.ColumnHeader Wow64;
        private System.Windows.Forms.ColumnHeader Path;
        private System.Windows.Forms.ColumnHeader Activated;
        private System.Windows.Forms.Timer TIMER;
        private System.Windows.Forms.ColumnHeader ProcName;
        private System.Windows.Forms.CheckBox CHK_IsMonitoring;
        private System.Windows.Forms.ColumnHeader UserName;
    }
}

