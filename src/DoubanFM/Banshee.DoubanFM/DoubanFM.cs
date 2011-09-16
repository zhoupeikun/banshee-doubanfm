// 
// DoubanFM.cs
//
// Copyright (C) 2011 Chen Tao <pro711@gmail.com>
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Net.Security;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Web;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Runtime.Remoting.Messaging;
using Mono.Addins;
using Banshee.Base;
using Banshee.Collection;
using Banshee.Sources;
using Banshee.Sources.Gui;
using Banshee.Configuration;
using Banshee.Gui;
using Banshee.ServiceStack;
using Banshee.MediaEngine;
using Gtk;
using Gdk;
using Hyena;
using Hyena.Json;

namespace Banshee.DoubanFM
{

    public class DoubanInvalidDataException : Exception
    {
        internal protected string message;

        public override string Message {
            get {
                return message;
            }
        }

        public DoubanInvalidDataException (string m) {
            message = m;
        }
    }

    public class DoubanLoginException : Exception
    {
    }

    public class CaptchaException : Exception
    {
		public string CaptchaId {
			get;
			private set;
		}
		
		public string CaptchaUri {
			get {
				return "https://www.douban.com/misc/captcha?id=" + CaptchaId + "&size=s";
			}
		}
			
		public CaptchaException(string captchaId) : base("Captcha required")
		{
			CaptchaId = captchaId;
		}
    }	
	
    /// <summary>
    /// Douban FM service.
    /// </summary>
    public class DoubanFM : IDoubanFMPlayQueue, IDisposable
    {
        // User ID
        public string uid {
            get; set;
        }

        public string bid {
            get; set;
        }

        public string dbcl2 {
            get; set;
        }

        public int channel {
            get { return _channel; }
            set { _channel = value; }
        }

        public Dictionary<string, DoubanFMChannel> Channels {
            get;
            private set;
        }
		
		public bool Initialized {
			get;
			private set;
		}

        private string username;
        private string password;
        private int _channel;
        private CookieContainer cookieJar;
        private DoubanFMSourceContents contents;

        private List<DoubanFMSong> history;

        delegate void InitializeHandler ();

        public DoubanFM (string username, string password, DoubanFMSourceContents contents) {
            this.uid = null;
            this.bid = null;
            this.dbcl2 = null;
            this.Channels = new Dictionary<string, DoubanFMChannel>();
            this._channel = 0;
            this.cookieJar = new CookieContainer();
            this.username = username;
            this.password = password;
            this.contents = contents;
            playList = new List<DoubanFMSong>();
            history = new List<DoubanFMSong>();

			Initialized = false;
            InitializeHandler asyncInitializeHandler = Initialize;
            asyncInitializeHandler.BeginInvoke(InitializeCallback, null);
        }

        public void Initialize() {
//            Thread loadChannelsThread = new Thread(new ThreadStart(LoadChannels));
//            loadChannelsThread.Start();

            Login(username, password);
			LoadChannels();

//            loadChannelsThread.Join();
        }

        public void ConnectPlaybackFinished() {
            DoubanFMSong.PlaybackFinished += HandleDoubanFMSongPlaybackFinished;
//			DoubanFMSong.PlaybackFinishedEvent += HandleDoubanFMSongPlaybackFinished;
        }

        public void DisconnectPlaybackFinished() {
            DoubanFMSong.PlaybackFinished -= HandleDoubanFMSongPlaybackFinished;
//			DoubanFMSong.PlaybackFinishedEvent -= HandleDoubanFMSongPlaybackFinished;
        }

        /// <summary>
        /// workaround for invalid certificate problem
        /// </summary>
        public static bool Validator (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
            return true;
        }

		public delegate void LoginErrorHandler();
		public event LoginErrorHandler LoginErrorEvent;
		
        /// <summary>
        /// login douban, get session token
        /// </summary>
        protected void Login (string username, string password) {
			try {
				AttemptLogin(username, password, null, null);
			}
			catch (CaptchaException e) {
				Hyena.Log.Information("Caught captcha exception");
				string captchaText = "";
				byte[] captchaImage = GetCaptchaImage(e.CaptchaUri);
				Gtk.Application.Invoke( delegate {
					Captcha captcha = new Captcha(captchaImage);
					captcha.Run();
					captchaText = captcha.CaptchaText;
					captcha.Destroy();
				});
				// wait for user to input captcha
				while (captchaText == "")
					Thread.Sleep(100);
				// retry login
				Hyena.Log.Information("Solved captcha: " + captchaText);
				AttemptLogin(username, password, e.CaptchaId, captchaText);
			}
		}
		
