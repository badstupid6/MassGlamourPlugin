using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Ipc;

namespace MassGlamour;

public static class IpcManager
{
    private static ICallGateSubscriber<Dictionary<Guid, string>>? _getGlamourerDesigns;
    private static ICallGateSubscriber<Guid, int, uint, ulong, int>? _applyGlamourerDesign;
    private static ICallGateSubscriber<int, uint, ulong, int>? _revertGlamourerState;
    private static ICallGateSubscriber<Dictionary<Guid, string>>? _getPenumbraCollections;
    private static ICallGateSubscriber<int, Guid?, bool, bool, (int Result, (Guid Id, string Name)? OldCollection)>? _setPenumbraCollection;
    private static ICallGateSubscriber<int, int, object>? _redrawPenumbraObject;

    private static ICallGateSubscriber<IList<(Guid UniqueId, string Name, string VirtualPath, List<(string Name, ushort WorldId, byte CharacterType, ushort CharacterSubType)> Characters, int Priority, bool IsEnabled)>>? _getCustomizeProfiles;
    private static ICallGateSubscriber<Guid, (int Result, string? ProfileJson)>? _getCustomizeProfile;
    private static ICallGateSubscriber<ushort, string, (int Result, Guid? ProfileId)>? _applyCustomizeProfile;
    private static ICallGateSubscriber<ushort, int>? _resetCustomizeProfile;

    private static bool _initialized = false;

    public static void Initialize()
    {
        if (_initialized) return;

        try
        {
            _getGlamourerDesigns = Service.PluginInterface.GetIpcSubscriber<Dictionary<Guid, string>>("Glamourer.GetDesignList.V2");
            _applyGlamourerDesign = Service.PluginInterface.GetIpcSubscriber<Guid, int, uint, ulong, int>("Glamourer.ApplyDesign");
            _revertGlamourerState = Service.PluginInterface.GetIpcSubscriber<int, uint, ulong, int>("Glamourer.RevertToAutomation.V2");
            Service.PluginLog.Information("MassGlamour: Glamourer IPC subscribers registered.");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "MassGlamour: Failed to register Glamourer IPC subscribers.");
        }

