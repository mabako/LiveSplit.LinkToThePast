namespace LiveSplit.LinkToThePast
{
    /// <summary>
    /// Currently active "module"
    /// </summary>
    enum GameModule : byte
    {
        Intro = 0x00,
        SelectFile = 0x01,
        CopyFile = 0x02,
        EraseFile = 0x03,
        NamePlayer = 0x04,
        LoadFile = 0x05,
        PreDungeon = 0x06,
        Dungeon = 0x07,
        PreOverworld = 0x08,
        Overworld = 0x09,
        PreOverworld2 = 0x0A,
        Overworld2 = 0x0B, // e.g. Master Sword grove
        Unknown0 = 0x0C,
        Unknown1 = 0x0D,
        Messaging = 0x0E, // text mode/item screen/map
        CloseSpotlight = 0x0F, // Screen transition
        OpenSpotlight = 0x10, // Screen transition
        HoleToDungeon = 0x11, // falling into an overworld hole into any room
        Death = 0x12,
        GanonVictory = 0x13, // Victory in LW dungeons (?)
        Attract = 0x14, //?
        MagicMirror = 0x15,
        Victory = 0x16, // Victory in DW dungeons (?)
        SaveAndQuit = 0x17,
        GanonEmerges = 0x18, // Found Batman
        TriforceRoom = 0x19,
        EndSequence = 0x1A, // credits
        LocationMenu = 0x1B // select from your house/sanctuary/death mountain
    }
}
