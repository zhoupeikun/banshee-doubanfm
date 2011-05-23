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

    /// <summary>
    /// Douban FM service.
    /// </summary>
    public class DoubanFM : IDoubanFMPlayQueue
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

        private string username;
        private string password;
        private int _channel;
        private CookieContainer cookieJar;
        private DoubanFMSourceContents contents;


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

            InitializeHandler asyncInitializeHandler = Initialize;
            asyncInitializeHandler.BeginInvoke(IntializeCallback, null);

            // connect events
            DoubanFMSong.PlaybackFinished += HandleDoubanFMSongPlaybackFinished;
        }

        public void Initialize() {
            Thread loadChannelsThread = new Thread(new ThreadStart(LoadChannels));
            loadChannelsThread.Start();

            Login(username, password);

            loadChannelsThread.Join();
        }


        /// <summary>
        /// workaround for invalid certificate problem
        /// </summary>
        public static bool Validator (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
            return true;
        }

        /// <summary>
        /// login douban, get session token
        /// </summary>
        protected void Login (string username, string password) {
            // get login information
            GetLoginInformation();

            NameValueCollection data = System.Web.HttpUtility.ParseQueryString(string.Empty);
            data["source"] = "simple";
            data["form_email"] = username;
            data["form_password"] = password;

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
            // Set the ContentType property of the WebRequest.
            request.ContentType = "application/x-www-form-urlencoded";
            // Set the ContentLength property of the WebRequest.
            request.ContentLength = byteArray.Length;
            // we need a CookieContainer, otherwise response.Cookies is empty
            request.CookieContainer = cookieJar;
            // Set cookies
            // request.CookieContainer.Add(new Cookie("bid", GetBid(), "https://www.douban.com/accounts/login", "www.douban.com"));
            // No auto-redirect
            request.AllowAutoRedirect = false;
            // Set user-agent
            request.UserAgent = "Mozilla/5.0 (X11; Linux x86_64; rv:2.0.1) Gecko/20100101 Firefox/4.0.1";
            // Get the request stream.
            Stream dataStream = request.GetRequestStream ();
            // Write the data to the request stream.
            dataStream.Write (byteArray, 0, byteArray.Length);
            // Close the Stream object.
            dataStream.Close ();
            // Get the response.
            HttpWebResponse response = (HttpWebResponse)request.GetResponse ();

            // Read cookies
            CookieCollection cookies = response.Cookies;
            try {
                this.dbcl2 = cookies["dbcl2"].Value.ToString();
                Hyena.Log.Information("dbcl2: " + dbcl2);
            }
            catch (KeyNotFoundException e) {
                Hyena.Log.Exception(e);
                throw new DoubanLoginException();
            }
            // Set User ID
            this.uid = this.dbcl2.Split(new char[] {':'})[0];
            Hyena.Log.Information("UID: " + uid);
            // Set cookies for douban.fm
            Hyena.Log.Information("Got cookies for www.douban.com: " + cookieJar.GetCookieHeader(new Uri("http://www.douban.com/")));
//            cookieJar.SetCookies(new Uri("http://douban.fm/"), cookieJar.GetCookieHeader(new Uri("http://www.douban.com/")));
//            cookieJar.Add(new Uri("http://douban.fm/"), cookieJar.GetCookies(new Uri("http://www.douban.com/")));
            foreach (Cookie cookie in cookieJar.GetCookies(new Uri("http://www.douban.com/"))) {
                Hyena.Log.Information(cookie.ToString());
                // we need to change the cookie domain for Add to work
                cookie.Domain = "douban.fm";
                cookieJar.Add(new Uri("http://douban.fm/"), cookie);
            }
            Hyena.Log.Information("Set cookies for douban.fm: " + cookieJar.GetCookieHeader(new Uri("http://douban.fm/")));
            // Clean up the streams.
            response.Close ();
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
                Hyena.Log.Information("Bid: " + bid);
            }
            catch (Exception e) {
                Hyena.Log.Exception(e);
                throw new DoubanLoginException();
            }
            response.Close ();
            return bid;
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


        protected void IntializeCallback(IAsyncResult result) {
            AsyncResult asyncResult = (AsyncResult)result;
            InitializeHandler handler = (InitializeHandler)asyncResult.AsyncDelegate;
            handler.EndInvoke(result);

            Gtk.Application.Invoke (delegate {
                RefreshChannels();
            });
        }

        /// <summary>
        /// Refresh channels display in content window
        /// </summary>
        protected void RefreshChannels () {
            Hyena.Log.Information("RefreshChannels");
            contents.UpdateChannels(Channels);
        }

        protected string FormatList<T>(List<T> sidlist, string verb) {
            if (sidlist.Count == 0) {
                // List is empty
                return "";
            } else {
                T[] sidarray = sidlist.ToArray();
                return string.Join("", sidarray.Select(s => "|" + s.ToString() + ":" + verb).ToArray());
            }
        }

        protected string FormatList<T>(List<T> sidlist) {
            return FormatList(sidlist, "");
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

                Hyena.Log.Information("Requesting data from " + uri);
                Hyena.Log.Information(cookieJar.GetCookieHeader(new Uri(uri)));
                // Get the response.
                HttpWebResponse response = (HttpWebResponse)request.GetResponse ();
                StreamReader reader = new StreamReader (response.GetResponseStream ());
                // Read the content.
                responseFromServer = reader.ReadToEnd ();
    
                Hyena.Log.Information("Response: " + responseFromServer);
            }
            catch (WebException e) {
                Hyena.Log.Exception(e);
            }

            return responseFromServer;
        }

        public List<DoubanFMSong> JsonToDoubanFMSongs(string json) {
            Deserializer deserializer = new Deserializer(json);
            JsonObject obj = (JsonObject)deserializer.Deserialize();
            // Information
//            Hyena.Log.Information("Got songs: " + obj.ToString());
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

//        public DoubanFMSong Previous { get; set; }

        #region IO With douban.fm


        /// <summary>
        /// Retrieve a new playlist
        /// </summary>
        /// <param name="history">
        /// history song IDs
        /// </param>
        /// </summary>
        public List<DoubanFMSong> NewPlaylist(List<int> history) {
            var _params = GetDefaultParams("n");
            _params["h"] = FormatList(history, "True");
            string results = RemoteFM(_params);

            return JsonToDoubanFMSongs(results);
        }

        /// <summary>
        /// Retrieve a new playlist
        /// </summary>
        public List<DoubanFMSong> NewPlaylist() {
            return NewPlaylist(new List<int>());
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
            _params["rest"] = FormatList<string>(rest);

            string results = RemoteFM(_params);

            return JsonToDoubanFMSongs(results);
        }

        public List<DoubanFMSong> BanSong(string sid, string aid) {
            return BanSong(sid, aid, Enumerable.Empty<string>().ToList());
        }

        public List<DoubanFMSong> FavSong(string sid, string aid) {
            var _params = GetDefaultParams("r");
            _params["sid"] = sid;
//            _params["aid"] = aid;

            string results = RemoteFM(_params);

            return JsonToDoubanFMSongs(results);
        }

        public List<DoubanFMSong> UnfavSong(string sid, string aid) {
            var _params = GetDefaultParams("u");
            _params["sid"] = sid;
            _params["aid"] = aid;

            string results = RemoteFM(_params);

            return JsonToDoubanFMSongs(results);
        }

        void HandleDoubanFMSongPlaybackFinished (TrackInfo track, double percentCompleted) {
            var song = track as DoubanFMSong;
            Hyena.Log.Information("HandleDoubanFMSongPlaybackFinished: percentCompleted=" + percentCompleted.ToString());
//            if ((percentCompleted > 0.9) && (track.PlayCount > 0)) {
            if (percentCompleted > 0.9) {
                Hyena.Log.Information("Finished playing a song: " + song.TrackTitle);
                PlayedSong(song.sid, song.aid);
            } else {
                Hyena.Log.Information("Skipped a song: " + song.TrackTitle);
                 SkipSong(song.sid, song.aid);
            }
        }

        /// <summary>
        /// tell douban that you have finished a song
        /// </summary>
        /// <param name="du">
        /// time your have been idle
        /// </param>
        public void PlayedSong(string sid, string aid, int du) {
            var _params = GetDefaultParams("e");
            _params["sid"] = sid;
            _params["aid"] = aid;
            _params["du"] = du.ToString();

            RemoteFM(_params);
        }

        public void PlayedSong(string sid, string aid) {
            PlayedSong(sid, aid, 0);
        }

        /// <summary>
        /// tell douban that you have skipped a song
        /// </summary>
        /// <param name="history">
        /// playlist history <see cref="List<DoubanFMSong>"/>
        /// </param>
        public void SkipSong(string sid, string aid, List<DoubanFMSong> history) {
            var _params = GetDefaultParams("s");
            _params["sid"] = sid;
            _params["aid"] = aid;
//            _params["h"] = FormatList<string>(history.GetRange(history.Count-50-1, 50).Select(s => s.sid).ToList());

            RemoteFM(_params);
        }

        public void SkipSong(string sid, string aid) {
            SkipSong(sid, aid, Enumerable.Empty<DoubanFMSong>().ToList());
        }

        /// <summary>
        /// request more playlist items
        /// </summary>
        /// <param name="history">
        /// your playlist history(played songs and skipped songs)
        /// </param>
        public List<DoubanFMSong> PlayedList(List<int> history) {
            var _params = GetDefaultParams("p");
            _params["h"] = FormatList(history.GetRange(history.Count-50-1, 50));

            string results = RemoteFM(_params);

            return JsonToDoubanFMSongs(results);
        }

        #endregion
    }
}
