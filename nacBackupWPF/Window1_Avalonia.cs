using System.Threading.Tasks;
using nac.Forms;

namespace nacBackupWPF;

public class Window1_Avalonia
{
    private nac.Forms.Form mainForm;
    
    private nacBackupWPF.MainViewModel myModel => this.mainForm.DataContext as nacBackupWPF.MainViewModel;

    public Window1_Avalonia()
    {
        
    }
    
    public void Run()
    {
        this.mainForm = nac.Forms.Form.NewForm(config =>
        {
            config.AfterSetup(_ =>
            {
                // force light theme
                Avalonia.Application.Current.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
            });
        });

        this.mainForm.DataContext = new nacBackupWPF.MainViewModel();

        buildForm(mainForm);
        mainForm.Display(onDisplay: onFormDisplay);
    }
    
    
    private void buildForm(nac.Forms.Form f)
    {
        f.HorizontalGroup(h =>
        {
            h.Text("nacBackup - ")
                .Text(lib.settings.appVersion, style: "color:blue;");
        });
        
    }
    
    private async Task onFormDisplay(Form arg)
    {
        
    }
    
    
}