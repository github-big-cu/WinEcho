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

namespace VSMMediaCaptureDemo
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

        private String m_strClientId = "amzn1.application-oa2-client.17b0427f476d4d7486215d681f5430c6";
        private String m_strClientSecret = "5d31829c475cca2e43fdef911ebda831fadb7b7cd4ec37a24834f8a007b1e4db";
        private String m_strProductId = "WinEcho";
        private String m_strSerial = "12345";
        private String m_strOAuthReidrectURL = "http://localhost/signin-amazon.htm";

        public MainPage()
        {
            this.InitializeComponent();
            InitFileSavePicker();

            CreateDownchannel();
        }

        /// <summary>
        /// GET for establishing the downchannel using the directives path
        /// Refer
        /// </summary>
        public async void CreateDownchannel()
        {
            using (System.Net.Http.HttpClient get = new System.Net.Http.HttpClient())
            {
                get.DefaultRequestHeaders.Add("Authorization", "Bearer " + Ini.access_token);
                var response = await (get.GetAsync(directivesURL, System.Net.Http.HttpCompletionOption.ResponseHeadersRead));

                var stream = await response.Content.ReadAsStreamAsync();
                var buffer = new byte[2048];

                while (await stream.ReadAsync(buffer, 0, buffer.Length) > 0)
                {
                    // Report progress and write to a different stream
                    string directive = System.Text.Encoding.UTF8.GetString(buffer, 0, buffer.Length);
                    using (StringReader reader = new StringReader(directive))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (line.Contains("{\"directive\":{\""))
                            {
                                Directives.ParseDirective(line);
                            }
                        }
                    }
                    Array.Clear(buffer, 0, buffer.Length);
                }
            }
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
                Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                if (!AVSInterface.AVSOAuth.CheckOAuth(m_strClientId, m_strClientSecret))
                {
                    AVSInterface.AVSOAuth.StartOAuth(m_strClientId, m_strProductId, m_strSerial, m_strOAuthReidrectURL, wvAuth);
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
                    AVSInterface.AVSOAuth.RequestAccessToken(strCode, m_strClientId, m_strClientSecret, m_strOAuthReidrectURL);

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

        private void button1_Click(object sender, RoutedEventArgs e)
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

        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var mediaFile = await m_fileSavePicker.PickSaveFileAsync();

            if (mediaFile != null)
            {
                //byte[] buffer = await FileIO.ReadBufferAsync(mediaFile);
                //m_audioStream = new InMemoryRandomAccessStream();
                //await m_audioStream.ReadAsync(buffer, buffer.Length, InputStreamOptions.None);
                //using (var dataReader = new DataReader(m_audioStream.GetInputStreamAt(0)))
                //{
                //    await dataReader.LoadAsync((uint)m_audioStream.Size);
                //    byte[] buffer = new byte[(int)m_audioStream.Size];
                //    dataReader.ReadBytes(buffer);
                //    await FileIO.WriteBytesAsync(mediaFile, buffer);
                //    UpdateRecordingControls(RecordingMode.Initializing);
                //}

                //using (m_audioStream = new InMemoryRandomAccessStream())
                //using (System.IO.FileStream file = new System.IO.FileStream(mediaFile.Name, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                //{
                //    byte[] bytes = new byte[file.Length];
                //    file.Read(bytes, 0, (int)file.Length);
                //    System.IO.MemoryStream ms = new System.IO.MemoryStream();
                //    ms.Write(bytes, 0, (int)file.Length);
                //    m_audioStream = RandomAccessStream(ms);

                //}

                m_audioStream = new InMemoryRandomAccessStream();
                using (var inputStream = await mediaFile.OpenReadAsync())
                {
                    await RandomAccessStream.CopyAsync(inputStream, m_audioStream);
                }
            }

        }
    }
}


    

