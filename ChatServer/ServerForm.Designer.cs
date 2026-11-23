namespace ChatServer
{
    partial class ServerForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            topPanel = new Panel();
            portLabel = new Label();
            portUpDown = new NumericUpDown();
            startButton = new Button();
            stopButton = new Button();
            logBox = new RichTextBox();
            clientsGroup = new GroupBox();
            clientsListView = new ListView();
            userColumn = new ColumnHeader();
            addressColumn = new ColumnHeader();
            connectedColumn = new ColumnHeader();
            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel();
            clientsGroup.SuspendLayout();
            topPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)portUpDown).BeginInit();
            statusStrip.SuspendLayout();
            SuspendLayout();
            //
            // topPanel
            //
            topPanel.Controls.Add(portLabel);
            topPanel.Controls.Add(portUpDown);
            topPanel.Controls.Add(startButton);
            topPanel.Controls.Add(stopButton);
            topPanel.Dock = DockStyle.Top;
            topPanel.Location = new Point(0, 0);
            topPanel.Name = "topPanel";
            topPanel.Size = new Size(720, 48);
            topPanel.TabIndex = 0;
            //
            // portLabel
            //
            portLabel.AutoSize = true;
            portLabel.Location = new Point(12, 16);
            portLabel.Name = "portLabel";
            portLabel.Size = new Size(33, 15);
            portLabel.TabIndex = 0;
            portLabel.Text = "Port:";
            //
            // portUpDown
            //
            portUpDown.Location = new Point(56, 12);
            portUpDown.Name = "portUpDown";
            portUpDown.Size = new Size(80, 23);
            portUpDown.TabIndex = 1;
            //
            // startButton
            //
            startButton.Location = new Point(152, 10);
            startButton.Name = "startButton";
            startButton.Size = new Size(90, 28);
            startButton.TabIndex = 2;
            startButton.Text = "Başlat";
            startButton.UseVisualStyleBackColor = true;
            startButton.Click += StartButton_Click;
            //
            // stopButton
            //
            stopButton.Location = new Point(250, 10);
            stopButton.Name = "stopButton";
            stopButton.Size = new Size(90, 28);
            stopButton.TabIndex = 3;
            stopButton.Text = "Durdur";
            stopButton.UseVisualStyleBackColor = true;
            stopButton.Click += StopButton_Click;
            //
            // logBox
            //
            logBox.BackColor = SystemColors.Window;
            logBox.BorderStyle = BorderStyle.FixedSingle;
            logBox.Dock = DockStyle.Fill;
            logBox.Location = new Point(0, 48);
            logBox.Name = "logBox";
            logBox.ReadOnly = true;
            logBox.Size = new Size(720, 382);
            logBox.TabIndex = 1;
            logBox.Text = "";
            //
            // clientsGroup
            //
            clientsGroup.Controls.Add(clientsListView);
            clientsGroup.Dock = DockStyle.Right;
            clientsGroup.Location = new Point(380, 48);
            clientsGroup.Name = "clientsGroup";
            clientsGroup.Padding = new Padding(8, 4, 8, 8);
            clientsGroup.Size = new Size(340, 382);
            clientsGroup.TabIndex = 3;
            clientsGroup.TabStop = false;
            clientsGroup.Text = "Bağlı istemciler (0)";
            //
            // clientsListView
            //
            clientsListView.Columns.AddRange(new ColumnHeader[] { userColumn, addressColumn, connectedColumn });
            clientsListView.Dock = DockStyle.Fill;
            clientsListView.FullRowSelect = true;
            clientsListView.HideSelection = false;
            clientsListView.Location = new Point(8, 20);
            clientsListView.MultiSelect = false;
            clientsListView.Name = "clientsListView";
            clientsListView.Size = new Size(324, 354);
            clientsListView.TabIndex = 0;
            clientsListView.UseCompatibleStateImageBehavior = false;
            clientsListView.View = View.Details;
            //
            // userColumn
            //
            userColumn.Text = "Kullanıcı ID";
            userColumn.Width = 110;
            //
            // addressColumn
            //
            addressColumn.Text = "Adres";
            addressColumn.Width = 130;
            //
            // connectedColumn
            //
            connectedColumn.Text = "Bağlanma";
            connectedColumn.Width = 70;
            //
            // statusStrip
            //
            statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel });
            statusStrip.Location = new Point(0, 430);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new Size(720, 22);
            statusStrip.TabIndex = 2;
            //
            // statusLabel
            //
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(66, 17);
            statusLabel.Text = "Durduruldu";
            //
            // ServerForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(720, 452);
            Controls.Add(logBox);
            Controls.Add(clientsGroup);
            Controls.Add(topPanel);
            Controls.Add(statusStrip);
            MinimumSize = new Size(640, 360);
            Name = "ServerForm";
            Text = "ChatApp Sunucu";
            clientsGroup.ResumeLayout(false);
            topPanel.ResumeLayout(false);
            topPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)portUpDown).EndInit();
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Panel topPanel;
        private Label portLabel;
        private NumericUpDown portUpDown;
        private Button startButton;
        private Button stopButton;
        private RichTextBox logBox;
        private GroupBox clientsGroup;
        private ListView clientsListView;
        private ColumnHeader userColumn;
        private ColumnHeader addressColumn;
        private ColumnHeader connectedColumn;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;
    }
}
