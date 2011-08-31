//
// DoubanFMSource.cs
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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;

using Mono.Addins;
using Mono.Unix;

using Banshee.Base;
using Banshee.Sources;
using Banshee.Sources.Gui;

// Other namespaces you might want:
using Banshee.Configuration;
using Banshee.Gui;
using Banshee.ServiceStack;
//using Banshee.Preferences;
using Banshee.MediaEngine;
using Banshee.Collection;
using Banshee.PlaybackController;
using Banshee.Streaming;

using Gtk;
using Gdk;
using Hyena;
using Hyena.Json;


namespace Banshee.DoubanFM
{
    // We are inheriting from Source, the top-level, most generic type of Source.
    // Other types include (inheritance indicated by indentation):
    //      DatabaseSource - generic, DB-backed Track source; used by PlaylistSource
    //        PrimarySource - 'owns' tracks, used by DaapSource, DapSource
    //          LibrarySource - used by Music, Video, Podcasts, and Audiobooks
    public class DoubanFMSource : Source, IBasicPlaybackController, ITrackModelSource, IDisposable
    {
        private DoubanFMActions actions;

        private DoubanFMSourceContents contents;
        private TrackListModel trackListModel;
        public DoubanFM fm {
            get;
            private set;
        }
//        private PlayerState state;
//        private DoubanFMSong currentSong;

        // In the sources TreeView, sets the order value for this source, small on top
        const int sort_order = 190;

        public DoubanFMSource () : base (AddinManager.CurrentLocalizer.GetString ("DoubanFM"),
                                               AddinManager.CurrentLocalizer.GetString ("DoubanFM"),
		                                       sort_order,
		                                       "doubanfm")
        {
            Pixbuf icon = new Pixbuf (System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("doubanfm.png"));
            Properties.Set<Pixbuf> ("Icon.Pixbuf_16", icon.ScaleSimple (16, 16, InterpType.Bilinear));

            // initialize gettext
            Catalog.Init ("banshee-extension-doubanfm", System.IO.Path.Combine (
                Hyena.Paths.InstalledApplicationDataRoot, "locale"));


            // actions
            actions = new DoubanFMActions(this);

            trackListModel = new MemoryTrackListModel();
            ServiceManager.SourceManager.AddSource(this);
			
        }

        public Dictionary<string, DoubanFMChannel> GetChannels ()
        {
            return this.fm.Channels;
        }

        // A count of 0 will be hidden in the source TreeView
        public override int Count {
            get { return 0; }
        }

        public override void Activate ()
        {
            base.Activate();
            if (this.fm == null) {
                try {
                    contents = new DoubanFMSourceContents();
                    Properties.Set<ISourceContents> ("Nereid.SourceContents", contents);
                    Properties.Set<bool> ("Nereid.SourceContents.HeaderVisible", false);
                    if (DoubanFMActions.UsernameSchema.Get().Length == 0 || DoubanFMActions.PasswordSchema.Get().Length == 0) {
                        actions.OnConfigurePlugin(this, null);
                    }
                    this.fm = new DoubanFM(DoubanFMActions.UsernameSchema.Get(), DoubanFMActions.PasswordSchema.Get(), contents);

                    ServiceManager.PlaybackController.NextSource = this;
                    ServiceManager.PlayerEngine.ConnectEvent(Next, PlayerEvent.RequestNextTrack);
//                    ServiceManager.PlayerEngine.ConnectEvent(FinishSong, PlayerEvent.EndOfStream);
//                    ServiceManager.PlayerEngine.ConnectEvent(StartSong, PlayerEvent.StartOfStream);
//                       ServiceManager.PlayerEngine.ConnectEvent(StateChanged, PlayerEvent.StateChange);
                }
                catch (DoubanLoginException e) {
                    Hyena.Log.Error("Douban FM login error: " + e.Message);
                }
            }
        }

        public override void Deactivate ()
        {
            if (this.fm == null)
               return;
            this.fm.DisconnectPlaybackFinished();
        }

