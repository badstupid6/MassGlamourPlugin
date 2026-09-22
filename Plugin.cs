using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;

namespace MassGlamour;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "MassGlamour";
    private const string CommandName = "/mglam";

    private readonly Configuration _configuration;
    private readonly PluginUI _ui;

    // We track pointers to know when a character has been drawn/rendered for the first time
    private readonly HashSet<nint> _seenPointers = new();

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();

        _configuration = Service.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        IpcManager.Initialize();

        _ui = new PluginUI(_configuration, this);

        Service.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the MassGlamour configuration window."
        });

        Service.PluginInterface.UiBuilder.Draw += DrawUI;
        Service.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        Service.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Service.Framework.Update -= OnFrameworkUpdate;
        Service.PluginInterface.UiBuilder.Draw -= DrawUI;
        Service.PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
        Service.CommandManager.RemoveHandler(CommandName);
        _ui.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        _ui.SettingsVisible = !_ui.SettingsVisible;
        if (_ui.SettingsVisible)
        {
            _ui.RefreshIpcData();
        }
    }

    public void ResetAllCurrent()
    {
        foreach (var obj in Service.ObjectTable)
        {
            if (obj is not ICharacter)
                continue;

            IpcManager.ResetGlamourerState(obj.ObjectIndex);
            IpcManager.ResetCustomizeProfile(obj.ObjectIndex);
            IpcManager.SetPenumbraCollection(null, obj.ObjectIndex);
        }

        Service.PluginLog.Information("MassGlamour: Reset requested for all currently loaded characters.");
    }

    public void SetEnabled(bool enabled)
    {
        if (_configuration.Enabled == enabled)
            return;

        _configuration.Enabled = enabled;
        _configuration.Save();

        if (enabled)
        {
            _ui.RefreshIpcData();
            ApplyToAllCurrent();
            Service.PluginLog.Information("MassGlamour: Master switch enabled.");
        }
        else
        {
            ResetAllCurrent();
            Service.PluginLog.Information("MassGlamour: Master switch disabled.");
        }
    }

    private void DrawUI() => _ui.Draw();
    private void DrawConfigUI() => OnCommand(string.Empty, string.Empty);

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!_configuration.Enabled)
            return;

        HashSet<nint> currentPointers = new();

        foreach (var obj in Service.ObjectTable)
        {
            if (obj == null || obj.Address == nint.Zero) continue;

            currentPointers.Add(obj.Address);

            // If we haven't seen this object pointer yet, it just loaded/drew in.
            if (!_seenPointers.Contains(obj.Address))
            {
                _seenPointers.Add(obj.Address);
                ApplyProfilesToObject(obj);
            }
        }

        // Intersect prevents memory leaking by removing pointers of characters that left render distance
        _seenPointers.IntersectWith(currentPointers);
    }

    public void ApplyToAllCurrent()
    {
        if (!_configuration.Enabled)
            return;

        foreach (var obj in Service.ObjectTable)
        {
            if (obj != null)
            {
                ApplyProfilesToObject(obj);
            }
        }
    }

    private void ApplyProfilesToObject(IGameObject obj)
    {
        // We only care about characters (Players, NPCs) that have customization data
        if (obj is not ICharacter character) return;

        foreach (var profile in _configuration.Profiles)
        {
            if (!profile.Enabled) continue;

            if (MatchesFilters(character, profile))
            {
                if (profile.GlamourerDesignId != Guid.Empty)
                {
                    IpcManager.ApplyGlamourerDesign(profile.GlamourerDesignId, obj.ObjectIndex);
                }

                if (profile.PenumbraCollectionId != Guid.Empty)
                {
                    IpcManager.SetPenumbraCollection(profile.PenumbraCollectionId, obj.ObjectIndex);
                }

                if (profile.CustomizeProfileId != Guid.Empty)
                {
                    IpcManager.ApplyCustomizeProfile(profile.CustomizeProfileId, obj.ObjectIndex);
                }
            }
        }
    }

    private bool MatchesFilters(ICharacter character, Profile profile)
    {
        bool isPlayer = character is IPlayerCharacter;
        bool isNpc = !isPlayer; // If it's a character but not a player, it's an NPC.

        if (profile.PlayerTarget != PlayerTarget.All)
        {
            var localPlayer = Service.ObjectTable.LocalPlayer;
            bool isActivePlayer = localPlayer != null && character.Address == localPlayer.Address;

            if (profile.PlayerTarget == PlayerTarget.ActivePlayerOnly && !isActivePlayer)
                return false;

            if (profile.PlayerTarget == PlayerTarget.NonActivePlayersOnly && (!isPlayer || isActivePlayer))
                return false;
        }

        if (!profile.ApplyToPlayer && isPlayer) return false;
        if (!profile.ApplyToNpc && isNpc) return false;

        // Customize[0] is Race, Customize[1] is Gender (0=Male, 1=Female)
        if (profile.ValidRaces.Count > 0 && !profile.ValidRaces.Contains(character.Customize[0])) return false;
        if (profile.ValidGenders.Count > 0 && !profile.ValidGenders.Contains(character.Customize[1])) return false;

        // Filter jobs
        if (profile.ValidJobs.Count > 0 && !profile.ValidJobs.Contains(character.ClassJob.RowId)) return false;

        return true;
    }
}