		/// <summary>
		/// try to login, throws exception if captcha is required
		/// </summary>
        protected void AttemptLogin (string username, string password, string captchaId, string captcha) {
            // get login information
            GetLoginInformation();

            NameValueCollection data = System.Web.HttpUtility.ParseQueryString(string.Empty);
            data["source"] = "simple";
            data["form_email"] = username;
            data["form_password"] = password;
			
			if (captcha != null) {
				data["captcha-id"] = captchaId;
				data["captcha-solution"] = captcha;
			}

            // workaround for invalid certificate problem, override certificate validator
            ServicePointManager.ServerCertificateValidationCallback = Validator;
            // Create a request using a URL that can receive a post.
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create ("https://www.douban.com/accounts/login");

            // Set the Method property of the request to POST.
            request.Method = "POST";
            // Create POST data and convert it to a byte array.
            StringBuilder postDataBuilder = new StringBuilder();
            foreach (string key in data.Keys)
            {
                if (postDataBuilder.Length > 0)
                    postDataBuilder.Append("&");
                postDataBuilder.AppendFormat("{0}={1}", key, System.Web.HttpUtility.UrlEncode(data[key]));
            }
            string postData = postDataBuilder.ToString();

            byte[] byteArray = Encoding.UTF8.GetBytes (postData);
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = byteArray.Length;
            // we need a CookieContainer, otherwise response.Cookies is empty
            request.CookieContainer = cookieJar;
            // Set cookies
            // request.CookieContainer.Add(new Cookie("bid", GetBid(), "https://www.douban.com/accounts/login", "www.douban.com"));
            request.AllowAutoRedirect = false;
            request.UserAgent = "Mozilla/5.0 (X11; Linux x86_64; rv:2.0.1) Gecko/20100101 Firefox/4.0.1";
			// Set timeout to 30 seconds
			request.Timeout = 30000;
			request.ReadWriteTimeout = 30000;
            // Get the request stream.
            Stream dataStream = request.GetRequestStream ();
            // Write the data to the request stream.
            dataStream.Write (byteArray, 0, byteArray.Length);
            // Close the Stream object.
            dataStream.Close ();
			
            // Get the response.
			try {
				Hyena.Log.Debug("Logging in to Douban server");
            	HttpWebResponse response = (HttpWebResponse)request.GetResponse ();
				Hyena.Log.Debug("Parsing response from Douban server");
				string location = response.Headers["Location"];
				
				if (location != null) {
					// there is redirection
					Hyena.Log.Debug("Redirected to " + response.Headers["Location"]);					
					if (location.Contains("error=requirecaptcha")) {
						response.Close();
						// get redirection page manually due to redirection bug in Mono
						request = (HttpWebRequest)WebRequest.Create (location);
						request.Method = "GET";
			            request.CookieContainer = cookieJar;
	        			response = (HttpWebResponse)request.GetResponse ();
						throw new CaptchaException(ParseCaptchaId(new StreamReader(response.GetResponseStream()).ReadToEnd()));
					} else if (location.Contains("error=notmatch")) {
						// username/password mismatch
//						LoginErrorEvent();
						throw new DoubanLoginException();
					}
				}
				
				string responseData = new StreamReader(response.GetResponseStream()).ReadToEnd();
				Hyena.Log.Debug(response.ResponseUri.ToString());
				Hyena.Log.Debug(responseData);
				
	            // Read cookies
	            CookieCollection cookies = response.Cookies;
	            try {
	                this.dbcl2 = cookies["dbcl2"].Value.ToString();
	                Hyena.Log.Debug("dbcl2: " + dbcl2);
	            }
	            catch (KeyNotFoundException e) {
	                Hyena.Log.Exception(e);
	                throw new DoubanLoginException();
	            }
	            // Set User ID
	            this.uid = this.dbcl2.Split(new char[] {':'})[0];
	            Hyena.Log.Debug("UID: " + uid);
	            // Set cookies for douban.fm
	            Hyena.Log.Debug("Got cookies for www.douban.com: " + cookieJar.GetCookieHeader(new Uri("http://www.douban.com/")));
	            foreach (Cookie cookie in cookieJar.GetCookies(new Uri("http://www.douban.com/"))) {
	                Hyena.Log.Debug(cookie.ToString());
	                // we need to change the cookie domain for Add to work
	                cookie.Domain = "douban.fm";
	                cookieJar.Add(new Uri("http://douban.fm/"), cookie);
	            }
	            Hyena.Log.Debug("Set cookies for douban.fm: " + cookieJar.GetCookieHeader(new Uri("http://douban.fm/")));
	            // Clean up the streams.
	            response.Close ();				
			}
			catch (WebException e) {
				Hyena.Log.Error("Got WebException when logging in to Douban server" + e.Status.ToString());
				LoginErrorEvent();
			}
			catch (DoubanLoginException) {
				Hyena.Log.Error("Got DoubanLoginException when logging in to Douban server");
				LoginErrorEvent();
			}			
        }
		
