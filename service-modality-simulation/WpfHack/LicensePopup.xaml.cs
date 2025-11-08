using System.Windows;
using LicenseValidatorLibrary;
using BluetoothConnection;

namespace WpfHack;

public partial class LicensePopup : Window
{
    public LicensePopup()
    {
        InitializeComponent();
        this.Loaded += LicensePopup_Loaded;
        this.Closed += LicensePopup_Closed;
        
        BluetoothConnection.BluetoothConnection.LicenseValidated += OnLicenseValidated;

    }
    
    private async void LicensePopup_Loaded(object? sender, RoutedEventArgs e)
    {
        await BluetoothConnection.BluetoothConnection.StartAdvertisingAsync();
    }
    
    private void LicensePopup_Closed(object? sender, EventArgs e)
    {
        BluetoothConnection.BluetoothConnection.StopAdvertising();
        BluetoothConnection.BluetoothConnection.LicenseValidated -= OnLicenseValidated;

    }
    
    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        string inputKey = LicenseKeyBox.Text.Trim();

        Console.WriteLine(inputKey);

        // Get validation result and level
        var (isValid, payload, error) = LicenseValidator.ValidateJwtToken(inputKey);

        Console.WriteLine(error);

        if (isValid && payload != null)
        {
            // Show our custom larger dialog with optional GIF
            var message = $"✅ License key is valid!\n\nEmail: {payload.Email}\nLevel: {payload.Level}\nDevice: {payload.DeviceSerial}";
            var win = new ValidationResultWindow(message, "Validation Success", "Assets/unlock.gif");
            win.Owner = this;
            win.ShowDialog();
        }
        else
        {
            var win = new ValidationResultWindow("❌ Invalid license key.", "Validation Failed", "Assets/lock.gif");
            win.Owner = this;
            win.ShowDialog();
        }
    }
    
    private void OnLicenseValidated()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var win = new ValidationResultWindow("✅ License key is valid!", "Validation Success", "Assets/unlock.gif");
            win.Owner = this;
            win.ShowDialog();

            this.Close();
        }));
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
}