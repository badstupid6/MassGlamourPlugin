using System;
using System.Collections.Generic;

namespace MassGlamour;

public enum PlayerTarget
{
    All,
    ActivePlayerOnly,
    NonActivePlayersOnly,
}

public class Profile
{
    public string Name { get; set; } = "New Profile";
    public bool Enabled { get; set; } = false;

    public Guid GlamourerDesignId { get; set; } = Guid.Empty;
    public string GlamourerDesignName { get; set; } = string.Empty;

    public Guid CustomizeProfileId { get; set; } = Guid.Empty;
    public string CustomizeProfileName { get; set; } = string.Empty;

    public Guid PenumbraCollectionId { get; set; } = Guid.Empty;
    public string PenumbraCollectionName { get; set; } = string.Empty;

    // Filters
    public bool ApplyToPlayer { get; set; } = true;
    public bool ApplyToNpc { get; set; } = false;
    public PlayerTarget PlayerTarget { get; set; } = PlayerTarget.All;

    public List<uint> ValidRaces { get; set; } = new();
    public List<byte> ValidGenders { get; set; } = new(); // 0 = Male, 1 = Female
    public List<uint> ValidJobs { get; set; } = new();
}