// 
// DoubanFMSong.cs
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
using Banshee.Collection;
using Hyena;
using Hyena.Json;

namespace Banshee.DoubanFM
{
    public class DoubanFMSong : TrackInfo
    {
        public override string TrackTitle { get; set; }
        public override string AlbumTitle { get; set; }
        public override string ArtistName { get; set; }
        public string company;
        public string sid;
        public string aid;
        public string ssid;
        public string picture;
        public override TimeSpan Duration { get; set; }
        public bool like { get; set; }

        public override SafeUri Uri {
            get;
            set;
        }

        /// <summary>
        /// Lookup for an field in a JsonObject
        /// </summary>
        private static T Lookup<T>(JsonObject o, string key, T fallback) {
            return o.ContainsKey(key) ? (T)o[key] : fallback;
        }

        public DoubanFMSong (JsonObject o) : base() {
            try {
                TrackTitle = Lookup<string>(o, "title", "Unknown");
                AlbumTitle = Lookup<string>(o, "albumtitle", "Unknown");
                ArtistName = Lookup<string>(o, "artist", "Unknown");
                company = Lookup<string>(o, "company", "");
                sid = Lookup<string>(o, "sid", "");
                aid = Lookup<string>(o, "aid", "");
                ssid = Lookup<string>(o, "ssid", "");
                picture = Lookup<string>(o, "picture", "");
                like = Lookup<string>(o, "like", "0") == "0" ? false : true;
                Duration = new TimeSpan(0, 0, Lookup<int>(o, "length", 0));
                this.Uri = new SafeUri((string)o["url"]);
            }
            catch (Exception e) {
                Hyena.Log.Exception(e);
                throw new DoubanInvalidDataException(e.Message);
            }
        }
    }
}
