using LiveSplit.ComponentUtil;
using LiveSplit.Model;
using LiveSplit.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace LiveSplit.LinkToThePast
{
    class GameData
    {
        public event EventHandler<StateEventArgs> OnNewGame;
        public event EventHandler<SplitEventArgs> Split;

        public bool IsRandomized;

        #region PPU / Frame count code
        private MemoryWatcher<uint> IPPU_TotalEmulatedFrames;
        private long currentFrames;
        private long savedFrames; // save some for emulator crashes

        public TimeSpan GameTime
        {
            get
            {
                return TimeSpan.FromSeconds((savedFrames + currentFrames) / 60f);
            }
        }
        #endregion

        private DeepPointer RomName;

        private MemoryWatcher<GameModule> Module;
        private MemoryWatcher<GameState> Progress;
        private MemoryWatcher<Pendant> Pendants;
        private MemoryWatcher<Crystal> Crystals;

        #region Randomizer items
        private MemoryWatcher<Randomizer.InventorySwap> SwappableInventory;
        private MemoryWatcher<Randomizer.InventorySwap2> SwappableInventory2;
        private MemoryWatcher<byte> Hammer;
        private MemoryWatcher<byte> Gloves;
        private MemoryWatcher<byte> Boots;
        #endregion

        #region Better check for defeated bosses in the Randomizer
        // Instead of checking which pendants and crystals dropped in
        private Dictionary<String, byte> bossRoomIds = new Dictionary<string, byte> {

            { "Eastern Palace", 0xC8 },
            { "Desert Palace", 0x33 },
            { "Tower of Hera", 0x07 },

            { "Palace of Darkness", 0x5A },
            { "Swamp Palace", 0x02 },
            { "Skull Woods", 0x29 },
            { "Thieves' Town", 0xAC },
            { "Ice Palace", 0xDE },
            { "Misery Mire", 0x90 },
            { "Turtle Rock", 0xA4 }
        };

        private Dictionary<String, DeepPointer> bossRoomFlags = new Dictionary<string, DeepPointer>();
        #endregion

        private Process Emulator;

        private Pendant donePendants;
        private Crystal doneCrystals;

        private LiveSplitState currentState;

        /// <summary>
        /// snes9x RAM addresses (i.e. where game RAM is)
        /// </summary>
        private static Dictionary<String, List<EmulatorOffsets>> offsets = new List<EmulatorOffsets>
        {
            new Snes9xOffsets { Process = "snes9x", Version = "1.53", RAM = 0x2EFBA4, TotalEmulatedFrames = 0x2FAD20 },
            new Snes9xOffsets { Process = "snes9x", Version = "1.54.1", RAM = 0x3410D4, TotalEmulatedFrames = 0x341008 },
            new Snes9xOffsets { Process = "snes9x-x64", Version = "1.53", RAM = 0x405EC8, TotalEmulatedFrames = 0x4190E8 },
            new Snes9xOffsets { Process = "snes9x-x64", Version  ="1.54.1", RAM = 0x4DAF18, TotalEmulatedFrames = 0x4D7A28 },
        }.GroupBy(x => x.Process).ToDictionary(x => x.Key, x => x.ToList());

        public void Update(LiveSplitState state)
        {
            currentState = state;
            if (!FindEmulator() || !IsLegendOfZelda())
            {
                savedFrames += currentFrames;
                currentFrames = 0;

                return;
            }

            Progress.Update(Emulator);
            Module.Update(Emulator);
            
            // Try to calculate the game time in terms of emulated frames
            if (IPPU_TotalEmulatedFrames != null)
            {
                IPPU_TotalEmulatedFrames.Update(Emulator);
                uint frames = IPPU_TotalEmulatedFrames.Current;

                if (Module.Old < GameModule.MAX && Module.Current >= GameModule.MAX)
                {
                    // just hit reset, so save the current frames
                    savedFrames += currentFrames;
                    currentFrames = 0;
                }
                else if (Module.Old >= GameModule.MAX && Module.Current < GameModule.MAX)
                {
                    // LTTP is playing again, the first (few?) frames of initialization aren't relevant to the time
                    savedFrames -= frames;
                    currentFrames = frames;
                }
                else if (Module.Current < GameModule.MAX)
                {
                    currentFrames = frames;
                }
            }


            // (snes9x) Resetting the emulator will fill the complete RAM with 0x55.
            // However, there's no Module with ID 0x55, so we're prior to real initialization.
            if (Module.Current >= GameModule.MAX)
                return;

            if (IsRandomized && Module.Current > GameModule.LoadFile)
                CheckForItems();
            
            // Only check for progress if it changed by one, which is normal story progression.
            // If you load a new game, your progress is 0 (and thus this isn't executed);
            // if you load an existing game, you'll start in state 2 or 3, which also won't lead to this being executed.
            if (Progress.Changed && (Progress.Old + 1) == Progress.Current)
                CheckForProgress();

            // The currently active screen changed.
            if (Module.Changed)
            {
                if (Module.Old == GameModule.LoadFile && Module.Current == GameModule.Dungeon)
                {
                    // Did we just start a new game?
                    if (Progress.Current == GameState.Start)
                    {
                        donePendants = 0;
                        doneCrystals = 0;

                        // Make sure we're not counting our passed frames so far as elapsed time
                        savedFrames = -currentFrames;

                        OnNewGame?.Invoke(this, new StateEventArgs(state));
                    }
                }
                else if ((Module.Old == GameModule.GanonVictory || Module.Old == GameModule.Victory) && Module.Old != Module.Current)
                    // we probably finished some dungeon
                    CheckForFinishedDungeon();

                else if (Module.Old == GameModule.GanonEmerges && Module.Current != GameModule.GanonEmerges)
                    Split?.Invoke(this, new SplitEventArgs(currentState, "Ganon's Tower"));

                else if (Module.Old != GameModule.TriforceRoom && Module.Current == GameModule.TriforceRoom)
                    // we're done, time to go home
                    Split?.Invoke(this, new SplitEventArgs(currentState, ALTTPComponent.TRIFORCE));
            }
        }

        private void CheckForProgress()
        {
            switch (Progress.Current)
            {
                case GameState.RescuedZelda:
                    Split?.Invoke(this, new SplitEventArgs(currentState, "Hyrule Castle"));
                    break;

                case GameState.BeatLightworldAgahnim:
                    Split?.Invoke(this, new SplitEventArgs(currentState, "Hyrule Castle Tower"));
                    break;
            }
        }

        #region Checks for finished dungeons (pendants & crystals)
        private void CheckForFinishedDungeon()
        {
            Pendants.Update(Emulator);
            Crystals.Update(Emulator);

            // LW dungeons
            CheckPendant("Eastern Palace", Pendant.GREEN);
            CheckPendant("Desert Palace", Pendant.BLUE);
            CheckPendant("Tower of Hera", Pendant.RED);

            // DW dungeons
            CheckCrystal("Palace of Darkness", Crystal.PalaceOfDarkness);
            CheckCrystal("Swamp Palace", Crystal.SwampPalace);
            CheckCrystal("Skull Woods", Crystal.SkullWoods);
            CheckCrystal("Thieves' Town", Crystal.ThievesTown);
            CheckCrystal("Ice Palace", Crystal.IcePalace);
            CheckCrystal("Misery Mire", Crystal.MiseryMire);
            CheckCrystal("Turtle Rock", Crystal.TurtleRock);
        }

        private void CheckPendant(string dungeonName, Pendant p)
        {
            if ((donePendants & p) == p)
                return;

            if (IsRandomized)
            {
                // This logic might not be entirely correct:
                //
                // We know that we emerged victorious from some fight, but have no idea what pendant/crystal it actually was.
                // However, since pendants and crystals are randomized, this could literally be anything.
                var pointer = bossRoomFlags[dungeonName];
                short value = pointer.Deref<short>(Emulator);

                // Is the boss for this room cleared?
                if ((value & 0x0800) == 0x0800)
                {
                    donePendants |= p;
                    Split?.Invoke(this, new SplitEventArgs(currentState, dungeonName));
                }
            }
            else
            {
                if ((Pendants.Current & p) == p)
                {
                    donePendants = Pendants.Current;
                    Split?.Invoke(this, new SplitEventArgs(currentState, dungeonName));
                }
            }
        }

        private void CheckCrystal(String dungeonName, Crystal c)
        {
            if ((doneCrystals & c) == c)
                return;

            if (IsRandomized)
            {
                // see the pendant check for why this is done
                var pointer = bossRoomFlags[dungeonName];
                short value = pointer.Deref<short>(Emulator);

                // Is the boss for this room cleared?
                if ((value & 0x0800) == 0x0800)
                {
                    doneCrystals |= c;
                    Split?.Invoke(this, new SplitEventArgs(currentState, dungeonName));
                }
            }
            else
            {
                if ((Crystals.Current & c) == c)
                {
                    doneCrystals = Crystals.Current;
                    Split?.Invoke(this, new SplitEventArgs(currentState, dungeonName));
                }
            }
        }
        #endregion

        private void CheckForItems()
        {
            SwappableInventory.Update(Emulator);
            if (SwappableInventory.Changed)
            {
                // were we just given the fake flute?
                // NOTE: this doesn't check for the real flute, i.e. when you are able to access the duck, because -finding- the flute is the more important issue.
                if ((SwappableInventory.Current & Randomizer.InventorySwap.FakeFlute) == Randomizer.InventorySwap.FakeFlute)
                    Split?.Invoke(this, new SplitEventArgs(currentState, "Flute", true));
            }

            SwappableInventory2.Update(Emulator);
            if (SwappableInventory2.Changed)
            {
                // were we just given the bow?
                if ((SwappableInventory2.Current & Randomizer.InventorySwap2.Bow) == Randomizer.InventorySwap2.Bow)
                    Split?.Invoke(this, new SplitEventArgs(currentState, "Bow", true));
            }

            Gloves.Update(Emulator);
            if (Gloves.Changed && Gloves.Current == 2)
                Split?.Invoke(this, new SplitEventArgs(currentState, "Titan's Mitt", true));

            Boots.Update(Emulator);
            if (Boots.Changed && Boots.Current > 0)
                Split?.Invoke(this, new SplitEventArgs(currentState, "Pegasus Boots", true));

            Hammer.Update(Emulator);
            if (Hammer.Changed && Hammer.Current > 0)
                Split?.Invoke(this, new SplitEventArgs(currentState, "Hammer", true));
        }

        /// <summary>
        /// Returns a compatible emulator instance.
        /// </summary>
        /// <returns>true if any emulator is running</returns>
        private bool FindEmulator()
        {
            if (Emulator?.HasExited == true)
                Emulator = null;

            if (Emulator != null)
                return true;

            try
            {
                foreach (var process in offsets)
                {
                    // Try to find any running emulator
                    Emulator = Process.GetProcessesByName(process.Key).FirstOrDefault();
                    if (Emulator != null && Emulator.HasExited)
                        Emulator = null;

                    if (Emulator != null)
                    {
                        // Are we using a supported version?
                        var version = Emulator.MainModuleWow64Safe().FileVersionInfo;
                        foreach (var emulatorOffsets in process.Value)
                        {
                            if (emulatorOffsets.Version == version.ProductVersion)
                            {
                                int pointerSize = process.Key == "snes9x-x64" ? 8 : 4;

                                // In snes9x source, this is defined in memmap.h
                                // uint *CMemory.RAM
                                int RAM = emulatorOffsets.RAM;

                                Module = new MemoryWatcher<GameModule>(new DeepPointer(RAM, 0x10));
                                Progress = new MemoryWatcher<GameState>(new DeepPointer(RAM, 0xF3C5));
                                Pendants = new MemoryWatcher<Pendant>(new DeepPointer(RAM, 0xF374));
                                Crystals = new MemoryWatcher<Crystal>(new DeepPointer(RAM, 0xF37A));

                                // Randomizer only: Swappable inventory for bottle/flute
                                SwappableInventory = new MemoryWatcher<Randomizer.InventorySwap>(new DeepPointer(RAM, 0xF412));
                                SwappableInventory2 = new MemoryWatcher<Randomizer.InventorySwap2>(new DeepPointer(RAM, 0xF414));

                                // Items
                                Hammer = new MemoryWatcher<byte>(new DeepPointer(RAM, 0xF34B));
                                Gloves = new MemoryWatcher<byte>(new DeepPointer(RAM, 0xF354));
                                Boots = new MemoryWatcher<byte>(new DeepPointer(RAM, 0xF355));

                                // Room Information
                                bossRoomFlags = bossRoomIds.ToDictionary(room => room.Key, room => new DeepPointer(RAM, 0xF000 + room.Value * 2));

                                // Try reading rom info
                                RomName = new DeepPointer(RAM + pointerSize, 0x7FC0);

                                // PPU info
                                IPPU_TotalEmulatedFrames = new MemoryWatcher<uint>(new DeepPointer(emulatorOffsets.TotalEmulatedFrames));

                                Log.Info("Using " + process.Key + ", " + version);
                                return true;
                            }
                        }

                        MessageBox.Show("Unsupported " + process.Key + " version: " + version.ProductVersion, "LTTP AutoSplitter", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Emulator = null;
                        return false;
                    }
                }
            }
            catch (Exception)
            {
                // Make sure there's no error when quitting the process
                Emulator = null;
            }
            // No emulator process running
            return false;
        }

        private bool IsLegendOfZelda()
        {
            if (RomName == null)
                return false;

            // Try and see what game version we're using
            string romName;
            RomName.DerefString(Emulator, 21, out romName);
            if (romName == "ZELDANODENSETSU      " || romName == "THE LEGEND OF ZELDA  ")
            {
                IsRandomized = false;
                return true;
            }
            else if(romName.StartsWith("Z3Rv") || romName.StartsWith("VT"))
            {
                // The normal randomizer is Z3Rv<version> <difficulty><seed>
                // VT are randomized seeds created by Veetorp, VT<difficulty><seed>v<version>
                IsRandomized = true;
                return true;
            }
            return false;
        }
    }

    public class StateEventArgs : EventArgs
    {
        public LiveSplitState State { get; protected set; }

        public StateEventArgs(LiveSplitState state)
        {
            this.State = state;
        }
    }

    public class SplitEventArgs : StateEventArgs
    {
        public String SplitName { get; protected set; }

        public bool Item { get; protected set; }

        public SplitEventArgs(LiveSplitState state, string splitName) : base(state)
        {
            this.SplitName = splitName;
        }

        public SplitEventArgs(LiveSplitState state, string splitName, bool item) : this(state, splitName)
        {
            this.Item = item;
        }
    }
}
