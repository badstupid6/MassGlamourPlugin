using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace MassGlamour;

public class PluginUI : IDisposable
{
    private readonly Configuration _configuration;
    private readonly Plugin _plugin;
    private bool _settingsVisible = false;

    private List<(Guid Id, string Name)> _glamourerDesigns = new();
    private List<(Guid Id, string Name)> _customizeProfiles = new();
    private List<(Guid Id, string Name)> _penumbraCollections = new();

    private Dictionary<uint, string> _raceCache = new();
    private Dictionary<uint, string> _jobCache = new();

    public bool SettingsVisible
    {
        get => _settingsVisible;
        set => _settingsVisible = value;
    }

    public PluginUI(Configuration configuration, Plugin plugin)
    {
        _configuration = configuration;
        _plugin = plugin;

        // Cache EXD data for UI
        var races = Service.DataManager.GetExcelSheet<Race>();
        if (races != null)
        {
            foreach (var race in races)
            {
                if (!string.IsNullOrEmpty(race.Masculine.ToString()))
                    _raceCache[race.RowId] = race.Masculine.ToString();
            }
        }

        var jobs = Service.DataManager.GetExcelSheet<ClassJob>();
        if (jobs != null)
        {
            foreach (var job in jobs)
            {
                if (!string.IsNullOrEmpty(job.Name.ToString()))
                    _jobCache[job.RowId] = job.Name.ToString();
            }
        }
    }

    public void RefreshIpcData()
    {
        _glamourerDesigns = IpcManager.GetGlamourerDesigns();
        _customizeProfiles = IpcManager.GetCustomizeProfiles();
        _penumbraCollections = IpcManager.GetPenumbraCollections();
        Service.PluginLog.Information($"MassGlamour: UI refresh completed. Glamourer designs: {_glamourerDesigns.Count}; Customize+ profiles: {_customizeProfiles.Count}; Penumbra collections: {_penumbraCollections.Count}.");
    }

