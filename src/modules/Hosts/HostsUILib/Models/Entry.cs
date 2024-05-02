// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using HostsUILib.Helpers;

namespace HostsUILib.Models
{
    public partial class Entry : ObservableObject
    {
        private static readonly char[] _spaceCharacters = new char[] { ' ', '\t' };

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Valid))]
        [NotifyPropertyChangedFor(nameof(IsAddressValid))]
        private string _address;

        partial void OnAddressChanged(string value)
        {
            if (ValidationHelper.ValidIPv4(value))
            {
                Type = AddressType.IPv4;
            }
            else if (ValidationHelper.ValidIPv6(value))
            {
                Type = AddressType.IPv6;
            }
            else
            {
                Type = AddressType.Invalid;
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Valid))]
        [NotifyPropertyChangedFor(nameof(IsHostsValid))]
        private string _hosts;

        partial void OnHostsChanged(string value)
        {
            SplittedHosts = value.Split(' ');
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Valid))]
        private string _comment;

        [ObservableProperty]
        private bool _active;

        [ObservableProperty]
        private bool? _ping;

        [ObservableProperty]
        private bool _pinging;

        [ObservableProperty]
        private bool _duplicate;

        public bool Valid => Validate(true);

        public bool IsAddressValid => ValidateAddressField();

        public bool IsHostsValid => ValidateHostsField(true);

        public string Line { get; private set; }

        public AddressType Type { get; private set; }

        public string[] SplittedHosts { get; private set; }

        public int Id { get; set; }

        public Entry()
        {
        }

        public Entry(int id, string line)
        {
            Id = id;
            Line = line.Trim();
            Parse();
        }

        public Entry(int id, string address, string hosts, string comment, bool active)
        {
            Id = id;
            Address = address.Trim();
            Hosts = hosts.Trim();
            Comment = comment.Trim();
            Active = active;
        }

        public void Parse()
        {
            Active = !Line.StartsWith("#", StringComparison.InvariantCultureIgnoreCase);

            var lineSplit = Line.TrimStart(' ', '#').Split('#');

            if (lineSplit.Length == 0)
            {
                return;
            }

            var addressHost = lineSplit[0];

            var addressHostSplit = addressHost.Split(_spaceCharacters, StringSplitOptions.RemoveEmptyEntries);
            var hostsBuilder = new StringBuilder();
            var commentBuilder = new StringBuilder();

            for (var i = 0; i < addressHostSplit.Length; i++)
            {
                var element = addressHostSplit[i].Trim();

                if (i == 0 && IPAddress.TryParse(element, out var _) && (element.Contains(':') || element.Contains('.')))
                {
                    Address = element;
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
                Line = Line,
                Address = Address,
                Hosts = Hosts,
                Comment = Comment,
                Active = Active,
            };
        }

        private bool ValidateAddressField()
        {
            return Type != AddressType.Invalid;
        }

        private bool ValidateHostsField(bool validateHostsLength)
        {
            return ValidationHelper.ValidHosts(Hosts, validateHostsLength);
        }

        public bool Validate(bool validateHostsLength)
        {
            if (Equals("102.54.94.97", "rhino.acme.com", "source server") || Equals("38.25.63.10", "x.acme.com", "x client host"))
            {
                return false;
            }

            return ValidateAddressField() && ValidateHostsField(validateHostsLength);
        }

        private bool Equals(string address, string hosts, string comment)
        {
            return string.Equals(Address, address, StringComparison.Ordinal)
                && string.Equals(Hosts, hosts, StringComparison.Ordinal)
                && string.Equals(Comment, comment, StringComparison.Ordinal);
        }
    }
}
