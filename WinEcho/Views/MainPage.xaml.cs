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
    public sealed partial class MainPage : Page
    {
        public enum RecordingMode
        {
            Initializing,
            Recording,
            Stopped,
        };

        private MediaCapture m_mediaCapture;
        private IRandomAccessStream m_audioStream;
        private FileSavePicker m_fileSavePicker;
        private DispatcherTimer m_timer;
        private TimeSpan m_elapsedTime;
        private DispatcherTimer m_alertTimer;
        private static Boolean m_firstLoad = true;

        Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

        public MainPage()
        {
            this.InitializeComponent();
            if (m_firstLoad)
            {
                InitFileSavePicker();

                CreateDownchannel();
                m_firstLoad = false;
            }
        }

        /// <summary>
        /// GET for establishing the downchannel using the directives path
        /// Refer
        /// </summary>
        public async void CreateDownchannel()
        {
            using (System.Net.Http.HttpClient downClient = new System.Net.Http.HttpClient())
            {
                downClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + AVSInterface.AVSOAuth.GetAccessToken());
                var response = await (downClient.GetAsync("https://avs-alexa-na.amazon.com/v20160207/directives", System.Net.Http.HttpCompletionOption.ResponseHeadersRead));

                var stream = await response.Content.ReadAsStreamAsync();
                var buffer = new byte[2048];

                while (await stream.ReadAsync(buffer, 0, buffer.Length) > 0)
                {
                    // Report progress and write to a different stream
                    string directive = System.Text.Encoding.UTF8.GetString(buffer, 0, buffer.Length);
                    using (System.IO.StringReader reader = new System.IO.StringReader(directive))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (line.Contains("{\"directive\":{\""))
                            {
                                if (line.Contains("SetAlert"))
                                {
                                    AVSInterface.SAResponseContent oContent = JsonConvert.DeserializeObject<AVSInterface.SAResponseContent>(line);
                                    m_alertTimer = new DispatcherTimer();
                                    m_alertTimer.Tick += m_alertTimer_Tick;
                                    m_alertTimer.Interval = oContent.directive.payload.scheduledTime - DateTime.Now;
                                    m_alertTimer.Start();

                                    // Need to send SetAlertSucceeded event.
                                }
                                // Remaining directives - DeleteAlert, ResetUserInactivity, SetVolume, AdjustVolume, SetMute, Play, Stop, ClearQueue,
                            }
                        }
                    }
                    Array.Clear(buffer, 0, buffer.Length);
                }
            }
        }

        async void m_alertTimer_Tick(object sender, object e)
        {
            m_alertTimer.Stop();

            // Need to send AlertStarted event.

            var messageDialog = new MessageDialog("Alert expired.");
            messageDialog.Commands.Add(new UICommand("Ok", new UICommandInvokedHandler(this.OkHandler)));
            await messageDialog.ShowAsync();

            // Need to send AlertStopped event
        }

        private void OkHandler(IUICommand command)
        {
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            await InitMediaCapture();
            UpdateRecordingControls(RecordingMode.Initializing);
            InitTimer();
        }

        private async Task InitMediaCapture()
        {
            m_mediaCapture = new MediaCapture();
            var captureInitSettings = new MediaCaptureInitializationSettings();
            captureInitSettings.StreamingCaptureMode = StreamingCaptureMode.Audio;
            await m_mediaCapture.InitializeAsync(captureInitSettings);
            m_mediaCapture.Failed += MediaCaptureOnFailed;
            m_mediaCapture.RecordLimitationExceeded += MediaCaptureOnRecordLimitationExceeded;
        }

        private void UpdateRecordingControls(RecordingMode recordingMode)
        {
            switch (recordingMode)
            {
                case RecordingMode.Initializing:
                    RecordButton.IsEnabled = true;
                    StopButton.IsEnabled = false;
                    SaveButton.IsEnabled = false;
                    break;
                case RecordingMode.Recording:
                    RecordButton.IsEnabled = false;
                    StopButton.IsEnabled = true;
                    SaveButton.IsEnabled = false;
                    break;
                case RecordingMode.Stopped:
                    RecordButton.IsEnabled = true;
                    StopButton.IsEnabled = false;
                    SaveButton.IsEnabled = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("recordingMode");
            }
        }

        private void InitTimer()
        {
            m_timer = new DispatcherTimer();
            m_timer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            m_timer.Tick += TimerOnTick;
        }

        private void TimerOnTick(object sender, object o)
        {
            m_elapsedTime = m_elapsedTime.Add(m_timer.Interval);
            Duration.DataContext = m_elapsedTime;
        }

        private async void MediaCaptureOnRecordLimitationExceeded(MediaCapture sender)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                await sender.StopRecordAsync();
                var warningMessage = new MessageDialog("The recording has stopped because you exceeded the maximum recording length.", "Recording Stoppped");
                await warningMessage.ShowAsync();
            });
        }

        private async void MediaCaptureOnFailed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                var warningMessage = new MessageDialog(String.Format("The audio capture failed: {0}", errorEventArgs.Message), "Capture Failed");
                await warningMessage.ShowAsync();
            });
        }

        private void InitFileSavePicker()
        {
            m_fileSavePicker = new FileSavePicker();
            m_fileSavePicker.FileTypeChoices.Add("Encoding", new List<string>() { AudioEncodingFormat.Wma.ToFileExtension() });
            m_fileSavePicker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
        }

        private async void button_Click(object sender, RoutedEventArgs e)
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
                    CallVoiceService();
                }
            }
            catch (Exception ex)
            {
                results.Equals(ex.ToString());
            }
        }

        private async void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            MediaEncodingProfile encodingProfile = null;

            encodingProfile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.Low);

            m_audioStream = new InMemoryRandomAccessStream();
            await m_mediaCapture.StartRecordToStreamAsync(encodingProfile, m_audioStream);
            UpdateRecordingControls(RecordingMode.Recording);
            m_timer.Start();
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            await m_mediaCapture.StopRecordAsync();
            UpdateRecordingControls(RecordingMode.Stopped);
            m_timer.Stop();
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var mediaFile = await m_fileSavePicker.PickSaveFileAsync();

            if (mediaFile != null)
            {
                using (var dataReader = new DataReader(m_audioStream.GetInputStreamAt(0)))
                {
                    await dataReader.LoadAsync((uint)m_audioStream.Size);
                    byte[] buffer = new byte[(int)m_audioStream.Size];
                    dataReader.ReadBytes(buffer);
                    await FileIO.WriteBytesAsync(mediaFile, buffer);
                    UpdateRecordingControls(RecordingMode.Initializing);
                }
            }
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

                    String strCode = args.Uri.Query.Split('&')[0].Replace("?code=", "");
                    String strClientId = "";
                    if (localSettings.Values["clientId"] != null && localSettings.Values["clientId"] != null)
                        strClientId = Convert.ToString(localSettings.Values["clientId"]);
                    String strClientSecret = "";
                    if (localSettings.Values["clientSecret"] != null && localSettings.Values["clientSecret"] != null)
                        strClientSecret = Convert.ToString(localSettings.Values["clientSecret"]);
                    String strOAuthReidrectURL = "";
                    if (localSettings.Values["oAuthReidrectURL"] != null && localSettings.Values["oAuthReidrectURL"] != null)
                        strOAuthReidrectURL = Convert.ToString(localSettings.Values["oAuthReidrectURL"]);
                    AVSInterface.AVSOAuth.RequestAccessToken(strCode, strClientId, strClientSecret, strOAuthReidrectURL);

                }
                else
                {
                    Duration.Text = "Navigation to: " + args.Uri.ToString() +
                                           " failed with error " + args.WebErrorStatus.ToString();
                }
            }
            catch (Exception ex)
            {
                results.Equals(ex.ToString());
            }
        }

        private async void CallVoiceService()
        {
            try
            {
                System.Net.Http.HttpResponseMessage respMessage = new System.Net.Http.HttpResponseMessage();
                respMessage = await new AVSInterface.AVSMessages().SpeechRecognizer((InMemoryRandomAccessStream)m_audioStream, AVSInterface.AVSOAuth.GetAccessToken());

                // Read the response
                var responseStream = await respMessage.Content.ReadAsStreamAsync();

                // Check the response header to find the mulit-part boundary string
                String strBoundary = respMessage.Content.Headers.ContentType.Parameters.FirstOrDefault().Value;
                string[] stringSeparators = new string[] { "--" + strBoundary };

                // Process the response
                responseStream.Seek(0, System.IO.SeekOrigin.Begin);
                var reader = new System.IO.StreamReader(responseStream, System.Text.Encoding.UTF8);
                string responseString2 = reader.ReadToEnd();
                String[] arrContent = responseString2.Split(stringSeparators, System.StringSplitOptions.RemoveEmptyEntries);

                // Pull the directive out of the content.
                var responseDirective = JsonConvert.DeserializeObject<AVSInterface.SRResponseContent>(arrContent[0].Split(new[] { Environment.NewLine }, StringSplitOptions.None)[3]);

                textBox.Text = arrContent[0];

                if (responseDirective.directive.header.name == "Speak")
                {
                    try
                    {
                        System.Net.Http.HttpResponseMessage speechStartResponse = await new AVSInterface.AVSMessages().SpeechStarted(AVSInterface.AVSOAuth.GetAccessToken(), responseDirective.directive.payload.token);
                        var memStream2 = new System.IO.MemoryStream();
                        responseStream.Seek(0, System.IO.SeekOrigin.Begin);
                        await responseStream.CopyToAsync(memStream2);
                        memStream2.Position = 0;

                        Windows.Storage.StorageFile file = await Windows.Storage.KnownFolders.MusicLibrary.CreateFileAsync("response.wav", Windows.Storage.CreationCollisionOption.ReplaceExisting);
                        await Windows.Storage.FileIO.WriteBytesAsync(file, memStream2.ToArray());
                        await AVSInterface.Audio.PlayAudio(file);
                        System.Net.Http.HttpResponseMessage speechFinishedResponse = await new AVSInterface.AVSMessages().SpeechFinished(AVSInterface.AVSOAuth.GetAccessToken(), responseDirective.directive.payload.token);
                        //player.Source = new Uri("http://vpr.streamguys.net/vpr96.mp3");
                        //player.Play();

                        var spStartResponseStream = await speechStartResponse.Content.ReadAsStreamAsync();
                        spStartResponseStream.Seek(0, System.IO.SeekOrigin.Begin);
                        var spStartReader = new System.IO.StreamReader(spStartResponseStream, System.Text.Encoding.UTF8);
                        string spStartResponseString = spStartReader.ReadToEnd();
                        textBox.Text += System.Environment.NewLine + spStartResponseString;

                        var spFinResponseStream = await speechStartResponse.Content.ReadAsStreamAsync();
                        spFinResponseStream.Seek(0, System.IO.SeekOrigin.Begin);
                        var spFinReader = new System.IO.StreamReader(spFinResponseStream, System.Text.Encoding.UTF8);
                        string spFinResponseString = spFinReader.ReadToEnd();
                        textBox.Text += System.Environment.NewLine + spFinResponseStream;
                    }
                    catch (Exception e)
                    {
                        results.Equals(e.ToString());
                    }
                }
                else
                {
                    textBox.Text = "Unknown directive: " + responseDirective.directive.header.name;
                }
            }
            catch (Exception ex)
            {
                results.Equals(ex.ToString());
                textBox.Text = ex.ToString();
            }
        }

        private void textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
        }

        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var mediaFile = await m_fileSavePicker.PickSaveFileAsync();

            if (mediaFile != null)
            {
                m_audioStream = new InMemoryRandomAccessStream();
                using (var inputStream = await mediaFile.OpenReadAsync())
                {
                    await RandomAccessStream.CopyAsync(inputStream, m_audioStream);
                }
            }

        }

        private void login_click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(AmazonLogin));
        }

        private void echo_click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(EchoPage));
        }
    }
}


    