    public void Draw()
    {
        if (!_settingsVisible) return;

        ImGui.SetNextWindowSize(new Vector2(600, 500), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("MassGlamour Configuration", ref _settingsVisible))
        {
            bool masterEnabled = _configuration.Enabled;
            if (ImGui.Checkbox("Enable MassGlamour", ref masterEnabled))
            {
                _plugin.SetEnabled(masterEnabled);
            }

            if (ImGui.Button("Refresh Glamourer/Customize+ Lists"))
            {
                RefreshIpcData();
            }

            ImGui.SameLine();
            if (ImGui.Button("Apply to All Current Characters"))
            {
                _plugin.ApplyToAllCurrent();
            }

            ImGui.SameLine();
            if (ImGui.Button("Reset All Current Characters"))
            {
                _plugin.ResetAllCurrent();
            }

            ImGui.Separator();
            ImGui.Text($"Loaded designs: {_glamourerDesigns.Count} | Loaded Customize+ profiles: {_customizeProfiles.Count} | Loaded Penumbra collections: {_penumbraCollections.Count}");

            if (ImGui.Button("Add New Profile"))
            {
                _configuration.Profiles.Add(new Profile());
                _configuration.Save();
            }

            ImGui.BeginChild("ProfilesList");
            for (int i = 0; i < _configuration.Profiles.Count; i++)
            {
                var profile = _configuration.Profiles[i];
                string profileDisplayName = profile.Enabled ? $"* {profile.Name}" : profile.Name;
                if (ImGui.CollapsingHeader($"{profileDisplayName}###profile_{i}"))
                {
                    bool enabled = profile.Enabled;
                    if (ImGui.Checkbox($"Enabled###enable_{i}", ref enabled))
                    {
                        profile.Enabled = enabled;
                        _configuration.Save();
                    }

                    ImGui.SameLine();
                    ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 100);
                    if (ImGui.Button($"Delete###del_{i}"))
                    {
                        _configuration.Profiles.RemoveAt(i);
                        _configuration.Save();
                        i--;
                        continue;
                    }

                    string name = profile.Name;
                    if (ImGui.InputText($"Profile Name###name_{i}", ref name, 64))
                    {
                        profile.Name = name;
                        _configuration.Save();
                    }

                    // Properly handled ref properties using local variables
                    Guid glamId = profile.GlamourerDesignId;
                    string glamName = profile.GlamourerDesignName;
                    DrawDropdown("Glamourer Design", _glamourerDesigns, ref glamId, ref glamName, i);
                    if (glamId != profile.GlamourerDesignId || glamName != profile.GlamourerDesignName)
                    {
                        profile.GlamourerDesignId = glamId;
                        profile.GlamourerDesignName = glamName;
                        _configuration.Save();
                    }

                    Guid custId = profile.CustomizeProfileId;
                    string custName = profile.CustomizeProfileName;
                    DrawDropdown("Customize+ Profile", _customizeProfiles, ref custId, ref custName, i);
                    if (custId != profile.CustomizeProfileId || custName != profile.CustomizeProfileName)
                    {
                        profile.CustomizeProfileId = custId;
                        profile.CustomizeProfileName = custName;
                        _configuration.Save();
                    }

                    Guid collectionId = profile.PenumbraCollectionId;
                    string collectionName = profile.PenumbraCollectionName;
                    DrawDropdown("Penumbra Collection", _penumbraCollections, ref collectionId, ref collectionName, i);
                    if (collectionId != profile.PenumbraCollectionId || collectionName != profile.PenumbraCollectionName)
                    {
                        profile.PenumbraCollectionId = collectionId;
                        profile.PenumbraCollectionName = collectionName;
                        _configuration.Save();
                    }

                    ImGui.Text("Filters (Leave lists empty to allow all):");

                    bool toPlayer = profile.ApplyToPlayer;
                    bool toNpc = profile.ApplyToNpc;
                    if (ImGui.Checkbox($"Apply to Players###player_{i}", ref toPlayer)) { profile.ApplyToPlayer = toPlayer; _configuration.Save(); }
                    ImGui.SameLine();
                    if (ImGui.Checkbox($"Apply to NPCs###npc_{i}", ref toNpc)) { profile.ApplyToNpc = toNpc; _configuration.Save(); }

                    string playerTargetLabel = profile.PlayerTarget switch
                    {
                        PlayerTarget.ActivePlayerOnly => "Active player only",
                        PlayerTarget.NonActivePlayersOnly => "Non-active players only",
                        _ => "All characters",
                    };
                    if (ImGui.BeginCombo($"Player target###player_target_{i}", playerTargetLabel))
                    {
                        foreach (var target in Enum.GetValues<PlayerTarget>())
                        {
                            string targetLabel = target switch
                            {
                                PlayerTarget.ActivePlayerOnly => "Active player only",
                                PlayerTarget.NonActivePlayersOnly => "Non-active players only",
                                _ => "All characters",
                            };
                            if (ImGui.Selectable(targetLabel, profile.PlayerTarget == target))
                            {
                                profile.PlayerTarget = target;
                                _configuration.Save();
                            }
                        }
                        ImGui.EndCombo();
                    }

                    if (ImGui.TreeNode($"Race Filters ({profile.ValidRaces.Count} selected)###races_{i}"))
                    {
                        foreach (var kvp in _raceCache)
                        {
                            bool selected = profile.ValidRaces.Contains(kvp.Key);
                            if (ImGui.Checkbox($"{kvp.Value}###race_{i}_{kvp.Key}", ref selected))
                            {
                                if (selected) profile.ValidRaces.Add(kvp.Key);
                                else profile.ValidRaces.Remove(kvp.Key);
                                _configuration.Save();
                            }
                        }
                        ImGui.TreePop();
                    }

                    if (ImGui.TreeNode($"Gender Filters ({profile.ValidGenders.Count} selected)###genders_{i}"))
                    {
                        bool male = profile.ValidGenders.Contains(0);
                        bool female = profile.ValidGenders.Contains(1);

                        if (ImGui.Checkbox($"Male###male_{i}", ref male)) { if (male) profile.ValidGenders.Add(0); else profile.ValidGenders.Remove(0); _configuration.Save(); }
                        ImGui.SameLine();
                        if (ImGui.Checkbox($"Female###female_{i}", ref female)) { if (female) profile.ValidGenders.Add(1); else profile.ValidGenders.Remove(1); _configuration.Save(); }
                        ImGui.TreePop();
                    }

                    if (ImGui.TreeNode($"Job Filters ({profile.ValidJobs.Count} selected)###jobs_{i}"))
                    {
                        foreach (var kvp in _jobCache)
                        {
                            bool selected = profile.ValidJobs.Contains(kvp.Key);
                            if (ImGui.Checkbox($"{kvp.Value}###job_{i}_{kvp.Key}", ref selected))
                            {
                                if (selected) profile.ValidJobs.Add(kvp.Key);
                                else profile.ValidJobs.Remove(kvp.Key);
                                _configuration.Save();
                            }
                        }
                        ImGui.TreePop();
                    }
                }
            }
            ImGui.EndChild();
        }
        ImGui.End();
    }

    private void DrawDropdown(string label, List<(Guid Id, string Name)> items, ref Guid selectedId, ref string selectedName, int index)
    {
        string display = string.IsNullOrEmpty(selectedName) ? "None" : selectedName;
        if (ImGui.BeginCombo($"{label}###{label}_{index}", display))
        {
            if (ImGui.Selectable("None", selectedId == Guid.Empty))
            {
                selectedId = Guid.Empty;
                selectedName = string.Empty;
            }

            foreach (var item in items)
            {
                if (ImGui.Selectable(item.Name, selectedId == item.Id))
                {
                    selectedId = item.Id;
                    selectedName = item.Name;
                }
            }
            ImGui.EndCombo();
        }
    }

    public void Dispose() { }
}