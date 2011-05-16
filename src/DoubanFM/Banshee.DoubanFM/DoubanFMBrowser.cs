//
// DoubanFMBrowser.cs
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
using System.Web;
using System.Security.Cryptography.X509Certificates;

namespace Banshee.DoubanFM
{
    public class DoubanFMBrowser
    {
        private CookieContainer cookieJar;

        public DoubanFMBrowser() {
            cookieJar = new CookieContainer();
            // workaround for invalid certificate problem, override certificate validator
            ServicePointManager.ServerCertificateValidationCallback = Validator;
        }

        /// <summary>
        /// workaround for invalid certificate problem
        /// </summary>
        public static bool Validator (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
            return true;
        }

        public HttpWebResponse Get(string baseUrl, NameValueCollection parameters, Encoding reqencode, Encoding resencode) {
            StringBuilder parassb = new StringBuilder();
            foreach (string key in parameters.Keys)
            {
                if (parassb.Length > 0)
                    parassb.Append("&");
                parassb.AppendFormat("{0}={1}", HttpUtility.UrlEncode(key, reqencode), HttpUtility.UrlEncode(parameters[key], reqencode));
            }

            if (parassb.Length > 0)
            {
                baseUrl += "?" + parassb;
            }
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(baseUrl);
            req.CookieContainer = cookieJar;
            req.Method = "GET";
            req.MaximumAutomaticRedirections = 3;
            req.Timeout = 5000;

//            string result = String.Empty;
//            using (StreamReader reader = new StreamReader(req.GetResponse().GetResponseStream(), resencode))
//            {
//                result = reader.ReadToEnd();
//            }
//            return result;
            return (HttpWebResponse)req.GetResponse();
        }

        public HttpWebResponse Post(string url, NameValueCollection parameters, Encoding reqencode, Encoding resencode)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";
            req.CookieContainer = cookieJar;
    
            StringBuilder parassb = new StringBuilder();
            foreach (string key in parameters.Keys)
            {
                if (parassb.Length > 0)
                    parassb.Append("&");
                parassb.AppendFormat("{0}={1}", key, parameters[key]);
            }
            byte[] data = reqencode.GetBytes(parassb.ToString());
            req.ContentLength = data.Length;

            Stream reqstream = req.GetRequestStream();
            reqstream.Write(data, 0, data.Length);
            reqstream.Close();
//            string result = String.Empty;
//            using (StreamReader reader = new StreamReader(req.GetResponse().GetResponseStream(), resencode))
//            {
//                result = reader.ReadToEnd();
//            }
//            return result;
            return (HttpWebResponse)req.GetResponse();
        }

    }
}
