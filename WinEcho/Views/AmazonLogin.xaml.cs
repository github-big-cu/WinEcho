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

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace WinEcho.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AmazonLogin : Page
    {
        Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

        public AmazonLogin()
        {
           this.InitializeComponent();
            initPage();           
        }

        private void initPage()
        {
            try
            {
                String strClientId = "";
                if (localSettings.Values["clientId"] != null && localSettings.Values["clientId"] != null)
                    strClientId = Convert.ToString(localSettings.Values["clientId"]);
                String strClientSecret = "";
                if (localSettings.Values["clientSecret"] != null && localSettings.Values["clientSecret"] != null)
                    strClientSecret = Convert.ToString(localSettings.Values["clientSecret"]);
                String strProductId = "";
                if (localSettings.Values["productId"] != null && localSettings.Values["productId"] != null)
                    strProductId = Convert.ToString(localSettings.Values["productId"]);
                String strSerial = "";
                if (localSettings.Values["deviceSerial"] != null && localSettings.Values["deviceSerial"] != null)
                    strSerial = Convert.ToString(localSettings.Values["deviceSerial"]);
                String strOAuthReidrectURL = "";
                if (localSettings.Values["oAuthReidrectURL"] != null && localSettings.Values["oAuthReidrectURL"] != null)
                    strOAuthReidrectURL = Convert.ToString(localSettings.Values["oAuthReidrectURL"]);
                if (!AVSInterface.AVSOAuth.CheckOAuth(strClientId, strClientSecret))
                {
                    AVSInterface.AVSOAuth.StartOAuth(strClientId, strProductId, strSerial, strOAuthReidrectURL, wvAuth);
                }
                else
                {
                    //String message = "You are already logged in.";
                    //resultsbox.Text = message.ToString();
                }
            }
            catch (Exception ex)
            {
                resultsbox.Equals(ex.ToString());
            }
        }

       
        private void goto_echo(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(EchoPage));
        }
    
        
        private async void wvAuth_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            try
            {
                if (args.IsSuccess == true)
                {
                    // Duration.Text = "Navigation to " + args.Uri.ToString() + " completed successfully.";
                    if (args.Uri.AbsoluteUri.Contains("www.amazon.com"))
                        return;

                    String strClientId = "";
                    if (localSettings.Values["clientId"] != null && localSettings.Values["clientId"] != null)
                        strClientId = Convert.ToString(localSettings.Values["clientId"]);
                    String strClientSecret = "";
                    if (localSettings.Values["clientSecret"] != null && localSettings.Values["clientSecret"] != null)
                        strClientSecret = Convert.ToString(localSettings.Values["clientSecret"]);
                    String strOAuthReidrectURL = "";
                    if (localSettings.Values["oAuthReidrectURL"] != null && localSettings.Values["oAuthReidrectURL"] != null)
                        strOAuthReidrectURL = Convert.ToString(localSettings.Values["oAuthReidrectURL"]);
                    String strCode = args.Uri.Query.Split('&')[0].Replace("?code=", "");
                    AVSInterface.AVSOAuth.RequestAccessToken(strCode, strClientId, strClientSecret, strOAuthReidrectURL);
                    Frame.Navigate(typeof(EchoPage));
                }
                else
                {
                    resultsbox.Text = "Navigation to: " + args.Uri.ToString() +
                                           " failed with error " + args.WebErrorStatus.ToString();
                }
            }
            catch (Exception ex)
            {
                resultsbox.Equals(ex.ToString());
            }
        }

       
    }


  

}