        try
        {
            _getPenumbraCollections = Service.PluginInterface.GetIpcSubscriber<Dictionary<Guid, string>>("Penumbra.GetCollections.V5");
            _setPenumbraCollection = Service.PluginInterface.GetIpcSubscriber<int, Guid?, bool, bool, (int Result, (Guid Id, string Name)? OldCollection)>("Penumbra.SetCollectionForObject.V5");
            _redrawPenumbraObject = Service.PluginInterface.GetIpcSubscriber<int, int, object>("Penumbra.RedrawObject.V5");
            Service.PluginLog.Information("MassGlamour: Penumbra IPC subscribers registered.");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "MassGlamour: Failed to register Penumbra IPC subscribers.");
        }

        try
        {
            _getCustomizeProfiles = Service.PluginInterface.GetIpcSubscriber<IList<(Guid UniqueId, string Name, string VirtualPath, List<(string Name, ushort WorldId, byte CharacterType, ushort CharacterSubType)> Characters, int Priority, bool IsEnabled)>>("CustomizePlus.Profile.GetList");
            _getCustomizeProfile = Service.PluginInterface.GetIpcSubscriber<Guid, (int Result, string? ProfileJson)>("CustomizePlus.Profile.GetByUniqueId");
            _applyCustomizeProfile = Service.PluginInterface.GetIpcSubscriber<ushort, string, (int Result, Guid? ProfileId)>("CustomizePlus.Profile.SetTemporaryProfileOnCharacter");
            _resetCustomizeProfile = Service.PluginInterface.GetIpcSubscriber<ushort, int>("CustomizePlus.Profile.DeleteTemporaryProfileOnCharacter");
            Service.PluginLog.Information("MassGlamour: Customize+ IPC subscribers registered.");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "MassGlamour: Failed to register Customize+ IPC subscribers.");
        }

        _initialized = true;
    }

    public static List<(Guid Id, string Name)> GetPenumbraCollections()
    {
        var list = new List<(Guid, string)>();
        try
        {
            _getPenumbraCollections ??= Service.PluginInterface.GetIpcSubscriber<Dictionary<Guid, string>>("Penumbra.GetCollections.V5");
            var collections = _getPenumbraCollections.InvokeFunc();
            if (collections != null)
                list.AddRange(collections.Select(x => (x.Key, x.Value)));

            Service.PluginLog.Information($"MassGlamour: Penumbra returned {list.Count} collections.");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "MassGlamour: Could not fetch Penumbra collections.");
        }

        return list;
    }

    public static void SetPenumbraCollection(Guid? collectionId, int objectIndex)
    {
        try
        {
            _setPenumbraCollection ??= Service.PluginInterface.GetIpcSubscriber<int, Guid?, bool, bool, (int Result, (Guid Id, string Name)? OldCollection)>("Penumbra.SetCollectionForObject.V5");
            var result = _setPenumbraCollection.InvokeFunc(objectIndex, collectionId, true, true);
            Service.PluginLog.Information($"MassGlamour: Penumbra collection set returned {result.Result} for object {objectIndex}.");

            if (result.Result == 0)
            {
                _redrawPenumbraObject ??= Service.PluginInterface.GetIpcSubscriber<int, int, object>("Penumbra.RedrawObject.V5");
                _redrawPenumbraObject.InvokeAction(objectIndex, 0);
                Service.PluginLog.Information($"MassGlamour: Penumbra redraw requested for object {objectIndex}.");
            }
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "MassGlamour: Could not set Penumbra collection.");
        }
    }

    public static void ClearPenumbraCollection(int objectIndex)
    {
        SetPenumbraCollection(null, objectIndex);
    }

    public static void ResetGlamourerState(int objectIndex)
    {
        try
        {
            _revertGlamourerState ??= Service.PluginInterface.GetIpcSubscriber<int, uint, ulong, int>("Glamourer.RevertToAutomation.V2");
            var result = _revertGlamourerState.InvokeFunc(objectIndex, 0, 6);
            Service.PluginLog.Information($"MassGlamour: Glamourer reset returned {result} for object {objectIndex}.");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "MassGlamour: Could not reset Glamourer state.");
        }
    }

    public static List<(Guid Id, string Name)> GetGlamourerDesigns()
    {
        // Re-initialize check in case plugins loaded after startup
        if (_getGlamourerDesigns == null)
        {
            try
            {
                _getGlamourerDesigns = Service.PluginInterface.GetIpcSubscriber<Dictionary<Guid, string>>("Glamourer.GetDesignList.V2");
                Service.PluginLog.Information("MassGlamour: Glamourer IPC subscriber became available.");
            }
            catch (Exception ex)
            {
                Service.PluginLog.Warning(ex, "MassGlamour: Glamourer IPC subscriber is unavailable.");
            }
        }

        var list = new List<(Guid, string)>();
        try
        {
            if (_getGlamourerDesigns != null)
            {
                var designs = _getGlamourerDesigns.InvokeFunc();
                if (designs != null)
                {
                    list.AddRange(designs.Select(d => (d.Key, d.Value)));
                }
            }
            Service.PluginLog.Information($"MassGlamour: Glamourer returned {list.Count} designs.");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "MassGlamour: Could not fetch Glamourer designs.");
        }
        return list;
    }

    public static void ApplyGlamourerDesign(Guid designId, int objectIndex)
    {
        if (_applyGlamourerDesign == null)
        {
            try
            {
                _applyGlamourerDesign = Service.PluginInterface.GetIpcSubscriber<Guid, int, uint, ulong, int>("Glamourer.ApplyDesign");
            }
            catch { /* Ignore */ }
        }

        try
        {
            var result = _applyGlamourerDesign?.InvokeFunc(designId, objectIndex, 0, 7);
            Service.PluginLog.Information($"MassGlamour: Glamourer apply returned {result} for {designId} on object {objectIndex}.");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "MassGlamour: Could not apply Glamourer design.");
        }
    }

    public static List<(Guid Id, string Name)> GetCustomizeProfiles()
    {
        if (_getCustomizeProfiles == null)
        {
            try
            {
                _getCustomizeProfiles = Service.PluginInterface.GetIpcSubscriber<IList<(Guid UniqueId, string Name, string VirtualPath, List<(string Name, ushort WorldId, byte CharacterType, ushort CharacterSubType)> Characters, int Priority, bool IsEnabled)>>("CustomizePlus.Profile.GetList");
            }
            catch (Exception ex)
            {
                Service.PluginLog.Warning(ex, "MassGlamour: Customize+ IPC subscriber is unavailable.");
            }
        }

        var list = new List<(Guid, string)>();
        try
        {
            if (_getCustomizeProfiles != null)
            {
                var profiles = _getCustomizeProfiles.InvokeFunc();
                if (profiles != null)
                {
                    foreach (var p in profiles)
                    {
                        list.Add((p.Item1, p.Item2));
                    }
                }
            }
            Service.PluginLog.Information($"MassGlamour: Customize+ returned {list.Count} profiles.");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "MassGlamour: Could not fetch Customize+ profiles.");
        }
        return list;
    }

    public static void ApplyCustomizeProfile(Guid profileId, int objectIndex)
    {
        try
        {
            _getCustomizeProfile ??= Service.PluginInterface.GetIpcSubscriber<Guid, (int Result, string? ProfileJson)>("CustomizePlus.Profile.GetByUniqueId");
            _applyCustomizeProfile ??= Service.PluginInterface.GetIpcSubscriber<ushort, string, (int Result, Guid? ProfileId)>("CustomizePlus.Profile.SetTemporaryProfileOnCharacter");

            var profile = _getCustomizeProfile.InvokeFunc(profileId);
            if (profile.Result != 0 || string.IsNullOrEmpty(profile.ProfileJson))
            {
                Service.PluginLog.Error($"MassGlamour: Customize+ could not retrieve profile {profileId}; result {profile.Result}.");
                return;
            }

            var result = _applyCustomizeProfile.InvokeFunc((ushort)objectIndex, profile.ProfileJson);
            Service.PluginLog.Information($"MassGlamour: Customize+ temporary profile returned {result.Result} for {profileId} on object {objectIndex}.");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "MassGlamour: Could not apply Customize+ temporary profile.");
        }
    }

    public static void ResetCustomizeProfile(int objectIndex)
    {
        try
        {
            _resetCustomizeProfile ??= Service.PluginInterface.GetIpcSubscriber<ushort, int>("CustomizePlus.Profile.DeleteTemporaryProfileOnCharacter");
            var result = _resetCustomizeProfile.InvokeFunc((ushort)objectIndex);
            Service.PluginLog.Information($"MassGlamour: Customize+ reset returned {result} for object {objectIndex}.");
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "MassGlamour: Could not reset Customize+ profile.");
        }
    }
}