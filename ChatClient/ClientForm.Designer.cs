namespace ChatClient
{
    partial class ClientForm
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
            connectionPanel = new FlowLayoutPanel();
            hostLabel = new Label();
            hostTextBox = new TextBox();
            portLabel = new Label();
            portUpDown = new NumericUpDown();
            usernameLabel = new Label();
            usernameTextBox = new TextBox();
            connectButton = new Button();
            disconnectButton = new Button();
            chatBox = new RichTextBox();
            onlineGroup = new GroupBox();
            onlineListBox = new ListBox();
            messagePanel = new Panel();
            messageTextBox = new TextBox();
            sendButton = new Button();
            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel();
            onlineGroup.SuspendLayout();
            messagePanel.SuspendLayout();
            connectionPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)portUpDown).BeginInit();
            statusStrip.SuspendLayout();
            SuspendLayout();
            //
            // connectionPanel
            //
            connectionPanel.AutoSize = true;
            connectionPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            connectionPanel.Controls.Add(hostLabel);
            connectionPanel.Controls.Add(hostTextBox);
            connectionPanel.Controls.Add(portLabel);
            connectionPanel.Controls.Add(portUpDown);
            connectionPanel.Controls.Add(usernameLabel);
            connectionPanel.Controls.Add(usernameTextBox);
            connectionPanel.Controls.Add(connectButton);
            connectionPanel.Controls.Add(disconnectButton);
            connectionPanel.Dock = DockStyle.Top;
            connectionPanel.Location = new Point(0, 0);
            connectionPanel.Name = "connectionPanel";
            connectionPanel.Padding = new Padding(8, 6, 8, 6);
            connectionPanel.Size = new Size(840, 41);
            connectionPanel.TabIndex = 0;
            //
            // hostLabel
            //
            hostLabel.AutoSize = true;
            hostLabel.Margin = new Padding(3, 6, 3, 0);
            hostLabel.Name = "hostLabel";
            hostLabel.TabIndex = 0;
            hostLabel.Text = "Sunucu:";
            //
            // hostTextBox
            //
            hostTextBox.Location = new Point(69, 9);
            hostTextBox.Name = "hostTextBox";
            hostTextBox.Size = new Size(130, 23);
            hostTextBox.TabIndex = 1;
            //
            // portLabel
            //
            portLabel.AutoSize = true;
            portLabel.Margin = new Padding(3, 6, 3, 0);
            portLabel.Name = "portLabel";
            portLabel.TabIndex = 2;
            portLabel.Text = "Port:";
            //
            // portUpDown
            //
            portUpDown.Location = new Point(245, 9);
            portUpDown.Name = "portUpDown";
            portUpDown.Size = new Size(72, 23);
            portUpDown.TabIndex = 3;
            //
            // usernameLabel
            //
            usernameLabel.AutoSize = true;
            usernameLabel.Margin = new Padding(3, 6, 3, 0);
            usernameLabel.Name = "usernameLabel";
            usernameLabel.TabIndex = 4;
            usernameLabel.Text = "Kullanıcı adı:";
            //
            // usernameTextBox
            //
            usernameTextBox.Location = new Point(417, 9);
            usernameTextBox.Name = "usernameTextBox";
            usernameTextBox.Size = new Size(130, 23);
            usernameTextBox.TabIndex = 5;
            //
            // connectButton
            //
            connectButton.Location = new Point(553, 8);
            connectButton.Name = "connectButton";
            connectButton.Size = new Size(90, 26);
            connectButton.TabIndex = 6;
            connectButton.Text = "Bağlan";
            connectButton.UseVisualStyleBackColor = true;
            connectButton.Click += ConnectButton_Click;
            //
            // disconnectButton
            //
            disconnectButton.Location = new Point(649, 8);
            disconnectButton.Name = "disconnectButton";
            disconnectButton.Size = new Size(120, 26);
            disconnectButton.TabIndex = 7;
            disconnectButton.Text = "Bağlantıyı Kes";
            disconnectButton.UseVisualStyleBackColor = true;
            disconnectButton.Click += DisconnectButton_Click;
            //
            // chatBox
            //
            chatBox.BackColor = SystemColors.Window;
            chatBox.BorderStyle = BorderStyle.FixedSingle;
            chatBox.Dock = DockStyle.Fill;
            chatBox.Location = new Point(0, 41);
            chatBox.Name = "chatBox";
            chatBox.ReadOnly = true;
            chatBox.Size = new Size(840, 440);
            chatBox.TabIndex = 1;
            chatBox.Text = "";
            //
            // onlineGroup
            //
            onlineGroup.Controls.Add(onlineListBox);
            onlineGroup.Dock = DockStyle.Right;
            onlineGroup.Location = new Point(660, 41);
            onlineGroup.Name = "onlineGroup";
            onlineGroup.Padding = new Padding(8, 4, 8, 8);
            onlineGroup.Size = new Size(180, 396);
            onlineGroup.TabIndex = 4;
            onlineGroup.TabStop = false;
            onlineGroup.Text = "Çevrimiçi (0)";
            //
            // onlineListBox
            //
            onlineListBox.BorderStyle = BorderStyle.FixedSingle;
            onlineListBox.Dock = DockStyle.Fill;
            onlineListBox.FormattingEnabled = true;
            onlineListBox.Location = new Point(8, 20);
            onlineListBox.Name = "onlineListBox";
            onlineListBox.Size = new Size(164, 368);
            onlineListBox.TabIndex = 0;
            //
            // messagePanel
            //
            messagePanel.Controls.Add(messageTextBox);
            messagePanel.Controls.Add(sendButton);
            messagePanel.Dock = DockStyle.Bottom;
            messagePanel.Location = new Point(0, 437);
            messagePanel.Name = "messagePanel";
            messagePanel.Size = new Size(840, 44);
            messagePanel.TabIndex = 3;
            //
            // messageTextBox
            //
            messageTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            messageTextBox.Location = new Point(11, 10);
            messageTextBox.Name = "messageTextBox";
            messageTextBox.PlaceholderText = "Mesajınızı yazın ve Enter'a basın";
            messageTextBox.Size = new Size(718, 23);
            messageTextBox.TabIndex = 0;
            //
            // sendButton
            //
            sendButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sendButton.Location = new Point(737, 8);
            sendButton.Name = "sendButton";
            sendButton.Size = new Size(92, 27);
            sendButton.TabIndex = 1;
            sendButton.Text = "Gönder";
            sendButton.UseVisualStyleBackColor = true;
            sendButton.Click += SendButton_Click;
            //
            // statusStrip
            //
            statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel });
            statusStrip.Location = new Point(0, 481);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new Size(840, 22);
            statusStrip.TabIndex = 2;
            //
            // statusLabel
            //
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(71, 17);
            statusLabel.Text = "Bağlı değil";
            //
            // ClientForm
            //
            AcceptButton = connectButton;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(840, 503);
            Controls.Add(chatBox);
            Controls.Add(onlineGroup);
            Controls.Add(messagePanel);
            Controls.Add(connectionPanel);
            Controls.Add(statusStrip);
            MinimumSize = new Size(560, 360);
            Name = "ClientForm";
            Text = "ChatApp İstemci";
            onlineGroup.ResumeLayout(false);
            messagePanel.ResumeLayout(false);
            messagePanel.PerformLayout();
            connectionPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)portUpDown).EndInit();
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private FlowLayoutPanel connectionPanel;
        private Label hostLabel;
        private TextBox hostTextBox;
        private Label portLabel;
        private NumericUpDown portUpDown;
        private Label usernameLabel;
        private TextBox usernameTextBox;
        private Button connectButton;
        private Button disconnectButton;
        private RichTextBox chatBox;
        private GroupBox onlineGroup;
        private ListBox onlineListBox;
        private Panel messagePanel;
        private TextBox messageTextBox;
        private Button sendButton;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;
    }
}
