using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net.Http;
using Windows.Security;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Media.Audio;

namespace AVSInterface
{

    public class SRContentHeader
    {
        [JsonProperty(PropertyName = "namespace")]
        public String nmespace;
        public String name;
        public String messageId;
        public String dialogRequestId;
    }

    public class SRContentPayload
    {
        public String profile;
        public String format;
    }

    public class SRContentEvent
    {
        public SRContentHeader header;
        public SRContentPayload payload;
    }

    public class SRMetaContent
    {
        [JsonProperty(PropertyName = "event")]
        public SRContentEvent evnt;
    }

    public class SRResponseHeader
    {
        [JsonProperty(PropertyName = "namespace")]
        public String nmespace;
        public String name;
        public String messageId;
        public String dialogRequestId;
    }

    public class SRResponsePayload
    {
        public String url;
        public String format;
        public String token;
    }

    public class SRResponseDirective
    {
        public SRResponseHeader header;
        public SRResponsePayload payload;
    }

    public class SRResponseContent
    {
        public SRResponseDirective directive;
    }

    public class SSContentHeader
    {
        [JsonProperty(PropertyName = "namespace")]
        public String nmespace;
        public String name;
        public String messageId;
    }

    public class SSContentPayload
    {
        public String token;
    }

    public class SSContentEvent
    {
        public SSContentHeader header;
        public SSContentPayload payload;
    }

    public class SSMetaContent
    {
        [JsonProperty(PropertyName = "event")]
        public SSContentEvent evnt;
    }

    public class SAResponseHeader
    {
        [JsonProperty(PropertyName = "namespace")]
        public String nmespace;
        public String name;
        public String messageId;
        public String dialogRequestId;
    }

    public class SAResponsePayload
    {
        public String token;
        public String type;
        public DateTime scheduledTime;
    }

    public class SAResponseDirective
    {
        public SAResponseHeader header;
        public SAResponsePayload payload;
    }

    public class SAResponseContent
    {
        public SAResponseDirective directive;
    }


    public class AVSMessages
    {
        public async Task<HttpResponseMessage> SpeechRecognizer(Windows.Storage.Streams.InMemoryRandomAccessStream _audioStream, String _strAccessToken)
        {
            HttpResponseMessage respMessage = new HttpResponseMessage();

            try
            {
                // Call the voice service
                //Define Http Headers
                System.Net.Http.HttpClient httpClient2 = new System.Net.Http.HttpClient();
                httpClient2.DefaultRequestHeaders.Add("authorization", String.Format("bearer {0}", _strAccessToken));
                httpClient2.DefaultRequestHeaders.TryAddWithoutValidation("content-type", String.Format("multipart/form-data; boundary=--boundry--"));

                // Define the content
                var oContent = new MultipartFormDataContent("--boundry--");

                // Content - metadata
                // {
                //      "context": [
                //          {{...}
                //      }
                //    ],
                //    "event": {
                //        "header": {
                //            "namespace": "SpeechRecognizer",
                //            "name": "Recognize",
                //            "messageId": "1234",
                //            "dialogRequestId": "5678"
                //        },
                //        "payload": {
                //            "profile": "CLOSE_TALK",
                //            "format": "AUDIO_L16_RATE_16000_CHANNELS_1"
                //        }
                //    }
                //}
                SRMetaContent hold = new SRMetaContent();
                hold.evnt = new SRContentEvent();
                hold.evnt.header = new SRContentHeader();
                hold.evnt.header.nmespace = "SpeechRecognizer";
                hold.evnt.header.name = "Recognize";
                hold.evnt.header.messageId = "1234";
                hold.evnt.header.dialogRequestId = "5678";
                hold.evnt.payload = new SRContentPayload();
                hold.evnt.payload.profile = "CLOSE_TALK";
                hold.evnt.payload.format = "AUDIO_L16_RATE_16000_CHANNELS_1";
                String strHold = JsonConvert.SerializeObject(hold);

                var metaContent = new System.Net.Http.StringContent(strHold);
                metaContent.Headers.Add("Content-Disposition", "form-data; name=\"metadata\"");
                metaContent.Headers.TryAddWithoutValidation("Content-Type", "application/json; charset=UTF-8");

                //Content - audio
                byte[] soundBytes = await GetAudioBytes(_audioStream);
                var audioConent = new StreamContent(new System.IO.MemoryStream(soundBytes));
                audioConent.Headers.Add("Content-Disposition", "form-data; name=\"audio\"");
                audioConent.Headers.TryAddWithoutValidation("Content-Type", "application/octet-stream");

                // Add the content to the request
                oContent.Add(metaContent);
                oContent.Add(audioConent);

                // Send the request to Amazon
                respMessage = httpClient2.PostAsync("https://avs-alexa-na.amazon.com/v20160207/events", oContent).Result;
            }
            catch (Exception)
            {
                //results.Equals(ex.ToString());
                //textBox.Text = ex.ToString();
            }
            return respMessage;
        }

