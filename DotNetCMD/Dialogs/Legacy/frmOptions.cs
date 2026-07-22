using System.Drawing;
using System.Windows.Forms;

public partial class frmOptions : Form
{
    public frmOptions()
        : this(null)
    {
    }

    public frmOptions(Control control)
        : this((object)control)
    {
    }

    public frmOptions(object selectedObject)
    {
        InitializeComponent();
        ApplyDialogAppearance();
        pg.SelectedObject = selectedObject;
    }

    private void ApplyDialogAppearance()
    {
        Text = DotNetCommander.Language.getString("options");
        labelTitle.Text = Text;
        buttonClose.Text = DotNetCommander.Language.getString("close");

        Font = DotNetCommander.DialogStyleService.CreateDialogFont();
        labelTitle.Font = DotNetCommander.DialogStyleService.CreateHeaderFont();
        pg.Font = DotNetCommander.DialogStyleService.CreateBodyFont();
        pg.CategoryForeColor = Color.FromArgb(55, 93, 129);
        pg.CommandsBackColor = Color.FromArgb(248, 250, 252);
        pg.HelpBackColor = Color.FromArgb(248, 250, 252);
        pg.LineColor = Color.FromArgb(224, 228, 233);
        pg.ViewBackColor = Color.White;
    }
}
