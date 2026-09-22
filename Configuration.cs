using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace MassGlamour;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public List<Profile> Profiles { get; set; } = new();

    public void Save()
    {
        Service.PluginInterface.SavePluginConfig(this);
    }
}