		protected string ParseCaptchaId(string html) {
			Regex captchaRegex = new Regex(@"https://www\.douban\.com/misc/captcha\?id=(?<id>\w+)");
			Match m = captchaRegex.Match(html);
			if (m.Success) {
//				string id = m.Groups[0].Value;
				string id = m.Result("${id}");
				Hyena.Log.Debug("Got captcha: " + id);
				return id;
			} else {
				Hyena.Log.Error("Captcha required but no image found");
				LoginErrorEvent();
				return null;
			}
		}

		protected byte[] GetCaptchaImage(string uri) {
			byte[] image;
			Hyena.Log.Debug("Fetching captcha image: " + uri);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create (uri);
            request.Method = "GET";
            request.CookieContainer = cookieJar;
            // Get the response.
            HttpWebResponse response = (HttpWebResponse)request.GetResponse ();
			BinaryReader reader = new BinaryReader(response.GetResponseStream());
			image = reader.ReadBytes(1024 * 1024);
			Hyena.Log.Debug("Captcha image size: " + image.Length.ToString());
			
			return image;
        }

        protected string GetLoginInformation() {
            if (bid != null)
                return bid;

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create ("http://www.douban.com/");
            request.Method = "GET";
            // we need a CookieContainer, otherwise response.Cookies is empty
            request.CookieContainer = cookieJar;
            // Get the response.
            HttpWebResponse response = (HttpWebResponse)request.GetResponse ();

            try {
                bid = response.Cookies["bid"].Value.ToString();
                Hyena.Log.Debug("Bid: " + bid);
            }
            catch (Exception e) {
                Hyena.Log.Exception(e);
                throw new DoubanLoginException();
            }
            response.Close ();
            return bid;
        }
		
		protected string InputCaptcha() {
			return "";
			
		}

        public void LoadChannels () {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create ("http://www.douban.com/j/app/radio/channels");
            request.Method = "GET";
            request.CookieContainer = cookieJar;
            // Get the response.
            HttpWebResponse response = (HttpWebResponse)request.GetResponse ();
            string responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
            // Deserialize the JSON
            Deserializer deserializer = new Deserializer(responseString);
            JsonObject obj = (JsonObject)deserializer.Deserialize();
            JsonArray arr = (JsonArray)obj["channels"];
            foreach (JsonObject c in arr) {
                this.Channels.Add((string)c["name"], new DoubanFMChannel((string)c["name"], ((int)c["channel_id"]).ToString(), (string)c["name_en"]));
            }
            Hyena.Log.Debug("Channels: " + string.Join(",", Channels.Keys.ToArray()));
        }


        protected void InitializeCallback(IAsyncResult result) {
            AsyncResult asyncResult = (AsyncResult)result;
            InitializeHandler handler = (InitializeHandler)asyncResult.AsyncDelegate;
            handler.EndInvoke(result);

			if (uid != null) {
				// Logged in successfully
				Initialized = true;
	            Gtk.Application.Invoke (delegate {
	                RefreshChannels();
	            });			
			}
        }

        /// <summary>
        /// Refresh channels display in content window
        /// </summary>
        protected void RefreshChannels () {
            Hyena.Log.Debug("RefreshChannels");
            contents.UpdateChannels(Channels);
        }

        private string GetHistoryVerb(DoubanFMSong song) {
            if (song.status == DoubanFMSongStatus.Finished)
                return "p";
            else if (song.status == DoubanFMSongStatus.Skipped)
                return "s";
            else
                return "";
        }

//        protected string FormatList<T>(List<T> sidlist, string verb) {
//            if (sidlist.Count == 0) {
//                // List is empty
//                return "";
//            } else {
//                T[] sidarray = sidlist.ToArray();
//                return string.Join("", sidarray.Select(s => "|" + s.ToString() + ":" + verb).ToArray());
//            }
//        }
//
//        protected string FormatList<T>(List<T> sidlist) {
//            return FormatList(sidlist, "");
//        }

