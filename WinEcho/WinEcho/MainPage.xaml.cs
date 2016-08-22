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




// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace VSMMediaCaptureDemo
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public enum RecordingMode
        {
            Initializing,
            Recording,
            Stopped,
        };

        private MediaCapture _mediaCapture;
        private IRandomAccessStream _audioStream;
        private FileSavePicker _fileSavePicker;
        private DispatcherTimer _timer;
        private TimeSpan _elapsedTime;
        private AudioEncodingFormat _selectedFormat;
        private AudioEncodingQuality _encodingQuality;

        public MainPage()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            await InitMediaCapture();
            LoadAudioEncodings();
            LoadAudioQualities();
            UpdateRecordingControls(RecordingMode.Initializing);
            InitTimer();
        }

        private async Task InitMediaCapture()
        {
            _mediaCapture = new MediaCapture();
            var captureInitSettings = new MediaCaptureInitializationSettings();
            captureInitSettings.StreamingCaptureMode = StreamingCaptureMode.Audio;
            await _mediaCapture.InitializeAsync(captureInitSettings);
            _mediaCapture.Failed += MediaCaptureOnFailed;
            _mediaCapture.RecordLimitationExceeded += MediaCaptureOnRecordLimitationExceeded;
        }

        private void LoadAudioEncodings()
        {
            var audioEncodingFormats = Enum.GetValues(typeof(AudioEncodingFormat)).Cast<AudioEncodingFormat>();
            AudioFormat.ItemsSource = audioEncodingFormats;
            AudioFormat.SelectedItem = AudioEncodingFormat.Mp3;
        }

        private void LoadAudioQualities()
        {
            var audioQualities = Enum.GetValues(typeof(AudioEncodingQuality)).Cast<AudioEncodingQuality>();
            AudioQuality.ItemsSource = audioQualities;
            AudioQuality.SelectedItem = AudioEncodingQuality.Auto;
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
            _timer = new DispatcherTimer();
            _timer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            _timer.Tick += TimerOnTick;
        }

        private void TimerOnTick(object sender, object o)
        {
            _elapsedTime = _elapsedTime.Add(_timer.Interval);
            Duration.DataContext = _elapsedTime;
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

            switch (_selectedFormat)
            {
                case AudioEncodingFormat.Mp3:
                    encodingProfile = MediaEncodingProfile.CreateMp3(_encodingQuality);
                    break;
                case AudioEncodingFormat.Mp4:
                    encodingProfile = MediaEncodingProfile.CreateM4a(_encodingQuality);
                    break;
                case AudioEncodingFormat.Wma:
                    encodingProfile = MediaEncodingProfile.CreateWma(_encodingQuality);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _audioStream = new InMemoryRandomAccessStream();
            await _mediaCapture.StartRecordToStreamAsync(encodingProfile, _audioStream);
            UpdateRecordingControls(RecordingMode.Recording);
            _timer.Start();
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            await _mediaCapture.StopRecordAsync();
            UpdateRecordingControls(RecordingMode.Stopped);
            _timer.Stop();
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var mediaFile = await _fileSavePicker.PickSaveFileAsync();

            if (mediaFile != null)
            {
                using (var dataReader = new DataReader(_audioStream.GetInputStreamAt(0)))
                {
                    await dataReader.LoadAsync((uint)_audioStream.Size);
                    byte[] buffer = new byte[(int)_audioStream.Size];
                    dataReader.ReadBytes(buffer);
                    await FileIO.WriteBytesAsync(mediaFile, buffer);
                    UpdateRecordingControls(RecordingMode.Initializing);
                }
            }
        }

        private void AudioFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedFormat = (AudioEncodingFormat)AudioFormat.SelectedItem;
            InitFileSavePicker();
        }

        private void InitFileSavePicker()
        {
            _fileSavePicker = new FileSavePicker();
            _fileSavePicker.FileTypeChoices.Add("Encoding", new List<string>() { _selectedFormat.ToFileExtension() });
            _fileSavePicker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
        }

        private void AudioQuality_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _encodingQuality = (AudioEncodingQuality)AudioQuality.SelectedItem;
        }

        private async void button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //Create HttpClient
                HttpClient httpClient = new HttpClient();

                //Define Http Headers
                string _ContentType = "application/json";
                httpClient.DefaultRequestHeaders.Accept.Add(new HttpMediaTypeWithQualityHeaderValue(_ContentType));
               // httpClient.DefaultRequestHeaders.Accept.TryParseAdd("application/json");
                var _CredentialBase64 = "Atza|IQEBLjAsAhQsM6FbRtczrnVqZ1jlnZYAedLitwIUREnRA50WDyjp3TtxjWkPqO6eVijxwEoBeB3aRb6zjP22g6BfSJgF9g7iPCtjSpkm86tCytHe3yi4twlI-tFHMBHnvTws_0KW4UrxPIVPpOy6Rq9FTzgPslasg2ruDut-MdXkQ_sIyC1KE4D6o_McobBR4mNyPEPi0vknNPKOCksJVyO0DheBOq7pJo-U84nufswmz9qe-ZAaug7qhhrNh8J5OI6k5UEgPzM8U8eYIWHdn1rdHtewFFpDE5ev0xJ6CYL-pe-hf6YQ7FH-iUXGcCxfGTFeYoJaMjqE8G6NanSNZAW9vW1hUxwCsuBiNItLLLx55nqUn-_L9HZMmxE_AEHlw1BSV4BUGGxNdq-em6lPxAKNZc7HNR-GdrvXfqq_2CpPxCchQs_1mNmzJE4XLgQBV8U5-BKDgKsgEU55Td810U_vAatfNLJKYkXLjOMZPY8-pnfHQFHaG1XMUxKXMRAGI_U4";
                httpClient.DefaultRequestHeaders.Add("Authorization", String.Format("bearer {0}", _CredentialBase64));

                //Call
                string ResponseString = await httpClient.GetStringAsync(
                    new Uri("https://avs-alexa-na.amazon.com/ping"));
                //Replace current URL with your URL
                results.Equals(ResponseString.ToString());
            }

            catch (Exception ex)
            {
                results.Equals(ex.ToString());
            }



        }

    }
}


    

