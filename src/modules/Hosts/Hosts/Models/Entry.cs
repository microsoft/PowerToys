// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Hosts.Helpers;

namespace Hosts.Models
{
    public partial class Entry : ObservableObject
    {
        private string _line;

        private string _address;

        public string Address
        {
            get => _address;
            set
            {
                SetProperty(ref _address, value);
                OnPropertyChanged(nameof(Valid));
            }
        }

        private string _hosts;

        public string Hosts
        {
            get => _hosts;
            set
            {
                SetProperty(ref _hosts, value);
                OnPropertyChanged(nameof(Valid));
                SplittedHosts = _hosts.Split(' ');
            }
        }

        [ObservableProperty]
        private string _comment;

        [ObservableProperty]
        private bool _active;

        [ObservableProperty]
        private bool? _ping;

        [ObservableProperty]
        private bool _pinging;

        [ObservableProperty]
        private bool _duplicate;

        public bool Valid => ValidationHelper.ValidHosts(_hosts) && (ValidationHelper.ValidIPv4(_address) || ValidationHelper.ValidIPv6(_address));

        public string[] SplittedHosts { get; private set; }

        public Entry()
        {
        }

        public Entry(string line)
        {
            _line = line.Trim();
            Parse();
        }

        public Entry(string address, string hosts, string comment, bool active)
        {
            Address = address.Trim();
            Hosts = hosts.Trim();
            Comment = comment.Trim();
            Active = active;
        }

        public void Parse()
        {
            Active = !_line.StartsWith("#", StringComparison.InvariantCultureIgnoreCase);

            var lineSplit = _line.TrimStart(' ', '#').Split('#');

            if (lineSplit.Length == 0)
            {
                return;
            }

            var addressHost = lineSplit[0];

            var addressHostSplit = addressHost.Split(' ', '\t');
            var hostsBuilder = new StringBuilder();
            var commentBuilder = new StringBuilder();

            for (var i = 0; i < addressHostSplit.Length; i++)
            {
                var element = addressHostSplit[i].Trim();

                if (string.IsNullOrWhiteSpace(element))
                {
                    continue;
                }

                if (Address == null)
                {
                    if (IPAddress.TryParse(element, out var _) && (element.Contains(':') || element.Contains('.')))
                    {
                        Address = element;
                    }
                }
                else
                {
                    if (hostsBuilder.Length > 0)
                    {
                        hostsBuilder.Append(' ');
                    }

                    hostsBuilder.Append(element);
                }
            }

            Hosts = hostsBuilder.ToString();

            for (var i = 1; i < lineSplit.Length; i++)
            {
                if (commentBuilder.Length > 0)
                {
                    commentBuilder.Append('#');
                }

                commentBuilder.Append(lineSplit[i]);
            }

            Comment = commentBuilder.ToString().Trim();
        }

        public Entry Clone()
        {
            return new Entry
            {
                _line = _line,
                Address = Address,
                Hosts = Hosts,
                Comment = Comment,
                Active = Active,
            };
        }

        public string GetLine()
        {
            return _line;
        }
    }
}
