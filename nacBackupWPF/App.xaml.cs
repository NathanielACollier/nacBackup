using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;

namespace nacBackupWPF;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private async void App_OnStartup(object sender, StartupEventArgs e)
    {
        try
        {

                
            //runWPF();
            runAvalonia();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Exception details: {ex}", "Error on app setup");
        }
        finally
        {
            App.Current.Shutdown();
        }
    }


    private void runAvalonia()
    {
        var avMainWin = new Window1_Avalonia();
        avMainWin.Run();
    }

    private void runWPF()
    {
        var mainWin = new Window1();
        mainWin.ShowDialog();
    }
}

