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
            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel();
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
            Controls.Add(topPanel);
            Controls.Add(statusStrip);
            MinimumSize = new Size(560, 360);
            Name = "ServerForm";
            Text = "ChatApp Sunucu";
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
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;
    }
}