        private string FormatHistoryList(int size) {
            var h = GetRecentItems(size);
            if (h == null || h.Count == 0)
                return "";
//            DoubanFMSong historyArray = h.ToArray();
            return string.Join("", h.Select(s => "|" + s.sid + ":" + GetHistoryVerb(s)).ToArray());
        }


        protected Dictionary<string,string> GetDefaultParams(string typeName) {
            var _params = new Dictionary<string, string> ();
            string[] fields = {"aid", "channel", "du", "h", "r", "rest", "sid", "type", "uid"};
            foreach (string s in fields) {
                _params[s] = "";
            }

            Random rnd = new Random();
            _params["r"] = rnd.NextDouble().ToString();
            _params["uid"] = this.uid;
            _params["channel"] = this.channel.ToString();

            if (typeName.Length > 0) {
                _params["type"] = typeName;
            }

            return _params;
        }

        /// <summary>
        /// IO with doufan.fm
        /// </summary>
        protected string RemoteFM(Dictionary<string,string> _params) {
            StringBuilder parassb = new StringBuilder();
            foreach (string key in _params.Keys)
            {
                if (_params[key].Length > 0) {
                    if (parassb.Length > 0)
                        parassb.Append("&");
                    parassb.AppendFormat("{0}={1}", HttpUtility.UrlEncode(key, Encoding.UTF8), HttpUtility.UrlEncode(_params[key], Encoding.UTF8));
                }
            }

            string uri = "http://douban.fm/j/mine/playlist?" + parassb;
            string responseFromServer = "";

            try {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create (uri);
                request.Method = "GET";
                request.CookieContainer = cookieJar;

                Hyena.Log.Debug("Requesting data from " + uri);
                Hyena.Log.Debug(cookieJar.GetCookieHeader(new Uri(uri)));
                // Get the response.
                HttpWebResponse response = (HttpWebResponse)request.GetResponse ();
                StreamReader reader = new StreamReader (response.GetResponseStream ());
                // Read the content.
                responseFromServer = reader.ReadToEnd ();
    
                Hyena.Log.Debug("Response: " + responseFromServer);
            }
            catch (WebException e) {
                Hyena.Log.Exception(e);
            }

            return responseFromServer;
        }

        public List<DoubanFMSong> JsonToDoubanFMSongs(string json) {
            Deserializer deserializer = new Deserializer(json);
            JsonObject obj = (JsonObject)deserializer.Deserialize();
            JsonArray song = (JsonArray)obj["song"];

            return song.Select(s => new DoubanFMSong((JsonObject)s)).ToList();
        }

        /// <summary>
        /// Reset playlist content.
        /// </summary>
        public void ResetPlaylist() {
            playList = NewPlaylist();
            playing = -1;
        }

        /// <summary>
        /// Get recently played songs.
        /// </summary>
        /// <param name="size">
        /// the number of songs to return at most
        /// </param>
        public List<DoubanFMSong> GetRecentItems (int size) {
            if (history == null || history.Count == 0)
                return null;
            if (history.Count < size)
                size = history.Count;
            return history.GetRange(history.Count - size, size);
        }

        #region IDoubanFMPlayQueue implementation
        public List<DoubanFMSong> playList { get; set; }

        public DoubanFMSong Current {
            get {
                return playList[playing];
            }
        }

        public int playing { get; set; }

        public DoubanFMSong PeekNext () {
            if (playList.Count == 0 || playing == playList.Count - 1) {
                ResetPlaylist();
                return playList[0];
            }
            else {
                return playList[playing + 1];
            }
        }

        public DoubanFMSong Next() {
            if (playList.Count == 0) {
                return null;
            }
            playing++;
            return playList[playing];
        }

        #endregion

        #region IO With douban.fm
        /// <summary>
        /// Retrieve a new playlist
        /// </summary>
        public List<DoubanFMSong> NewPlaylist() {
            var _params = GetDefaultParams("n");
            _params["h"] = FormatHistoryList(10);
            string results = RemoteFM(_params);

            return JsonToDoubanFMSongs(results);
        }

