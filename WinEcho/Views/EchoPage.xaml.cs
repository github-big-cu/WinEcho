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
    public sealed partial class EchoPage : Page
    {
        Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

        public enum RecordingMode
        {
            Initializing,
            Recording,
            Stopped,
        };

        private MediaCapture m_mediaCapture;
        private IRandomAccessStream m_audioStream;
        private DispatcherTimer m_timer;
        private TimeSpan m_elapsedTime;
        private static Boolean m_leftDown = false;

        public EchoPage()
        {
            this.InitializeComponent();
            //Tap_button.AddHandler(PointerPressedEvent,
            //new UIElement.PointerEventHandler(Tap_button_PointerPressed), true);
            image.PointerPressed += Tap_button_PointerPressed;
            image.PointerReleased += Tap_button_PointerReleased;
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
                    StopButton.IsEnabled = false;
                    break;
                case RecordingMode.Recording:
                    StopButton.IsEnabled = true;
                    break;
                case RecordingMode.Stopped:
                    StopButton.IsEnabled = false;
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

        private async void sendVoice(object sender, RoutedEventArgs e)
        {
            MediaEncodingProfile encodingProfile = null;

            encodingProfile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.Low);

            m_audioStream = new InMemoryRandomAccessStream();
            await m_mediaCapture.StartRecordToStreamAsync(encodingProfile, m_audioStream);
            UpdateRecordingControls(RecordingMode.Recording);
            m_timer.Start();
        }

        private async void CallVoiceService()
        {
            try
            {
                // Check to see if the Amazon login is working.
                String strClientId = "";
                if (localSettings.Values["clientId"] != null && localSettings.Values["clientId"] != null)
                    strClientId = Convert.ToString(localSettings.Values["clientId"]);
                String strClientSecret = "";
                if (localSettings.Values["clientSecret"] != null && localSettings.Values["clientSecret"] != null)
                    strClientSecret = Convert.ToString(localSettings.Values["clientSecret"]);
                if (!AVSInterface.AVSOAuth.CheckOAuth(strClientId, strClientSecret))
                {
                    var messageDialog = new MessageDialog("Amazon login not setup.");
                    messageDialog.Commands.Add(new UICommand("Ok", new UICommandInvokedHandler(this.OkHandler)));
                    await messageDialog.ShowAsync();
                    return;
                }

                // Call the speech recognizer with the audio stream
                results.Text = "Sending request";
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

                if (responseDirective.directive.header.name == "Speak")
                {
                    results.Text = responseDirective.directive.header.name;
                    try
                    {
                        // Send the SpeechStarted event
                        System.Net.Http.HttpResponseMessage speechStartResponse = await new AVSInterface.AVSMessages().SpeechStarted(AVSInterface.AVSOAuth.GetAccessToken(), responseDirective.directive.payload.token);
                        var spStartResponseStream = await speechStartResponse.Content.ReadAsStreamAsync();
                        spStartResponseStream.Seek(0, System.IO.SeekOrigin.Begin);
                        var spStartReader = new System.IO.StreamReader(spStartResponseStream, System.Text.Encoding.UTF8);
                        string spStartResponseString = spStartReader.ReadToEnd();

                        // Get the contents of the SpeechRecongnizer response
                        var memStream2 = new System.IO.MemoryStream();
                        responseStream.Seek(0, System.IO.SeekOrigin.Begin);
                        await responseStream.CopyToAsync(memStream2);
                        memStream2.Position = 0;

                        // Save the contents to a file and play the file.
                        Windows.Storage.StorageFile file = await Windows.Storage.KnownFolders.MusicLibrary.CreateFileAsync("response.wav", Windows.Storage.CreationCollisionOption.ReplaceExisting);
                        await Windows.Storage.FileIO.WriteBytesAsync(file, memStream2.ToArray());
                        await AVSInterface.Audio.PlayAudio(file);

                        // Send the SpeechFinished event.
                        System.Net.Http.HttpResponseMessage speechFinishedResponse = await new AVSInterface.AVSMessages().SpeechFinished(AVSInterface.AVSOAuth.GetAccessToken(), responseDirective.directive.payload.token);
                        var spFinResponseStream = await speechFinishedResponse.Content.ReadAsStreamAsync();
                        spFinResponseStream.Seek(0, System.IO.SeekOrigin.Begin);
                        var spFinReader = new System.IO.StreamReader(spFinResponseStream, System.Text.Encoding.UTF8);
                        string spFinResponseString = spFinReader.ReadToEnd();
                    }
                    catch (Exception e)
                    {
                        results.Text.Equals(e.ToString());
                    }
                }
                else
                {
                    results.Text.Equals("Unknown directive: " + responseDirective.directive.header.name);
                }
            }
            catch (Exception ex)
            {
                results.Text.Equals(ex.ToString());
            }
        }

        private void OkHandler(IUICommand command)
        {
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
            CallVoiceService();
        }

        private async void Tap_button_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            try
            {
                MediaEncodingProfile encodingProfile = null;
                encodingProfile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.Low);
                m_audioStream = new InMemoryRandomAccessStream();
                await m_mediaCapture.StartRecordToStreamAsync(encodingProfile, m_audioStream);
                UpdateRecordingControls(RecordingMode.Recording);
                m_timer.Start();
            }
            catch (Exception ex)
            {
                results.Text.Equals(ex.ToString());
            }
        }

        private async void Tap_button_PointerReleased(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            try
            {
                await m_mediaCapture.StopRecordAsync();
                UpdateRecordingControls(RecordingMode.Stopped);
                m_timer.Stop();
                CallVoiceService();
            }
            catch (Exception ex)
            {
                results.Text.Equals(ex.ToString());
            }
        }

    }
}

