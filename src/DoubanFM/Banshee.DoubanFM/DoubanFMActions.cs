// 
// DoubanFMActions.cs
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
using Gtk;

using Mono.Unix;

using Banshee.Collection;
using Banshee.Gui;
using Banshee.MediaEngine;
using Banshee.ServiceStack;
using Banshee.Sources;
using Banshee.Configuration;


namespace Banshee.DoubanFM
{
    public class DoubanFMActions : BansheeActionGroup
    {
        private DoubanFMSource fmSource;
        private uint actions_id;
        
        public DoubanFMActions (DoubanFMSource fmSource) : base (ServiceManager.Get<InterfaceActionService> (), "DoubanFM")
        {
            this.fmSource = fmSource;

            // Track actions
            Add (new ActionEntry [] {
                new ActionEntry (
                    "DoubanFMFavAction", null,
                    Catalog.GetString ("Love Track"), null,
                    Catalog.GetString ("Mark current track as loved"), OnLoved),

                new ActionEntry (
                    "DoubanFMUnfavAction", null,
                    Catalog.GetString ("Cancel Love Track"), null,
                    Catalog.GetString ("Cancel marking track as loved"), OnCancelLoved),

                new ActionEntry (
                    "DoubanFMHateAction", null,
                    Catalog.GetString ("Ban Track"), null,
                    Catalog.GetString ("Mark current track as banned"), OnHated)
            });

            this["DoubanFMFavAction"].IconName = "face-smile";
            this["DoubanFMUnfavAction"].IconName = "face-smile";
            this["DoubanFMHateAction"].IconName = "face-sad";

            this["DoubanFMFavAction"].IsImportant = true;
            this["DoubanFMUnfavAction"].IsImportant = true;
            this["DoubanFMHateAction"].IsImportant = true;

            Add(new ActionEntry[] {
                new ActionEntry ("DoubanFMAction", null,
                                 Catalog.GetString ("_DoubanFM"), null,
                                 Catalog.GetString ("Configure DoubanFM"), null),
                new ActionEntry ("DoubanFMConfigureAction", Stock.Properties,
                                 Catalog.GetString ("_Configure"), null,
                                 Catalog.GetString ("Configure DoubanFM"), OnConfigurePlugin)
            });

            actions_id = Actions.UIManager.AddUiFromResource ("UI.xml");
            Actions.AddActionGroup (this);

            ServiceManager.PlaybackController.SourceChanged += OnPlaybackSourceChanged;
            ServiceManager.PlayerEngine.ConnectEvent (OnPlayerEvent,
                PlayerEvent.StartOfStream |
                PlayerEvent.EndOfStream);
            UpdateActions ();
        }

        public override void Dispose ()
        {
            Actions.UIManager.RemoveUi (actions_id);
            Actions.RemoveActionGroup (this);
//            lastfm.Connection.StateChanged -= HandleConnectionStateChanged;
//            Actions.SourceActions ["SourcePropertiesAction"].Activated -= OnSourceProperties;
            ServiceManager.PlaybackController.SourceChanged -= OnPlaybackSourceChanged;
            ServiceManager.PlayerEngine.DisconnectEvent (OnPlayerEvent);
            base.Dispose ();
        }

        #region Action Handlers
        public void OnConfigurePlugin (object o, EventArgs args)
        {
            Configuration config = new Configuration (UsernameSchema.Get (), PasswordSchema.Get ());
            config.Run ();
            config.Destroy ();
        }

        private void OnLoved (object sender, EventArgs args)
        {
            Hyena.Log.Information("Loved a track.");
            DoubanFMSong song = ServiceManager.PlayerEngine.CurrentTrack as DoubanFMSong;
            if (song == null)
                return;
            fmSource.fm.playList = fmSource.fm.FavSong(song.sid, song.aid);
            song.like = true;
            UpdateActions();
        }

        private void OnCancelLoved (object sender, EventArgs args)
        {
            Hyena.Log.Information("Cancel loving a track.");
            DoubanFMSong song = ServiceManager.PlayerEngine.CurrentTrack as DoubanFMSong;
            if (song == null)
                return;
            fmSource.fm.playList = fmSource.fm.UnfavSong(song.sid, song.aid);
            song.like = false;
            UpdateActions();
        }

        private void OnHated (object sender, EventArgs args)
        {
            Hyena.Log.Information("Hated a track.");
            DoubanFMSong song = ServiceManager.PlayerEngine.CurrentTrack as DoubanFMSong;
            if (song == null)
                return;

            fmSource.fm.playList = fmSource.fm.BanSong(song.sid, song.aid);
            ServiceManager.PlaybackController.Next ();
        }

        #endregion

        private void OnPlayerEvent (PlayerEventArgs args)
        {
            UpdateActions ();
        }
//
//        private void HandleConnectionStateChanged (object sender, ConnectionStateChangedArgs args)
//        {
//            UpdateActions ();
//        }
//
        private bool updating = false;
        private void UpdateActions ()
        {
            lock (this) {
                if (updating)
                    return;
                updating = true;
            }

            TrackInfo current_track = ServiceManager.PlayerEngine.CurrentTrack;
            this["DoubanFMFavAction"].Visible = (current_track is DoubanFMSong) && !((DoubanFMSong)current_track).like;
            this["DoubanFMUnfavAction"].Visible = (current_track is DoubanFMSong) && ((DoubanFMSong)current_track).like;
            // only personal channel has hate action
            this["DoubanFMHateAction"].Visible = (current_track is DoubanFMSong) && (fmSource.fm.channel == 0);

            updating = false;
        }

        private void OnPlaybackSourceChanged (object o, EventArgs args)
        {
            if (Actions == null || Actions.PlaybackActions == null || ServiceManager.PlaybackController == null)
                return;

            UpdateActions ();

            bool is_doubanfm = ServiceManager.PlaybackController.Source is DoubanFMSource;
            Actions.PlaybackActions["PreviousAction"].Sensitive = !is_doubanfm;

            // TODO
//            if (is_doubanfm && !was_doubanfm)
//                track_actions_id = Actions.UIManager.AddUiFromResource ("LastfmTrackActions.xml");
//            else if (!is_doubanfm && was_doubanfm)
//                Actions.UIManager.RemoveUi (track_actions_id);

//            was_doubanfm = is_doubanfm;
        }

        public static readonly SchemaEntry<string> UsernameSchema = new SchemaEntry<string> (
            "plugins.doubanfm", "user", "", "Douban username", "Douban username");

        public static readonly SchemaEntry<string> PasswordSchema = new SchemaEntry<string> (
            "plugins.doubanfm", "pass", "", "Douban password", "Douban password");
    }
}

