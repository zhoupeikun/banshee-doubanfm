// 
// DoubanFMChannel.cs
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
using System.Collections.Generic;
using Hyena.Json;
using System;

namespace Banshee.DoubanFM
{
	public class DoubanFMChannel
	{
		private static IList<DoubanFMChannel> _InternalChannels = null;
		private  static DoubanFMChannel _PersonalChannel = null;
		internal const string PersonalChannelId = "0";
		//Internal Channels
		public static IList<DoubanFMChannel> InternalChannels {
			get { 
				if (_InternalChannels == null) {
					_InternalChannels = new List<DoubanFMChannel> ();
					_InternalChannels.Add (new DoubanFMChannel ("红心电台", "-3", "Red Heart"));
				}
				return _InternalChannels;
			}
		}
		
		//Personal Channel
		public static DoubanFMChannel PersonalChannel {
			get{ return _PersonalChannel;}
			set{ _PersonalChannel = value;}
		}
		
		public string name {
			get;
			set;
		}

		public string id {
			get;
			set;
		}

		public string englishName {
			get;
			set;
		}

		public DoubanFMChannel (string name, string id, string englishName)
		{
			this.name = name;
			this.id = id;
			this.englishName = englishName;
		}
		
		/// <summary>
		///Load the channel list form a json string 
		/// </summary>
		/// <returns>
		/// IList<DoubanFMChannel>
		/// </returns>
		/// <param name='json'>
		/// Json string
		/// </param>
		public static IList<DoubanFMChannel> FromJsonString (string json)
		{
			IList<DoubanFMChannel> channels = new List<DoubanFMChannel> ();
			Deserializer deserializer = new Deserializer (json);
			JsonObject obj = (JsonObject)deserializer.Deserialize ();
			JsonArray arr = (JsonArray)obj ["channels"];
			foreach (JsonObject c in arr) {
				string name = (string)c ["name"];
				int id = (int)c ["channel_id"];
				string english_name = (string)c ["name_en"];
				DoubanFMChannel channel = new DoubanFMChannel (name, id.ToString (), english_name);
				//set the personal channel
				if (channel.id == DoubanFMChannel.PersonalChannelId) {
					DoubanFMChannel.PersonalChannel = channel;
				}
				channels.Add (channel);
			}
			return channels;
		}
		
		#region overload the operator == and !==
		public static bool operator == (DoubanFMChannel src, DoubanFMChannel dst)
		{ 
			if (Object.ReferenceEquals (src, null)) {
				if (Object.ReferenceEquals (dst, null))
					return true;
				else
					return false;
			}
			return src.Equals (dst);
		}
		
		public static bool operator != (DoubanFMChannel src, DoubanFMChannel dst)
		{
			return !(src == dst);
		}
		
		public override bool Equals (object obj)
		{
			return this.Equal (obj as DoubanFMChannel);
		}
		
		public override int GetHashCode ()
		{
			return base.GetHashCode ();
		}
		
		public override string ToString ()
		{
			return string.Format ("[DoubanFMChannel: name={0}, id={1}, englishName={2}]", name, id, englishName);
		}

		private bool Equal (DoubanFMChannel channel)
		{
			if (Object.ReferenceEquals (channel, null))
				return false;
			if (Object.ReferenceEquals (this, channel))
				return true;
			return this.id == channel.id;
		}
		#endregion
	}
}