        /// <summary>
        /// delete a song from your playlist
        /// </summary>
        /// <param name="sid">
        /// Song ID
        /// </param>
        /// <param name="aid">
        /// Album ID
        /// </param>
        /// <param name="rest">
        /// Rest song IDs in current playlist
        /// </param>
        public List<DoubanFMSong> BanSong(string sid, string aid, List<string> rest) {
            var _params = GetDefaultParams("b");
            _params["sid"] = sid;
            _params["aid"] = aid;
//            _params["rest"] = FormatList<string>(rest);

            string results = RemoteFM(_params);

            return JsonToDoubanFMSongs(results);
        }

        public List<DoubanFMSong> BanSong(string sid, string aid) {
            return BanSong(sid, aid, Enumerable.Empty<string>().ToList());
        }

        public List<DoubanFMSong> FavSong(DoubanFMSong song) {
            var _params = GetDefaultParams("r");
            _params["sid"] = song.sid;
            _params["h"] = FormatHistoryList(10) + "|" + song.sid + ":" + "r";

            string results = RemoteFM(_params);

            return JsonToDoubanFMSongs(results);
        }

        public List<DoubanFMSong> UnfavSong(DoubanFMSong song) {
            var _params = GetDefaultParams("u");
            _params["sid"] = song.sid;
//            _params["aid"] = song.aid;
            _params["h"] = FormatHistoryList(10) + "|" + song.sid + ":" + "u";

            string results = RemoteFM(_params);

            return JsonToDoubanFMSongs(results);
        }

        void HandleDoubanFMSongPlaybackFinished (TrackInfo track, double percentCompleted) {
            Hyena.Log.Debug("HandleDoubanFMSongPlaybackFinished: percentCompleted=" + percentCompleted.ToString());
            if (track == null)
                return;

            var song = track as DoubanFMSong;

            if (percentCompleted > 0.95) {
                Hyena.Log.Debug("Finished playing a song: " + song.TrackTitle);
                song.commited = true;
                PlayedSong(song);

            } else {
                Hyena.Log.Debug("Skipped a song: " + song.TrackTitle);
                song.commited = true;
                SkipSong(song);
            }
        }

        /// <summary>
        /// tell douban that you have finished a song
        /// </summary>
        /// <param name="du">
        /// time your have been idle
        /// </param>
        public void PlayedSong(DoubanFMSong song, int du) {
            var _params = GetDefaultParams("e");
            _params["sid"] = song.sid;
            _params["aid"] = song.aid;
            _params["du"] = du.ToString();

            ThreadAssist.Spawn (delegate {
                try {
                    RemoteFM(_params);
                } catch (System.Net.WebException e) {
                    Hyena.Log.Warning ("Got Exception Trying to PlayedSong", e.ToString (), false);
                }
            });
            song.status = DoubanFMSongStatus.Finished;
            history.Add(song);
        }

        public void PlayedSong(DoubanFMSong song) {
            PlayedSong(song, 0);
        }

        /// <summary>
        /// tell douban that you have skipped a song
        /// </summary>
        /// <param name="history">
        /// playlist history <see cref="List<DoubanFMSong>"/>
        /// </param>
        public void SkipSong(DoubanFMSong song, List<DoubanFMSong> history) {
            var _params = GetDefaultParams("s");
            _params["sid"] = song.sid;
            _params["aid"] = song.aid;
            _params["h"] = FormatHistoryList(10) + "|" + song.sid + ":" + "s";

            ThreadAssist.Spawn (delegate {
                try {
                    RemoteFM(_params);
                } catch (System.Net.WebException e) {
                    Hyena.Log.Warning ("Got Exception Trying to SkipSong", e.ToString (), false);
                }
            });
            song.status = DoubanFMSongStatus.Skipped;
            history.Add(song);
        }

        public void SkipSong(DoubanFMSong song) {
            SkipSong(song, Enumerable.Empty<DoubanFMSong>().ToList());
        }

        /// <summary>
        /// request more playlist items
        /// </summary>
        /// <param name="history">
        /// your playlist history(played songs and skipped songs)
        /// </param>
        public List<DoubanFMSong> PlayedList(List<int> history) {
            var _params = GetDefaultParams("p");
            _params["h"] = FormatHistoryList(10);

            string results = RemoteFM(_params);

            return JsonToDoubanFMSongs(results);
        }

        #endregion

        public void Dispose() {
            DisconnectPlaybackFinished();
        }
    }
}
