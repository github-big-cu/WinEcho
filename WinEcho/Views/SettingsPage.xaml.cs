using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using Windows.Media.Capture;
using WinEcho;
using Windows.Web.Http;
using Windows.Web.Http.Headers;
using System.Net.Http;
using Windows.Security;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Media.Audio;
using Newtonsoft.Json;

namespace WinEcho.Views
{
    /// <summary>
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

        public SettingsPage()
        {
            this.InitializeComponent();

            // Check to see if we have a non-expired access token
            if (localSettings.Values["clientId"] != null && localSettings.Values["clientId"] != null)
                txtClientId.Text = Convert.ToString(localSettings.Values["clientId"]);
            if (localSettings.Values["clientSecret"] != null && localSettings.Values["clientSecret"] != null)
                txtClientSecret.Text = Convert.ToString(localSettings.Values["clientSecret"]);
            if (localSettings.Values["productId"] != null && localSettings.Values["productId"] != null)
                txtProductId.Text = Convert.ToString(localSettings.Values["productId"]);
            if (localSettings.Values["deviceSerial"] != null && localSettings.Values["deviceSerial"] != null)
                txtDeviceSerial.Text = Convert.ToString(localSettings.Values["deviceSerial"]);
            if (localSettings.Values["oAuthReidrectURL"] != null && localSettings.Values["oAuthReidrectURL"] != null)
                txtoAuthURL.Text = Convert.ToString(localSettings.Values["oAuthReidrectURL"]);
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                localSettings.Values["clientId"] = txtClientId.Text;
                localSettings.Values["clientSecret"] = txtClientSecret.Text;
                localSettings.Values["productId"] = txtProductId.Text;
                localSettings.Values["deviceSerial"] = txtDeviceSerial.Text;
                localSettings.Values["oAuthReidrectURL"] = txtoAuthURL.Text;
            }
            catch (Exception)
            {

            }
        }

        private void btnClearAccessToken_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AVSInterface.AVSOAuth.ResetOAuth("AccessToken");
            }
            catch (Exception)
            {
            }
        }

        private void btnClearOAuth_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AVSInterface.AVSOAuth.ResetOAuth("");
            }
            catch (Exception)
            {

            }
        }
    }
}


    

