namespace ThePerezidentsCiv3DesktopTools
{
    partial class ModalDialog
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
            lblMessage = new Label();
            btnConfirm = new Button();
            btnCancel = new Button();
            SuspendLayout();

            // lblMessage
            lblMessage.Location = new Point(12, 12);
            lblMessage.Size = new Size(360, 60);
            lblMessage.TextAlign = ContentAlignment.MiddleCenter;

            // btnConfirm
            btnConfirm.Location = new Point(12, 84);
            btnConfirm.Size = new Size(170, 30);
            btnConfirm.Click += btnConfirm_Click;

            // btnCancel
            btnCancel.Location = new Point(202, 84);
            btnCancel.Size = new Size(170, 30);
            btnCancel.Click += btnCancel_Click;

            // ModalDialog
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(384, 126);
            Controls.Add(lblMessage);
            Controls.Add(btnConfirm);
            Controls.Add(btnCancel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ResumeLayout(false);
        }

        #endregion

        protected Label lblMessage;
        protected Button btnConfirm;
        protected Button btnCancel;
    }
}
