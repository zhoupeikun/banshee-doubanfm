// 
// DoubanFMSourceContents.cs
//  Douban FM Plugin for Banshee. Source contents based on Magnatune plugin.
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
using Banshee.Sources;
using Banshee.Sources.Gui;
using Gtk;
using Mono.Unix;

namespace Banshee.DoubanFM
{
    public class DoubanFMSourceContents: Hyena.Widgets.ScrolledWindow, ISourceContents
    {
        private DoubanFMSource fmSource;

        private VBox main_box;
        private Viewport viewport;

        private TitledList channels;
        private Image logo;
        private Gdk.Pixbuf logo_pix;

        public DoubanFMSourceContents ()
        {
            HscrollbarPolicy = PolicyType.Never;
            VscrollbarPolicy = PolicyType.Automatic;

            viewport = new Viewport ();
            viewport.ShadowType = ShadowType.None;

            main_box = new VBox ();
            main_box.Spacing = 6;
            main_box.BorderWidth = 5;
            main_box.ReallocateRedraws = true;

            // Clamp the width, preventing horizontal scrolling
            SizeAllocated += delegate(object o, SizeAllocatedArgs args) {
                main_box.WidthRequest = args.Allocation.Width - 10;
            };

            viewport.Add (main_box);

            StyleSet += delegate {
                viewport.ModifyBg (StateType.Normal, Style.Base (StateType.Normal));
                viewport.ModifyFg (StateType.Normal, Style.Text (StateType.Normal));
            };

            logo_pix = new Gdk.Pixbuf (System.Reflection.Assembly.GetExecutingAssembly ()
                                       .GetManifestResourceStream ("doubanfm_banner.jpg"));
            logo = new Image (logo_pix);

            // auto-scale logo
            // aspect ratio of logo is 4.55
            SizeAllocated += delegate(object o, SizeAllocatedArgs args) {
                int width = args.Allocation.Width - 50;
                logo.Pixbuf = logo_pix.ScaleSimple (width, (int)((float)width / 4.55f), Gdk.InterpType.Bilinear);
            };

            main_box.PackStart (logo, false, false, 0);

            channels = new TitledList (Catalog.GetString("Channels"));
            main_box.PackStart (channels, false, false, 0);

            AddWithFrame (viewport);
            ShowAll ();
        }

        public bool SetSource (ISource src)
        {
            fmSource = src as DoubanFMSource;
            if (fmSource == null) {
                return false;
            }

            // event handlers
            channels.ChangeChannelEvent += fmSource.ChangeChannel;

            if (fmSource.fm != null) {
                UpdateChannels(fmSource.fm.Channels);
            }
            else {
                UpdateChannels ();
            }

            return true;
        }

        public void UpdateChannels ()
        {
            this.channels.SetList();
        }

        public void UpdateChannels (Dictionary<string,DoubanFMChannel> channels) {
            this.channels.SetList(channels);
        }

        public ISource Source {
            get { return fmSource; }
        }

        public void ResetSource ()
        {
            fmSource = null;
        }

        public Widget Widget {
            get { return this; }
        }

        public void Refresh ()
        {
//            if (genres != null)
//                UpdateGenres ();
        }
    }
}

