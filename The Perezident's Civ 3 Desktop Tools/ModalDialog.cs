using Civ3Tools;
using QueryCiv3;

namespace ThePerezidentsCiv3DesktopTools
{
    public partial class ModalDialog : Form
    {
        public ModalDialog(string message, string title = "", string confirmText = "OK", string cancelText = "Cancel")
        {
            InitializeComponent();
            lblMessage.Text = message;
            Text = title;
            btnConfirm.Text = confirmText;
            btnCancel.Text = cancelText;
        }

        protected virtual void btnConfirm_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        protected virtual void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    public class ExportUnitsToCsvDialog : ModalDialog
    {
        private static string? ScenarioPath = null;
        private static List<string>? UnitLines = null;
        public ExportUnitsToCsvDialog() : base("No file selected", "Export Unit Data to CSV", "Load scenario file", "Export to CSV")
        {
        }

        public static new void Show()
        {
            using var dialog = new ExportUnitsToCsvDialog();
            dialog.ShowDialog();
        }

        protected override void btnConfirm_Click(object sender, EventArgs e)
        {
            string Civ3Path = Civ3Location.GetCiv3Path();
            using OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Civ3 Scenario Files (*.biq)|*.biq";
            ofd.InitialDirectory = (Civ3Path != "/civ3/path/not/found") ? Civ3Path : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    UnitLines = GetUnitInfo.GetUnitListString(ofd.FileName);
                    ScenarioPath = ofd.FileName;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    ScenarioPath = null;
                }
                finally
                {
                    MessageBox.Show($"Scenario file loaded successfully. Ready to export to CSV.", "Success!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            if (ScenarioPath == null)
                lblMessage.Text = "No file selected";
            else
                lblMessage.Text = Path.GetFileName(ScenarioPath);
        }

        protected override void btnCancel_Click(object sender, EventArgs e)
        {
            if (ScenarioPath == null || UnitLines == null)
            {
                MessageBox.Show("No valid scenario file loaded. Please load a scenario file before exporting.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            string Civ3Path = Civ3Location.GetCiv3Path();
            using SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "CSV files (*.csv)|*.csv";
            saveFileDialog.InitialDirectory = (Civ3Path != "/civ3/path/not/found") ? Civ3Path : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            saveFileDialog.FileName = Path.GetFileNameWithoutExtension(ScenarioPath) + "_units.csv";
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    File.WriteAllLines(saveFileDialog.FileName, UnitLines);
                    MessageBox.Show($"Unit data exported successfully to {saveFileDialog.FileName}", "Success!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error exporting to CSV: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
