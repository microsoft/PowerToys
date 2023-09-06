// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EnvironmentVariables.Models
{
    public class VariablesSet
    {
        public static readonly Guid UserGuid = new Guid("92F7AA9A-AE31-49CD-83C8-80A71E432AA5");
        public static readonly Guid SystemGuid = new Guid("F679C74D-DB00-4795-92E1-B1F6A4833279");

        private static readonly string UserIconPath = "/Assets/EnvironmentVariables/UserIcon.png";
        private static readonly string SystemIconPath = "/Assets/EnvironmentVariables/SystemIcon.png";
        private static readonly string ProfileIconPath = "/Assets/EnvironmentVariables/ProfileIcon.png";

        public Guid Id { get; }

        public string Name { get; }

        public VariablesSetType Type { get; }

        public string IconPath { get; }

        public List<Variable> Variables { get; }

        public VariablesSet(Guid id, string name, VariablesSetType type)
        {
            Id = id;
            Name = name;
            Type = type;
            Variables = new List<Variable>();

            IconPath = Type switch
            {
                VariablesSetType.User => UserIconPath,
                VariablesSetType.System => SystemIconPath,
                VariablesSetType.Profile => ProfileIconPath,
                _ => throw new NotImplementedException(),
            };
        }
    }
}
