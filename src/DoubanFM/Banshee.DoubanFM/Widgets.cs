//
// Widgets.cs
//  Douban FM Plugin for Banshee. Widgets for GTK based on Magnatune widgets.
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

using Banshee.Streaming;
using Banshee.Widgets;
using Hyena;
using Gtk;
using Gdk;
using System;
using System.Collections.Generic;
using Mono.Addins;

namespace Banshee.DoubanFM
{
    public class TitledList : VBox
    {
        private Label title;
        private TileView tile_view;
        private static readonly Color active_color = new Color(0xff, 0x00, 0x00);
        private MenuTile active_tile;
        private List<MenuTile> tiles;

        public TitledList (string title_str) : base()
        {
            title = new Label ();
            title.Xalign = 0;
            title.Ellipsize = Pango.EllipsizeMode.End;
            title.Markup = String.Format ("<b>{0}</b>", GLib.Markup.EscapeText (title_str));

            PackStart (title, false, false, 0);
            title.Show ();

            StyleSet += delegate {
                title.ModifyBg (StateType.Normal, Style.Base (StateType.Normal));
                title.ModifyFg (StateType.Normal, Style.Text (StateType.Normal));
            };

            tile_view = new TileView (2);
            PackStart (tile_view, true, true, 0);
            tile_view.Show ();

            StyleSet += delegate {
                tile_view.ModifyBg (StateType.Normal, Style.Base (StateType.Normal));
                tile_view.ModifyFg (StateType.Normal, Style.Base (StateType.Normal));
            };
        }

        public void SetList (Dictionary<string, DoubanFMChannel> channels)
        {
//            Hyena.Log.Debug("Number of channels: " + channels.Count.ToString());

            if (channels.Count == 0) {
                tile_view.ClearWidgets ();
                MenuTile tile = new MenuTile();
                tile.PrimaryText = AddinManager.CurrentLocalizer.GetString("Loading channels");
                tile.SecondaryText = AddinManager.CurrentLocalizer.GetString("Please wait...");
                tile_view.AddWidget(tile);
            }
            else {
                if (tiles == null) {
                    tile_view.ClearWidgets ();
                    tiles = new List<MenuTile>();
                    foreach (KeyValuePair<string, DoubanFMChannel> p in channels) {
                        MenuTile tile =  new MenuTile();
                        tile.PrimaryText = p.Key;
                        tile.SecondaryText = p.Value.englishName;
                        tile.ButtonPressEvent += PlayChannel;
                        tile_view.AddWidget(tile);
                        tiles.Add(tile);
                    }
                }
//                else {
//                    foreach (MenuTile tile in tiles) {
//                        tile_view.AddWidget(tile);
//                    }
//                }
            }

            tile_view.ShowAll ();
        }

        public void SetList() {
            SetList(new Dictionary<string, DoubanFMChannel>());
        }

        public delegate void ChangeChannelHandler(string channel);
        public event ChangeChannelHandler ChangeChannelEvent;

        private void PlayChannel (object sender, ButtonPressEventArgs args)
        {
            if (active_tile != null) {
                // reset color
                active_tile.ModifyText(StateType.Normal);
            }
            MenuTile tile = sender as MenuTile;
            Hyena.Log.Debug(string.Format ("Tuning Douban FM to {0}", tile.PrimaryText), null);
            ChangeChannelEvent(tile.PrimaryText);
            tile.ModifyText(Gtk.StateType.Normal, active_color);
            active_tile = tile;
        }
    }
}