        public void ChangeChannel(DoubanFMChannel channel) {
            int newChannel = int.Parse(channel.id);
            if (fm.channel == newChannel) {
                if (!ServiceManager.PlayerEngine.IsPlaying())
                    Next(true, true);
                return;
            }
            fm.channel = newChannel;
            fm.ResetPlaylist();
            // start playing new list
            Next(true, true);
        }

        public void ChangeChannel(string channel) {
            DoubanFMChannel c;
            fm.Channels.TryGetValue(channel, out c);
            if (c != null) {
                ChangeChannel(c);
            }
        }

        private void Next (PlayerEventArgs args) {
            Next(true, true);
        }

        private void StartSong (PlayerEventArgs args) {
            Hyena.Log.Information("Start of stream.");
        }

        private void FinishSong (PlayerEventArgs args) {
            Hyena.Log.Information("End of stream reached.");
//            fm.PlayedSong(fm.Current.sid, fm.Current.aid);
        }

        private void StateChanged (PlayerEventArgs args) {
//            PlayerState currentState = ServiceManager.PlayerEngine.CurrentState;
//            var song = ServiceManager.PlayerEngine.CurrentTrack;
//
//            if (state == PlayerState.Loaded && currentState == PlayerState.Playing) {
//                state = PlayerState.Playing;
//                if (song is DoubanFMSong)
//                    currentSong = song as DoubanFMSong;
//            }
//            else if (state == PlayerState.Playing && currentState == PlayerState.Idle) {
//                // FIXME : temporary workaround for marking song as played problem when gapless playing is enabled
//                if (currentSong != null) {
////                    Hyena.Log.Debug(string.Format("{0}:{1}", ServiceManager.PlayerEngine.Position, ServiceManager.PlayerEngine.Length));
////                    if (!currentSong.commited && ServiceManager.PlayerEngine.Position >= ServiceManager.PlayerEngine.Length - 1) {
////                        currentSong.commited = true;
////                        this.fm.PlayedSong(currentSong.sid, currentSong.aid);
////                    }
//                    ThreadAssist.Spawn( delegate {
//                        var songToCommit = currentSong;
//                        Thread.Sleep(57000);
//                        if (!songToCommit.commited)
//                            this.fm.PlayedSong(songToCommit.sid, songToCommit.aid);
//                    });
//                }
//            }
//            state = currentState;
        }

        #region IBasicPlaybackController implementation
        public bool Next (bool restart, bool changeImmediately)
        {
            if (fm == null || fm.Initialized == false) {
                return false;
            }
            var song = fm.PeekNext();
            if (song == null) {
                Hyena.Log.Information("Got null from PeekNext!");
                return false;
            }
            Hyena.Log.Debug(song.ToString());
            if (changeImmediately) {
                ServiceManager.PlayerEngine.OpenPlay(song);
                fm.Next();

                // try download cover art
            	Banshee.Kernel.Scheduler.Schedule (new Banshee.DoubanFM.DoubanFMCoverFecthJob (song), Banshee.Kernel.JobPriority.Normal);
            }
            return true;
        }

        public bool First () {
            return false;
        }

        public bool Previous (bool restart) {
            return false;
        }

        #endregion

        #region ITrackModelSource implementation
        public void Reload () {
            trackListModel.Reload();
        }

        public TrackListModel TrackModel { get { return trackListModel; } }

        public bool HasDependencies { get { return false; } }

        public bool CanAddTracks { get { return false; } }

        public bool CanRemoveTracks { get { return false; } }

        public bool CanDeleteTracks { get { return false; } }

        public bool ConfirmRemoveTracks { get { return false; } }

        public bool CanRepeat { get { return true; } }

        public bool CanShuffle { get { return false; } }

        public bool ShowBrowser { get { return false; } }

        public bool Indexable { get { return false; } }

        public void RemoveTracks (Hyena.Collections.Selection selection)
        {
        }

        public void DeleteTracks (Hyena.Collections.Selection selection)
        {
        }
        #endregion

        public void Dispose ()
        {
            actions.Dispose();
            actions = null;
            ServiceManager.PlayerEngine.DisconnectEvent(Next);
        }


    }
}
