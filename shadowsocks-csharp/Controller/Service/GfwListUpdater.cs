﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

using Newtonsoft.Json;

using Shadowsocks.Model;
using Shadowsocks.Properties;
using Shadowsocks.Util;

namespace Shadowsocks.Controller
{
    public class GFWListUpdater
    {
        private const string GFWLIST_URL = "https://raw.githubusercontent.com/gfwlist/gfwlist/master/gfwlist.txt";

        public event EventHandler<ResultEventArgs> UpdateCompleted;

        public event ErrorEventHandler Error;

        public class ResultEventArgs : EventArgs
        {
            public bool Success;

            public ResultEventArgs(bool success)
            {
                Success = success;
            }
        }

        private static readonly IEnumerable<char> IgnoredLineBegins = new[] { '!', '[' };
        private void http_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                File.WriteAllText(Utils.GetTempPath("gfwlist.txt"), e.Result, Encoding.UTF8);
                List<string> lines = ParseResult(e.Result);
                if (File.Exists(PACServer.USER_RULE_FILE))
                {
                    string local = File.ReadAllText(PACServer.USER_RULE_FILE, Encoding.UTF8);
                    using (var sr = new StringReader(local))
                    {
                        lines.AddRange(sr.NonWhiteSpaceLines().Where(rule => !rule.BeginWithAny(IgnoredLineBegins)));
                    }
                }
                string abpContent;
                if (File.Exists(PACServer.USER_ABP_FILE))
                {
                    abpContent = File.ReadAllText(PACServer.USER_ABP_FILE, Encoding.UTF8);
                }
                else
                {
                    abpContent = Utils.UnGzip(Resources.abp_js);
                }
                abpContent = abpContent.Replace("__RULES__", JsonConvert.SerializeObject(lines, Formatting.Indented));
                if (File.Exists(PACServer.PAC_FILE))
                {
                    string original = File.ReadAllText(PACServer.PAC_FILE, Encoding.UTF8);
                    if (original == abpContent)
                    {
                        UpdateCompleted?.Invoke(this, new ResultEventArgs(false));
                        return;
                    }
                }
                File.WriteAllText(PACServer.PAC_FILE, abpContent, Encoding.UTF8);
                UpdateCompleted?.Invoke(this, new ResultEventArgs(true));
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex));
            }
        }

        public void UpdatePACFromGFWList(Configuration config)
        {
            WebClient http = new WebClient();
            http.Proxy = new WebProxy(IPAddress.Loopback.ToString(), config.localPort);
            http.DownloadStringCompleted += http_DownloadStringCompleted;
            http.DownloadStringAsync(new Uri(GFWLIST_URL));
        }

        public static List<string> ParseResult(string response)
        {
            byte[] bytes = Convert.FromBase64String(response);
            string content = Encoding.ASCII.GetString(bytes);
            List<string> valid_lines = new List<string>();
            using (var sr = new StringReader(content))
            {
                valid_lines.AddRange(sr.NonWhiteSpaceLines().Where(line => !line.BeginWithAny(IgnoredLineBegins)));
            }
            return valid_lines;
        }
    }
}
