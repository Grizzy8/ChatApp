namespace ChatClient
{
    partial class ClientForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        // Arayüz büyük ölçüde koyu temaya göre kodla kurulur (bkz. ClientForm.Layout.cs).
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1040, 680);
            MinimumSize = new Size(760, 500);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10F);
            Name = "ClientForm";
            Text = "ChatApp";
            BuildLayout();
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
