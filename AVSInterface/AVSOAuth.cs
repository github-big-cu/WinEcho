using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace AVSInterface
{
    public class OAuthAttributes
    {
        public String deviceSerialNumber;
    }

    public class OAuthContents
    {
        public String productID;
        public OAuthAttributes productInstanceAttributes;
    }

    public class OAuthScopeData
    {
        [JsonProperty(PropertyName = "alexa:all")]
        public OAuthContents alexaall;
    }

    public class AVSOAuth
    {
        public static Boolean CheckOAuth(String _strClientId, String _strClientSecret)
        {
            try
            {
                Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

                // Check to see if we have a non-expired access token
                if (localSettings.Values["accessToken"] != null && localSettings.Values["accessTokenExpires"] != null)
                {
                    if (DateTime.Now < Convert.ToDateTime(localSettings.Values["accessTokenExpires"]))
                        return true;
                }

                // Check to see if there is a refresh token
                if (localSettings.Values["refreshToken"] != null)
                {
                    // If so, try and get a new access token
                    if (RequestAccessTokenRefresh(_strClientId, _strClientSecret))
                        return true;
                }
            }
            catch (Exception)
            {
            }
            return false;
        }

        public static Boolean ResetOAuth(String _strType)
        {
            try
            {
                Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                if (_strType == "AccessToken")
                {
                    localSettings.Values["accessToken"] = null;
                }
                else
                {
                    localSettings.Values["accessToken"] = null;
                    localSettings.Values["accessTokenExpires"] = null;
                    localSettings.Values["refreshToken"] = null;
                }
            }
            catch (Exception)
            {
            }
            return true;
        }

        public static String GetAccessToken()
        {
            try
            {
                Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

                // Check to see if we have a non-expired access token
                if (localSettings.Values["accessToken"] != null && localSettings.Values["accessTokenExpires"] != null)
                {
                    if (DateTime.Now < Convert.ToDateTime(localSettings.Values["accessTokenExpires"]))
                        return Convert.ToString(localSettings.Values["accessToken"]);
                }
            }
            catch (Exception)
            {
            }
            return "";
        }

        public static Boolean RequestAccessToken(String _strAuthCode, String _strClientId, String _strClientSecret, String _strOAuthReidrectURL)
        {
            Boolean bRet = false;
            try
            {
                // Make the call to get an access token.

                //Create HttpClient
                System.Net.Http.HttpClient httpClient = new System.Net.Http.HttpClient();

                // grant_type=authorization_code&code=ANBzsjhYZmNCTeAszagk&client_id=1234&client_secret=1234&redirect_uri=https%3A%2F%2Flocalhost
                var values = new Dictionary<string, string>
                {
                    { "grant_type", "authorization_code" },
                    { "code", _strAuthCode },
                    { "client_id", _strClientId },
                    { "client_secret", _strClientSecret },
                    { "redirect_uri", _strOAuthReidrectURL }
                };

                var content = new System.Net.Http.FormUrlEncodedContent(values);

                var response = httpClient.PostAsync(" https://api.amazon.com/auth/o2/token", content).Result;

                var responseString = response.Content.ReadAsStringAsync().Result;
                var arrResp = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<String, String>>(responseString);
                String strAccessToken = arrResp["access_token"];
                String strRefreshToken = arrResp["refresh_token"];
                Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                localSettings.Values["accessToken"] = strAccessToken;
                localSettings.Values["accessTokenExpires"] = DateTime.Now.AddHours(1).ToString("MM/dd/yy HH:mm:ss");
                localSettings.Values["refreshToken"] = strRefreshToken;
                return true;
            }
            catch (Exception)
            {
                //results.Equals(ex.ToString());
                //textBox.Text = ex.ToString();
            }
            return bRet;
        }

        public static Boolean RequestAccessTokenRefresh(String _strClientId, String _strClientSecret)
        {
            Boolean bRet = false;
            try
            {
                // Make the call to get an access token using a refresh token

                //Create HttpClient
                System.Net.Http.HttpClient httpClient = new System.Net.Http.HttpClient();

                Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                if ((localSettings.Values["refreshToken"] == null))
                    return false;
                String strRefreshToken = Convert.ToString(localSettings.Values["refreshToken"]);
                var values = new Dictionary<string, string>
                {
                    { "grant_type", "refresh_token" },
                    { "refresh_token", strRefreshToken },
                    { "client_id", _strClientId },
                    { "client_secret", _strClientSecret }
                };

                var content = new System.Net.Http.FormUrlEncodedContent(values);

                var response = httpClient.PostAsync(" https://api.amazon.com/auth/o2/token", content).Result;

                var responseString = response.Content.ReadAsStringAsync().Result;
                var arrResp = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<String, String>>(responseString);
                String strAccessToken = arrResp["access_token"];
                strRefreshToken = arrResp["refresh_token"];
                localSettings.Values["accessToken"] = strAccessToken;
                localSettings.Values["accessTokenExpires"] = DateTime.Now.AddHours(1).ToString("MM/dd/yy HH:mm:ss");
                localSettings.Values["refreshToken"] = strRefreshToken;
                bRet = true;
            }
            catch (Exception)
            {
                //results.Equals(ex.ToString());
                //textBox.Text = ex.ToString();
            }
            return bRet;
        }

        public static void StartOAuth(String _strClientId, String _strProductId, String _strDeviceSerial, String _strRedirectURL, Windows.UI.Xaml.Controls.WebView _wvAuth)
        {
            try
            {
                // Show the Amazon login page to allow the user to login.
                String strAuthURL = "https://www.amazon.com/ap/oa?";
                strAuthURL += "client_id=" + _strClientId;
                String strScope = System.Net.WebUtility.UrlEncode("alexa:all");
                strAuthURL += "&scope=" + strScope;

                strAuthURL += "&scope_data=";
                OAuthScopeData oScope = new OAuthScopeData();
                oScope.alexaall = new OAuthContents();
                oScope.alexaall.productID = _strProductId;
                oScope.alexaall.productInstanceAttributes = new OAuthAttributes();
                oScope.alexaall.productInstanceAttributes.deviceSerialNumber = _strDeviceSerial;
                String hold = JsonConvert.SerializeObject(oScope);
                hold = System.Net.WebUtility.UrlEncode(hold);
                strAuthURL += hold;

                strAuthURL += "&response_type=code";
                strAuthURL += "&redirect_uri=" + System.Net.WebUtility.UrlEncode(_strRedirectURL).Replace(".", "%2E");

                System.Uri uriAuth = new Uri(strAuthURL);
                _wvAuth.Source = uriAuth;
            }
            catch (Exception)
            {
            }
        }
    }
}
