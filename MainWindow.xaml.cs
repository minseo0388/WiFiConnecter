using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace WiFiConnecter
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<WiFiNetworkInfo> WiFiNetworks { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            WiFiNetworks = new ObservableCollection<WiFiNetworkInfo>();
            WiFiListView.ItemsSource = WiFiNetworks;
            
            // Add event handler for SSID text change
            SsidTextBox.TextChanged += SsidTextBox_TextChanged;
            
            // Load WiFi networks on startup
            _ = LoadWiFiNetworks();
        }

        private void SsidTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Enable connect button if SSID is not empty
            ConnectButton.IsEnabled = !string.IsNullOrEmpty(SsidTextBox.Text.Trim());
            
            if (!string.IsNullOrEmpty(SsidTextBox.Text.Trim()))
            {
                StatusTextBlock.Text = "Enter WiFi password and click Connect.";
            }
            else
            {
                StatusTextBlock.Text = "Enter WiFi network name (SSID) or select from list.";
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadWiFiNetworks();
        }

        private async Task LoadWiFiNetworks()
        {
            try
            {
                StatusTextBlock.Text = "Scanning for WiFi networks...";
                RefreshButton.IsEnabled = false;
                
                WiFiNetworks.Clear();
                
                await Task.Run(() =>
                {
                    try
                    {
                        // Get available WiFi networks using netsh wlan show network
                        ProcessStartInfo startInfo = new ProcessStartInfo()
                        {
                            FileName = "netsh",
                            Arguments = "wlan show network",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        };

                        string output = "";
                        using (Process process = Process.Start(startInfo))
                        {
                            output = process.StandardOutput.ReadToEnd();
                            process.WaitForExit();
                            var lines = output.Split('\n');
                            string currentSsid = null;
                            string currentSecurity = null;
                            foreach (var line in lines)
                            {
                                string trimmedLine = line.Trim();
                                if (trimmedLine.StartsWith("SSID") && trimmedLine.Contains(":"))
                                {
                                    // If we have a previous block, add it
                                    if (!string.IsNullOrEmpty(currentSsid) && !string.IsNullOrEmpty(currentSecurity))
                                    {
                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            var existing = WiFiNetworks.FirstOrDefault(w => w.Ssid == currentSsid);
                                            if (existing == null)
                                            {
                                                WiFiNetworks.Add(new WiFiNetworkInfo
                                                {
                                                    Ssid = currentSsid,
                                                    SignalStrength = 0,
                                                    Security = currentSecurity,
                                                    IsConnected = "No"
                                                });
                                            }
                                        });
                                    }
                                    // Start new block
                                    var parts = trimmedLine.Split(':', 2);
                                    if (parts.Length > 1)
                                    {
                                        var ssid = parts[1].Trim();
                                        if (string.IsNullOrEmpty(ssid))
                                            ssid = "[Hidden Network]";
                                        currentSsid = ssid;
                                        currentSecurity = null;
                                    }
                                }
                                else if ((trimmedLine.StartsWith("인증") || trimmedLine.StartsWith("Authentication")) && trimmedLine.Contains(":"))
                                {
                                    var parts = trimmedLine.Split(':', 2);
                                    if (parts.Length > 1)
                                    {
                                        string auth = parts[1].Trim().ToLower();
                                        if (auth.Contains("열림") || auth.Contains("open"))
                                            currentSecurity = "Open";
                                        else if (auth.Contains("wpa2-개인") || auth.Contains("wpa2-personal"))
                                            currentSecurity = "WPA2";
                                        else if (auth.Contains("wpa2-엔터프라이즈") || auth.Contains("wpa2-enterprise"))
                                            currentSecurity = "WPA2-Enterprise";
                                        else if (auth.Contains("wpa3"))
                                            currentSecurity = "WPA3";
                                        else if (auth.Contains("wpa"))
                                            currentSecurity = "WPA";
                                        else if (auth.Contains("wep"))
                                            currentSecurity = "WEP";
                                        else
                                            currentSecurity = "Secured";
                                    }
                                }
                            }
                            // Add the last block if valid
                            if (!string.IsNullOrEmpty(currentSsid) && !string.IsNullOrEmpty(currentSecurity))
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    var existing = WiFiNetworks.FirstOrDefault(w => w.Ssid == currentSsid);
                                    if (existing == null)
                                    {
                                        WiFiNetworks.Add(new WiFiNetworkInfo
                                        {
                                            Ssid = currentSsid,
                                            SignalStrength = 0,
                                            Security = currentSecurity,
                                            IsConnected = "No"
                                        });
                                    }
                                });
                            }
                        }
                        // If no networks found, show the raw netsh output for debugging
                        if (WiFiNetworks.Count == 0 && !string.IsNullOrWhiteSpace(output))
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show(output, "netsh wlan show network output", MessageBoxButton.OK, MessageBoxImage.Information);
                            });
                        }

                        // Get signal strength information
                        startInfo.Arguments = "wlan show network mode=bssid";
                        using (Process process = Process.Start(startInfo))
                        {
                            string bssidOutput = process.StandardOutput.ReadToEnd();
                            process.WaitForExit();
                            var lines = bssidOutput.Split('\n');
                            string currentSsid = "";
                            foreach (var line in lines)
                            {
                                string trimmedLine = line.Trim();
                                // Look for SSID lines
                                if (trimmedLine.StartsWith("SSID") && trimmedLine.Contains(":"))
                                {
                                    var parts = trimmedLine.Split(':', 2);
                                    if (parts.Length > 1)
                                    {
                                        currentSsid = parts[1].Trim();
                                        if (string.IsNullOrEmpty(currentSsid))
                                        {
                                            currentSsid = "[Hidden Network]";
                                        }
                                    }
                                }
                                // Look for Signal strength (Korean: "신호")
                                else if ((trimmedLine.StartsWith("신호") || trimmedLine.StartsWith("Signal")) && trimmedLine.Contains(":"))
                                {
                                    var parts = trimmedLine.Split(':', 2);
                                    if (parts.Length > 1)
                                    {
                                        string signal = parts[1].Trim().Replace("%", "");
                                        if (int.TryParse(signal, out int signalValue))
                                        {
                                            // Update the signal strength for this network
                                            Application.Current.Dispatcher.Invoke(() =>
                                            {
                                                var network = WiFiNetworks.FirstOrDefault(w => w.Ssid == currentSsid);
                                                if (network != null)
                                                {
                                                    network.SignalStrength = signalValue;
                                                }
                                            });
                                        }
                                    }
                                }
                            }
                        }

                        // Check connected networks
                        startInfo.Arguments = "wlan show interfaces";
                        using (Process process = Process.Start(startInfo))
                        {
                            string ifaceOutput = process.StandardOutput.ReadToEnd();
                            process.WaitForExit();
                            var lines = ifaceOutput.Split('\n');
                            foreach (var line in lines)
                            {
                                if ((line.Contains("SSID") || line.Contains("프로필")) && line.Contains(":") && !line.Contains("BSSID"))
                                {
                                    var parts = line.Split(':', 2);
                                    if (parts.Length > 1)
                                    {
                                        string connectedSsid = parts[1].Trim();
                                        if (!string.IsNullOrEmpty(connectedSsid))
                                        {
                                            Application.Current.Dispatcher.Invoke(() =>
                                            {
                                                var network = WiFiNetworks.FirstOrDefault(w => w.Ssid == connectedSsid);
                                                if (network != null)
                                                {
                                                    network.IsConnected = "Yes";
                                                }
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            StatusTextBlock.Text = $"Error scanning networks: {ex.Message}";
                        });
                    }
                });
                
                StatusTextBlock.Text = $"Found {WiFiNetworks.Count} WiFi networks.";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Error scanning networks: {ex.Message}";
                MessageBox.Show($"Error scanning WiFi networks: {ex.Message}", "Error", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RefreshButton.IsEnabled = true;
            }
        }

        private void WiFiListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WiFiListView.SelectedItem is WiFiNetworkInfo selectedNetwork)
            {
                SsidTextBox.Text = selectedNetwork.Ssid;
                StatusTextBlock.Text = $"Selected: {selectedNetwork.Ssid} ({selectedNetwork.Security}) - Enter password and click Connect.";
            }
            
            // Enable connect button if SSID is not empty
            ConnectButton.IsEnabled = !string.IsNullOrEmpty(SsidTextBox.Text.Trim());
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            string ssid = SsidTextBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrEmpty(ssid))
            {
                MessageBox.Show("Please enter the WiFi network name (SSID).", "Missing SSID", 
                               MessageBoxButton.OK, MessageBoxImage.Warning);
                SsidTextBox.Focus();
                return;
            }

            // Check if it's an open network (no password required)
            var selectedNetwork = WiFiNetworks.FirstOrDefault(w => w.Ssid.Equals(ssid, StringComparison.OrdinalIgnoreCase));
            bool isOpenNetwork = selectedNetwork?.Security == "Open";

            if (!isOpenNetwork && string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Please enter the WiFi password.", "Missing Password", 
                               MessageBoxButton.OK, MessageBoxImage.Warning);
                PasswordBox.Focus();
                return;
            }

            await ConnectToWiFi(ssid, password);
        }

        private async Task ConnectToWiFi(string ssid, string password)
        {
            try
            {
                // Show progress
                StatusTextBlock.Text = $"Connecting to {ssid}...";
                ConnectionProgressBar.Visibility = Visibility.Visible;
                ConnectButton.IsEnabled = false;
                RefreshButton.IsEnabled = false;

                bool success = await Task.Run(() =>
                {
                    try
                    {
                        // Create WiFi profile XML
                        string profileXml = CreateWiFiProfile(ssid, password);
                        
                        // Save profile to temporary file
                        string tempFile = System.IO.Path.GetTempFileName() + ".xml";
                        System.IO.File.WriteAllText(tempFile, profileXml);

                        try
                        {
                            // Add the profile using netsh
                            ProcessStartInfo startInfo = new ProcessStartInfo()
                            {
                                FileName = "netsh",
                                Arguments = $"wlan add profile filename=\"{tempFile}\"",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true
                            };

                            string addProfileOutput = "";
                            using (Process process = Process.Start(startInfo))
                            {
                                addProfileOutput = process.StandardOutput.ReadToEnd();
                                string errorOutput = process.StandardError.ReadToEnd();
                                process.WaitForExit();
                                
                                if (process.ExitCode != 0)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Add profile failed: {addProfileOutput} {errorOutput}");
                                    // Continue anyway, profile might already exist
                                }
                            }

                            // Connect to the network
                            startInfo.Arguments = $"wlan connect name=\"{ssid}\"";
                            using (Process process = Process.Start(startInfo))
                            {
                                string connectOutput = process.StandardOutput.ReadToEnd();
                                string errorOutput = process.StandardError.ReadToEnd();
                                process.WaitForExit();
                                
                                System.Diagnostics.Debug.WriteLine($"Connect output: {connectOutput}");
                                if (!string.IsNullOrEmpty(errorOutput))
                                {
                                    System.Diagnostics.Debug.WriteLine($"Connect error: {errorOutput}");
                                }
                                
                                // Wait for connection to establish
                                System.Threading.Thread.Sleep(3000);
                                
                                // Check if we're actually connected
                                return IsConnectedToNetwork(ssid);
                            }
                        }
                        finally
                        {
                            // Clean up temp file
                            if (System.IO.File.Exists(tempFile))
                            {
                                System.IO.File.Delete(tempFile);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Connection exception: {ex.Message}");
                        return false;
                    }
                });

                if (success)
                {
                    StatusTextBlock.Text = $"Successfully connected to {ssid}!";
                    MessageBox.Show($"Successfully connected to {ssid}!", "Connection Successful", 
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Refresh the network list
                    await LoadWiFiNetworks();
                }
                else
                {
                    StatusTextBlock.Text = $"Failed to connect to {ssid}. Please check your credentials.";
                    MessageBox.Show($"Failed to connect to {ssid}. Please check your credentials and try again.", 
                                   "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Connection error: {ex.Message}";
                MessageBox.Show($"Error connecting to WiFi: {ex.Message}", "Connection Error", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Hide progress and re-enable controls
                ConnectionProgressBar.Visibility = Visibility.Collapsed;
                ConnectButton.IsEnabled = true;
                RefreshButton.IsEnabled = true;
            }
        }

        private bool IsConnectedToNetwork(string ssid)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo()
                {
                    FileName = "netsh",
                    Arguments = "wlan show interfaces",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    
                    var lines = output.Split('\n');
                    foreach (var line in lines)
                    {
                        if ((line.Contains("SSID") || line.Contains("프로필")) && line.Contains(":") && !line.Contains("BSSID"))
                        {
                            var parts = line.Split(':', 2);
                            if (parts.Length > 1)
                            {
                                string connectedSsid = parts[1].Trim();
                                if (connectedSsid.Equals(ssid, StringComparison.OrdinalIgnoreCase))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking connection: {ex.Message}");
            }
            return false;
        }

        private string CreateWiFiProfile(string ssid, string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                // Open network profile
                return $@"<?xml version=""1.0""?>
<WLANProfile xmlns=""http://www.microsoft.com/networking/WLAN/profile/v1"">
    <name>{ssid}</name>
    <SSIDConfig>
        <SSID>
            <name>{ssid}</name>
        </SSID>
    </SSIDConfig>
    <connectionType>ESS</connectionType>
    <connectionMode>auto</connectionMode>
    <MSM>
        <security>
            <authEncryption>
                <authentication>open</authentication>
                <encryption>none</encryption>
                <useOneX>false</useOneX>
            </authEncryption>
        </security>
    </MSM>
</WLANProfile>";
            }
            else
            {
                // WPA2 network profile
                return $@"<?xml version=""1.0""?>
<WLANProfile xmlns=""http://www.microsoft.com/networking/WLAN/profile/v1"">
    <name>{ssid}</name>
    <SSIDConfig>
        <SSID>
            <name>{ssid}</name>
        </SSID>
    </SSIDConfig>
    <connectionType>ESS</connectionType>
    <connectionMode>auto</connectionMode>
    <MSM>
        <security>
            <authEncryption>
                <authentication>WPA2PSK</authentication>
                <encryption>AES</encryption>
                <useOneX>false</useOneX>
            </authEncryption>
            <sharedKey>
                <keyType>passPhrase</keyType>
                <protected>false</protected>
                <keyMaterial>{password}</keyMaterial>
            </sharedKey>
        </security>
    </MSM>
</WLANProfile>";
            }
        }
    }

    public class WiFiNetworkInfo
    {
        public string Ssid { get; set; }
        public int SignalStrength { get; set; }
        public string Security { get; set; }
        public string IsConnected { get; set; }
    }
}
