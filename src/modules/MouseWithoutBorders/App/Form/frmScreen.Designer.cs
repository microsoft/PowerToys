namespace MouseWithoutBorders
{
    partial class FrmScreen
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FrmScreen));
            this.MainMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuExit = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.menuAbout = new System.Windows.Forms.ToolStripMenuItem();
            this.menuHelp = new System.Windows.Forms.ToolStripMenuItem();
            this.menuGenDumpFile = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.menuGetScreenCapture = new System.Windows.Forms.ToolStripMenuItem();
            this.menuGetFromAll = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSendScreenCapture = new System.Windows.Forms.ToolStripMenuItem();
            this.allToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSend2Myself = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.menuWindowsPhone = new System.Windows.Forms.ToolStripMenuItem();
            this.menuWindowsPhoneEnable = new System.Windows.Forms.ToolStripMenuItem();
            this.menuWindowsPhoneDownload = new System.Windows.Forms.ToolStripMenuItem();
            this.menuWindowsPhoneInformation = new System.Windows.Forms.ToolStripMenuItem();
            this.menuReinstallKeyboardAndMouseHook = new System.Windows.Forms.ToolStripMenuItem();
            this.menuMachineMatrix = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem2 = new System.Windows.Forms.ToolStripSeparator();
            this.MenuAllPC = new System.Windows.Forms.ToolStripMenuItem();
            this.dUCTDOToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tRUONG2DToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.NotifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.picLogonLogo = new System.Windows.Forms.PictureBox();
            this.MainMenu.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picLogonLogo)).BeginInit();
            this.SuspendLayout();
            // 
            // MainMenu
            // 
            this.MainMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuExit,
            this.toolStripSeparator2,
            this.menuAbout,
            this.menuHelp,
            this.menuGenDumpFile,
            this.toolStripSeparator1,
            this.menuGetScreenCapture,
            this.menuSendScreenCapture,
            this.toolStripMenuItem1,
            this.menuWindowsPhone,
            this.menuReinstallKeyboardAndMouseHook,
            this.menuMachineMatrix,
            this.toolStripMenuItem2,
            this.MenuAllPC,
            this.dUCTDOToolStripMenuItem,
            this.tRUONG2DToolStripMenuItem});
            this.MainMenu.Name = "MainMenu";
            this.MainMenu.Size = new System.Drawing.Size(197, 292);
            this.MainMenu.Opening += new System.ComponentModel.CancelEventHandler(this.MainMenu_Opening);
            this.MainMenu.MouseLeave += new System.EventHandler(this.MainMenu_MouseLeave);
            // 
            // menuExit
            // 
            this.menuExit.ForeColor = System.Drawing.Color.Black;
            this.menuExit.Name = "menuExit";
            this.menuExit.Size = new System.Drawing.Size(196, 22);
            this.menuExit.Text = "&Exit";
            this.menuExit.Click += new System.EventHandler(this.MenuExit_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(193, 6);
            // 
            // menuAbout
            // 
            this.menuAbout.ForeColor = System.Drawing.Color.Black;
            this.menuAbout.Name = "menuAbout";
            this.menuAbout.Size = new System.Drawing.Size(196, 22);
            this.menuAbout.Text = "A&bout";
            this.menuAbout.Click += new System.EventHandler(this.MenuAbout_Click);
            // 
            // menuHelp
            // 
            this.menuHelp.Name = "menuHelp";
            this.menuHelp.Size = new System.Drawing.Size(196, 22);
            this.menuHelp.Text = "&Help && Questions";
            this.menuHelp.Click += new System.EventHandler(this.MenuHelp_Click);
            // 
            // menuGenDumpFile
            // 
            this.menuGenDumpFile.Name = "menuGenDumpFile";
            this.menuGenDumpFile.Size = new System.Drawing.Size(196, 22);
            this.menuGenDumpFile.Text = "&Generate log";
            this.menuGenDumpFile.ToolTipText = "Create logfile for triage, logfile will be generated under program directory.";
            this.menuGenDumpFile.Visible = false;
            this.menuGenDumpFile.Click += new System.EventHandler(this.MenuGenDumpFile_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(193, 6);
            // 
            // menuGetScreenCapture
            // 
            this.menuGetScreenCapture.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuGetFromAll});
            this.menuGetScreenCapture.ForeColor = System.Drawing.Color.Black;
            this.menuGetScreenCapture.Name = "menuGetScreenCapture";
            this.menuGetScreenCapture.Size = new System.Drawing.Size(196, 22);
            this.menuGetScreenCapture.Text = "&Get Screen Capture from";
            // 
            // menuGetFromAll
            // 
            this.menuGetFromAll.Enabled = false;
            this.menuGetFromAll.Name = "menuGetFromAll";
            this.menuGetFromAll.Size = new System.Drawing.Size(85, 22);
            this.menuGetFromAll.Text = "All";
            this.menuGetFromAll.Click += new System.EventHandler(this.MenuGetScreenCaptureClick);
            // 
            // menuSendScreenCapture
            // 
            this.menuSendScreenCapture.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.allToolStripMenuItem,
            this.menuSend2Myself});
            this.menuSendScreenCapture.ForeColor = System.Drawing.Color.Black;
            this.menuSendScreenCapture.Name = "menuSendScreenCapture";
            this.menuSendScreenCapture.Size = new System.Drawing.Size(196, 22);
            this.menuSendScreenCapture.Text = "Send Screen &Capture to";
            // 
            // allToolStripMenuItem
            // 
            this.allToolStripMenuItem.Name = "allToolStripMenuItem";
            this.allToolStripMenuItem.Size = new System.Drawing.Size(105, 22);
            this.allToolStripMenuItem.Text = "All";
            this.allToolStripMenuItem.Click += new System.EventHandler(this.MenuSendScreenCaptureClick);
            // 
            // menuSend2Myself
            // 
            this.menuSend2Myself.Name = "menuSend2Myself";
            this.menuSend2Myself.Size = new System.Drawing.Size(105, 22);
            this.menuSend2Myself.Text = "Myself";
            this.menuSend2Myself.Click += new System.EventHandler(this.MenuSendScreenCaptureClick);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(193, 6);
            // 
            // menuReinstallKeyboardAndMouseHook
            // 
            this.menuReinstallKeyboardAndMouseHook.Name = "menuReinstallKeyboardAndMouseHook";
            this.menuReinstallKeyboardAndMouseHook.Size = new System.Drawing.Size(196, 22);
            this.menuReinstallKeyboardAndMouseHook.Text = "&Reinstall Input hook";
            this.menuReinstallKeyboardAndMouseHook.ToolTipText = "This might help when keyboard/Mouse redirection stops working";
            this.menuReinstallKeyboardAndMouseHook.Visible = false;
            this.menuReinstallKeyboardAndMouseHook.Click += new System.EventHandler(this.MenuReinstallKeyboardAndMouseHook_Click);
            // 
            // menuMachineMatrix
            // 
            this.menuMachineMatrix.Font = new System.Drawing.Font(System.Windows.Forms.Control.DefaultFont.Name, System.Windows.Forms.Control.DefaultFont.Size, System.Drawing.FontStyle.Bold);
            this.menuMachineMatrix.ForeColor = System.Drawing.Color.Black;
            this.menuMachineMatrix.Name = "menuMachineMatrix";
            this.menuMachineMatrix.Size = new System.Drawing.Size(196, 22);
            this.menuMachineMatrix.Text = "&Settings";
            this.menuMachineMatrix.Click += new System.EventHandler(this.MenuMachineMatrix_Click);
            // 
            // toolStripMenuItem2
            // 
            this.toolStripMenuItem2.Name = "toolStripMenuItem2";
            this.toolStripMenuItem2.Size = new System.Drawing.Size(193, 6);
            // 
            // menuAllPC
            // 
            this.MenuAllPC.CheckOnClick = true;
            this.MenuAllPC.Name = "menuAllPC";
            this.MenuAllPC.Size = new System.Drawing.Size(196, 22);
            this.MenuAllPC.Text = "&ALL COMPUTERS";
            this.MenuAllPC.ToolTipText = "Repeat Mouse/keyboard in all machines.";
            this.MenuAllPC.Click += new System.EventHandler(this.MenuAllPC_Click);
            // 
            // dUCTDOToolStripMenuItem
            // 
            this.dUCTDOToolStripMenuItem.Name = "dUCTDOToolStripMenuItem";
            this.dUCTDOToolStripMenuItem.Size = new System.Drawing.Size(196, 22);
            this.dUCTDOToolStripMenuItem.Tag = "MACHINE: TEST1";
            this.dUCTDOToolStripMenuItem.Text = "DUCTDO";
            // 
            // tRUONG2DToolStripMenuItem
            // 
            this.tRUONG2DToolStripMenuItem.Name = "tRUONG2DToolStripMenuItem";
            this.tRUONG2DToolStripMenuItem.Size = new System.Drawing.Size(196, 22);
            this.tRUONG2DToolStripMenuItem.Tag = "MACHINE: TEST2";
            this.tRUONG2DToolStripMenuItem.Text = "TRUONG2D";
            // 
            // notifyIcon
            // 
            this.NotifyIcon.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info;
            this.NotifyIcon.BalloonTipText = "Microsoft® Visual Studio® 2010";
            this.NotifyIcon.BalloonTipTitle = "Microsoft® Visual Studio® 2010";
            this.NotifyIcon.ContextMenuStrip = this.MainMenu;
            this.NotifyIcon.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon.Icon")));
            this.NotifyIcon.Text = "Microsoft® Visual Studio® 2010";
            this.NotifyIcon.MouseDown += new System.Windows.Forms.MouseEventHandler(this.NotifyIcon_MouseDown);
            // 
            // picLogonLogo
            // 
            this.picLogonLogo.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.picLogonLogo.Image = global::MouseWithoutBorders.Properties.Images.MouseWithoutBorders;
            this.picLogonLogo.Location = new System.Drawing.Point(99, 62);
            this.picLogonLogo.Name = "picLogonLogo";
            this.picLogonLogo.Size = new System.Drawing.Size(95, 17);
            this.picLogonLogo.TabIndex = 1;
            this.picLogonLogo.TabStop = false;
            this.picLogonLogo.Visible = false;
            // 
            // frmScreen
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Black;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.ClientSize = new System.Drawing.Size(10, 10);
            this.ControlBox = false;
            this.Controls.Add(this.picLogonLogo);
            this.Cursor = System.Windows.Forms.Cursors.Default;
            this.Enabled = false;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "frmScreen";
            this.Opacity = 0.5D;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "Microsoft® Visual Studio® 2010 Application";
            this.TopMost = true;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FrmScreen_FormClosing);
            this.Load += new System.EventHandler(this.FrmScreen_Load);
            this.Shown += new System.EventHandler(this.FrmScreen_Shown);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.FrmScreen_MouseMove);
            this.MainMenu.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.picLogonLogo)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ToolStripMenuItem menuExit;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem menuMachineMatrix;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem2;
        private System.Windows.Forms.ToolStripMenuItem dUCTDOToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem tRUONG2DToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem menuSendScreenCapture;
        private System.Windows.Forms.ToolStripMenuItem menuSend2Myself;
        private System.Windows.Forms.ToolStripMenuItem menuGetScreenCapture;
        private System.Windows.Forms.ToolStripMenuItem menuGetFromAll;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem allToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem menuAbout;
        private System.Windows.Forms.PictureBox picLogonLogo;
        private System.Windows.Forms.ToolStripMenuItem menuHelp;
        private System.Windows.Forms.ToolStripMenuItem menuReinstallKeyboardAndMouseHook;
        private System.Windows.Forms.ToolStripMenuItem menuGenDumpFile;
        private System.Windows.Forms.ToolStripMenuItem menuWindowsPhone;
        private System.Windows.Forms.ToolStripMenuItem menuWindowsPhoneEnable;
        private System.Windows.Forms.ToolStripMenuItem menuWindowsPhoneInformation;
        private System.Windows.Forms.ToolStripMenuItem menuWindowsPhoneDownload;
    }
}

