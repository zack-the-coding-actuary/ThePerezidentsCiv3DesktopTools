namespace ThePerezidentsCiv3DesktopTools
{
    public partial class Main : Form
    {
        public Main()
        {
            InitializeComponent();
        }

        private void btnExportUnitsToCsv_Click(object sender, EventArgs e)
        {
            ExportUnitsToCsvDialog.Show();
        }
    }
}
