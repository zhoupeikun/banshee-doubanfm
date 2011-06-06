//
// DoubanFMCoverFecthJob.cs
//
// Copyright (C) 2011 Chen Tao <pro711@gmail.com>
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.IO;
using System.Net;
using System.Xml;
using System.Text;
using System.Collections.Generic;

using Hyena;

using Banshee.Base;
using Banshee.Metadata;
using Banshee.ServiceStack;
using Banshee.Kernel;
using Banshee.Collection;
using Banshee.Streaming;
using Banshee.Networking;

namespace Banshee.DoubanFM
{
    public class DoubanFMCoverFecthJob : MetadataServiceJob
    {
        private DoubanFMSong song;

        public DoubanFMCoverFecthJob (DoubanFMSong song) : base ()
        {
            this.song = song;
			Track = song;
        }

        public static string ArtworkIdFor (DoubanFMSong song)
        {
            string digest = CoverArtSpec.CreateArtistAlbumId(song.ArtistName, song.AlbumTitle);
            return digest;
        }

        public override void Run()
        {
            Fetch ();
        }

        public void Fetch ()
        {
            if (song.picture == null) {
                return;
            }

            string cover_art_id = ArtworkIdFor (song);

            if (cover_art_id == null) {
                return;
            } else if (CoverArtSpec.CoverExists (cover_art_id)) {
                return;
            } else if (!InternetConnected) {
                return;
            }

            // Download cover from Douban
            try {
                if (
				    // first attempt - large album art
				    SaveHttpStreamCover (new Uri (song.picture.Replace("/mpic/", "/lpic/")), cover_art_id, null) || 
				    // second attempt - normal album art
				    SaveHttpStreamCover (new Uri (song.picture), cover_art_id, null)) {
                    Log.Debug ("Downloaded cover art from Douban", cover_art_id);
                    StreamTag tag = new StreamTag ();
                    tag.Name = CommonTags.AlbumCoverId;
                    tag.Value = cover_art_id;
                    AddTag (tag);
					StreamTagger.TrackInfoMerge((TrackInfo)song, tag);
					// tell player engine that track info has updated
					if ((TrackInfo)song == ServiceManager.PlayerEngine.CurrentTrack) {
						ServiceManager.Get<Banshee.Collection.Gui.ArtworkManager> ().ClearCacheFor (song.ArtworkId);
						ServiceManager.PlayerEngine.TrackInfoUpdated();
					}
					
                    return;
                }
            } catch (Exception e) {
                Hyena.Log.Exception ("Downloading cover art from Douban failed", e);
            }

        }
    }
}