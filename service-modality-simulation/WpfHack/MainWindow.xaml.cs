using System.Windows;
using LicenseValidatorLibrary;
using BluetoothConnection;

namespace WpfHack;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
    
    private void OpenLicensePopup_Click(object sender, RoutedEventArgs e)
    {
        var popup = new LicensePopup
        {
            Owner = this
        };
        popup.ShowDialog();
    }
}