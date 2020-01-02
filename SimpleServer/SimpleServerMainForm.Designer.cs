namespace SimpleServer
{
	partial class SimpleServerMainForm
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
			this.ctrlLog = new System.Windows.Forms.ListBox();
			this.groupBoxServer = new System.Windows.Forms.GroupBox();
			this.labelServerPort = new System.Windows.Forms.Label();
			this.labelServerIp = new System.Windows.Forms.Label();
			this.groupBoxData = new System.Windows.Forms.GroupBox();
			this.groupBoxSocket = new System.Windows.Forms.GroupBox();
			this.buttonListen = new System.Windows.Forms.Button();
			this.label1 = new System.Windows.Forms.Label();
			this.ctrlBacklogSize = new System.Windows.Forms.NumericUpDown();
			this.ctrlServerPort = new System.Windows.Forms.NumericUpDown();
			this.ctrlServerIp = new System.Windows.Forms.TextBox();
			this.groupBoxServer.SuspendLayout();
			this.groupBoxSocket.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.ctrlBacklogSize)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.ctrlServerPort)).BeginInit();
			this.SuspendLayout();
			// 
			// ctrlLog
			// 
			this.ctrlLog.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.ctrlLog.FormattingEnabled = true;
			this.ctrlLog.HorizontalScrollbar = true;
			this.ctrlLog.ItemHeight = 12;
			this.ctrlLog.Location = new System.Drawing.Point(12, 12);
			this.ctrlLog.Name = "ctrlLog";
			this.ctrlLog.Size = new System.Drawing.Size(514, 268);
			this.ctrlLog.TabIndex = 0;
			// 
			// groupBoxServer
			// 
			this.groupBoxServer.Controls.Add(this.ctrlBacklogSize);
			this.groupBoxServer.Controls.Add(this.label1);
			this.groupBoxServer.Controls.Add(this.ctrlServerPort);
			this.groupBoxServer.Controls.Add(this.labelServerPort);
			this.groupBoxServer.Controls.Add(this.ctrlServerIp);
			this.groupBoxServer.Controls.Add(this.labelServerIp);
			this.groupBoxServer.Location = new System.Drawing.Point(12, 286);
			this.groupBoxServer.Name = "groupBoxServer";
			this.groupBoxServer.Size = new System.Drawing.Size(514, 52);
			this.groupBoxServer.TabIndex = 1;
			this.groupBoxServer.TabStop = false;
			this.groupBoxServer.Text = "Server";
			// 
			// labelServerPort
			// 
			this.labelServerPort.AutoSize = true;
			this.labelServerPort.Location = new System.Drawing.Point(149, 21);
			this.labelServerPort.Name = "labelServerPort";
			this.labelServerPort.Size = new System.Drawing.Size(31, 12);
			this.labelServerPort.TabIndex = 0;
			this.labelServerPort.Text = "Port:";
			// 
			// labelServerIp
			// 
			this.labelServerIp.AutoSize = true;
			this.labelServerIp.Location = new System.Drawing.Point(7, 21);
			this.labelServerIp.Name = "labelServerIp";
			this.labelServerIp.Size = new System.Drawing.Size(20, 12);
			this.labelServerIp.TabIndex = 0;
			this.labelServerIp.Text = "IP:";
			// 
			// groupBoxData
			// 
			this.groupBoxData.Location = new System.Drawing.Point(12, 345);
			this.groupBoxData.Name = "groupBoxData";
			this.groupBoxData.Size = new System.Drawing.Size(514, 100);
			this.groupBoxData.TabIndex = 2;
			this.groupBoxData.TabStop = false;
			this.groupBoxData.Text = "Data";
			// 
			// groupBoxSocket
			// 
			this.groupBoxSocket.Controls.Add(this.buttonListen);
			this.groupBoxSocket.Location = new System.Drawing.Point(12, 452);
			this.groupBoxSocket.Name = "groupBoxSocket";
			this.groupBoxSocket.Size = new System.Drawing.Size(514, 100);
			this.groupBoxSocket.TabIndex = 3;
			this.groupBoxSocket.TabStop = false;
			this.groupBoxSocket.Text = "Socket";
			// 
			// buttonListen
			// 
			this.buttonListen.Location = new System.Drawing.Point(7, 21);
			this.buttonListen.Name = "buttonListen";
			this.buttonListen.Size = new System.Drawing.Size(75, 23);
			this.buttonListen.TabIndex = 4;
			this.buttonListen.Text = "Listen";
			this.buttonListen.UseVisualStyleBackColor = true;
			this.buttonListen.Click += new System.EventHandler(this.buttonListen_Click);
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(257, 21);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(83, 12);
			this.label1.TabIndex = 0;
			this.label1.Text = "Backlog Size:";
			// 
			// ctrlBacklogSize
			// 
			this.ctrlBacklogSize.DataBindings.Add(new System.Windows.Forms.Binding("Value", global::SimpleServer.Properties.Settings.Default, "BacklogSize", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
			this.ctrlBacklogSize.Increment = new decimal(new int[] {
            100,
            0,
            0,
            0});
			this.ctrlBacklogSize.Location = new System.Drawing.Point(346, 18);
			this.ctrlBacklogSize.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
			this.ctrlBacklogSize.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            0});
			this.ctrlBacklogSize.Name = "ctrlBacklogSize";
			this.ctrlBacklogSize.Size = new System.Drawing.Size(65, 21);
			this.ctrlBacklogSize.TabIndex = 3;
			this.ctrlBacklogSize.ThousandsSeparator = true;
			this.ctrlBacklogSize.Value = global::SimpleServer.Properties.Settings.Default.BacklogSize;
			// 
			// ctrlServerPort
			// 
			this.ctrlServerPort.DataBindings.Add(new System.Windows.Forms.Binding("Value", global::SimpleServer.Properties.Settings.Default, "ServerPort", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
			this.ctrlServerPort.Location = new System.Drawing.Point(186, 19);
			this.ctrlServerPort.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
			this.ctrlServerPort.Name = "ctrlServerPort";
			this.ctrlServerPort.Size = new System.Drawing.Size(65, 21);
			this.ctrlServerPort.TabIndex = 2;
			this.ctrlServerPort.Value = global::SimpleServer.Properties.Settings.Default.ServerPort;
			// 
			// ctrlServerIp
			// 
			this.ctrlServerIp.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::SimpleServer.Properties.Settings.Default, "ServerIp", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
			this.ctrlServerIp.Location = new System.Drawing.Point(33, 18);
			this.ctrlServerIp.Name = "ctrlServerIp";
			this.ctrlServerIp.Size = new System.Drawing.Size(110, 21);
			this.ctrlServerIp.TabIndex = 1;
			this.ctrlServerIp.Text = global::SimpleServer.Properties.Settings.Default.ServerIp;
			// 
			// SimpleServerMainForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(538, 564);
			this.Controls.Add(this.groupBoxSocket);
			this.Controls.Add(this.groupBoxData);
			this.Controls.Add(this.groupBoxServer);
			this.Controls.Add(this.ctrlLog);
			this.Name = "SimpleServerMainForm";
			this.Text = "SImpleServerMainForm";
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.SimpleServerMainForm_FormClosing);
			this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.SimpleServerMainForm_FormClosed);
			this.groupBoxServer.ResumeLayout(false);
			this.groupBoxServer.PerformLayout();
			this.groupBoxSocket.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.ctrlBacklogSize)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.ctrlServerPort)).EndInit();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.ListBox ctrlLog;
		private System.Windows.Forms.GroupBox groupBoxServer;
		private System.Windows.Forms.Label labelServerIp;
		private System.Windows.Forms.TextBox ctrlServerIp;
		private System.Windows.Forms.NumericUpDown ctrlServerPort;
		private System.Windows.Forms.Label labelServerPort;
		private System.Windows.Forms.GroupBox groupBoxData;
		private System.Windows.Forms.GroupBox groupBoxSocket;
		private System.Windows.Forms.Button buttonListen;
		private System.Windows.Forms.NumericUpDown ctrlBacklogSize;
		private System.Windows.Forms.Label label1;
	}
}

