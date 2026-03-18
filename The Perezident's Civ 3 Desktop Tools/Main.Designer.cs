namespace ThePerezidentsCiv3DesktopTools
{
    partial class Main
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

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            panelBiq = new GroupBox();
            btnExportUnitsToCsv = new Button();
            panelSav = new GroupBox();
            panelBiq.SuspendLayout();
            SuspendLayout();
            // 
            // panelBiq
            // 
            panelBiq.Controls.Add(btnExportUnitsToCsv);
            panelBiq.Location = new Point(12, 12);
            panelBiq.Name = "panelBiq";
            panelBiq.Size = new Size(200, 300);
            panelBiq.TabIndex = 0;
            panelBiq.TabStop = false;
            panelBiq.Text = ".BIQ Functions";
            // 
            // btnExportUnitsToCsv
            // 
            btnExportUnitsToCsv.Location = new Point(10, 25);
            btnExportUnitsToCsv.Name = "btnExportUnitsToCsv";
            btnExportUnitsToCsv.Size = new Size(180, 30);
            btnExportUnitsToCsv.TabIndex = 0;
            btnExportUnitsToCsv.Text = "Export units to CSV";
            btnExportUnitsToCsv.Click += btnExportUnitsToCsv_Click;
            // 
            // panelSav
            // 
            panelSav.Location = new Point(224, 12);
            panelSav.Name = "panelSav";
            panelSav.Size = new Size(200, 300);
            panelSav.TabIndex = 1;
            panelSav.TabStop = false;
            panelSav.Text = ".SAV Functions";
            // 
            // Main
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(436, 324);
            Controls.Add(panelBiq);
            Controls.Add(panelSav);
            Name = "Main";
            Text = "The Perezident's Civ 3 Desktop Tools";
            panelBiq.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private GroupBox panelBiq;
        private GroupBox panelSav;
        private Button btnExportUnitsToCsv;
    }
}