        private async Task<byte[]> GetAudioBytes(Windows.Storage.Streams.InMemoryRandomAccessStream _audioStream)
        {
            using (var dataReader = new Windows.Storage.Streams.DataReader(_audioStream.GetInputStreamAt(0)))
            {
                await dataReader.LoadAsync((uint)_audioStream.Size);
                byte[] buffer = new byte[(int)_audioStream.Size];
                dataReader.ReadBytes(buffer);
                return buffer;
            }
        }

        public async Task<HttpResponseMessage> SpeechStarted(String _strAccessToken, String _strSpeechToken)
        {
            HttpResponseMessage respMessage = new HttpResponseMessage();

            try
            {
                // Call the voice service
                //Define Http Headers
                System.Net.Http.HttpClient httpClient2 = new System.Net.Http.HttpClient();
                httpClient2.DefaultRequestHeaders.Add("authorization", String.Format("bearer {0}", _strAccessToken));
                httpClient2.DefaultRequestHeaders.TryAddWithoutValidation("content-type", String.Format("multipart/form-data; boundary=--boundry--"));

                // Define the content
                var oContent = new MultipartFormDataContent("--boundry--");

                // Content - metadata
                // {
                //      "context": [
                //          {{...}
                //      }
                //    ],
                //        {
                //            "event": {
                //                "header": {
                //                    "namespace": "SpeechSynthesizer",
                //                      "name": "SpeechStarted",
                //                      "messageId": "{{STRING}}"
                //                },
                //                  "payload": {
                //                    "token": "{{STRING}}"
                //                  }
                //            }
                //        }
                //    }
                //}
                SSMetaContent hold = new SSMetaContent();
                hold.evnt = new SSContentEvent();
                hold.evnt.header = new SSContentHeader();
                hold.evnt.header.nmespace = "SpeechSynthesizer";
                hold.evnt.header.name = "SpeechStarted";
                hold.evnt.header.messageId = "1234";
                //hold.evnt.header.dialogRequestId = "5678";
                hold.evnt.payload = new SSContentPayload();
                hold.evnt.payload.token = _strSpeechToken;
                String strHold = JsonConvert.SerializeObject(hold);

                var metaContent = new System.Net.Http.StringContent(strHold);
                metaContent.Headers.Add("Content-Disposition", "form-data; name=\"metadata\"");
                metaContent.Headers.TryAddWithoutValidation("Content-Type", "application/json; charset=UTF-8");

                // Add the content to the request
                oContent.Add(metaContent);

                // Send the request to Amazon
                respMessage = httpClient2.PostAsync(" https://avs-alexa-na.amazon.com/v20160207/events", oContent).Result;
            }
            catch (Exception)
            {
                //results.Equals(ex.ToString());
                //textBox.Text = ex.ToString();
            }
            return respMessage;
        }

        public async Task<HttpResponseMessage> SpeechFinished(String _strAccessToken, String _strSpeechToken)
        {
            HttpResponseMessage respMessage = new HttpResponseMessage();

            try
            {
                // Call the voice service
                //Define Http Headers
                System.Net.Http.HttpClient httpClient2 = new System.Net.Http.HttpClient();
                httpClient2.DefaultRequestHeaders.Add("authorization", String.Format("bearer {0}", _strAccessToken));
                httpClient2.DefaultRequestHeaders.TryAddWithoutValidation("content-type", String.Format("multipart/form-data; boundary=--boundry--"));

                // Define the content
                var oContent = new MultipartFormDataContent("--boundry--");

                // Content - metadata
                // {
                //      "context": [
                //          {{...}
                //      }
                //    ],
                //        {
                //            "event": {
                //                "header": {
                //                    "namespace": "SpeechSynthesizer",
                //                      "name": "SpeechStarted",
                //                      "messageId": "{{STRING}}"
                //                },
                //                  "payload": {
                //                    "token": "{{STRING}}"
                //                  }
                //            }
                //        }
                //    }
                //}
                SSMetaContent hold = new SSMetaContent();
                hold.evnt = new SSContentEvent();
                hold.evnt.header = new SSContentHeader();
                hold.evnt.header.nmespace = "SpeechSynthesizer";
                hold.evnt.header.name = "SpeechFinished";
                hold.evnt.header.messageId = "1234";
                //hold.evnt.header.dialogRequestId = "5678";
                hold.evnt.payload = new SSContentPayload();
                hold.evnt.payload.token = _strSpeechToken;
                String strHold = JsonConvert.SerializeObject(hold);

                var metaContent = new System.Net.Http.StringContent(strHold);
                metaContent.Headers.Add("Content-Disposition", "form-data; name=\"metadata\"");
                metaContent.Headers.TryAddWithoutValidation("Content-Type", "application/json; charset=UTF-8");

                // Add the content to the request
                oContent.Add(metaContent);

                // Send the request to Amazon
                respMessage = httpClient2.PostAsync(" https://avs-alexa-na.amazon.com/v20160207/events", oContent).Result;
            }
            catch (Exception)
            {
                //results.Equals(ex.ToString());
                //textBox.Text = ex.ToString();
            }
            return respMessage;
        }

    }
}
