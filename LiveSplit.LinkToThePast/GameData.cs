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

        private MemoryWatcher<GameModule> Module;
        private MemoryWatcher<GameState> Progress;
        private MemoryWatcher<Pendant> Pendants;
        private MemoryWatcher<Crystal> Crystals;

        // Randomizer items
        private MemoryWatcher<Randomizer.InventorySwap> SwappableInventory;
        private MemoryWatcher<Randomizer.InventorySwap2> SwappableInventory2;
        private MemoryWatcher<byte> Hammer;
        private MemoryWatcher<byte> Gloves;
        private MemoryWatcher<byte> Boots;
        // End randomizer items

        private Process Emulator;

        private Pendant donePendants;
        private Crystal doneCrystals;

        private LiveSplitState currentState;

        /// <summary>
        /// Base addresses to look up stuff in $7E from.
        /// </summary>
        private static Dictionary<String, Dictionary<String, int>> baseAddresses = new Dictionary<String, Dictionary<String, int>>{
            { "snes9x", new Dictionary<String, int> {
                { "1.53", 0x2EFBA4 },
                { "1.54.1", 0x3410D4 }
            }},
            { "snes9x-x64", new Dictionary<String, int>{
                { "1.53", 0x405EC8 },
                { "1.54.1", 0x4DAF18 },
            }}
        };

        public void Update(LiveSplitState state, bool randomized)
        {
            currentState = state;
            if (!FindEmulator())
                return;

            Progress.Update(Emulator);
            Module.Update(Emulator);

            if (randomized && Module.Current > GameModule.LoadFile)
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

        // TODO this mostly checks for pendants & crystal ownership, not which bosses you've beat
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
            if ((donePendants & p) == 0 && (Pendants.Current & p) == p)
            {
                donePendants = Pendants.Current;
                Split?.Invoke(this, new SplitEventArgs(currentState, dungeonName));
            }
        }

        private void CheckCrystal(String dungeonName, Crystal c)
        {
            if ((doneCrystals & c) == 0 && (Crystals.Current & c) == c)
            {
                doneCrystals = Crystals.Current;
                Split?.Invoke(this, new SplitEventArgs(currentState, dungeonName));
            }
        }

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
                foreach (var process in baseAddresses)
                {
                    // Try to find any running emulator
                    Emulator = Process.GetProcessesByName(process.Key).FirstOrDefault();
                    if (Emulator != null && Emulator.HasExited)
                        Emulator = null;

                    if (Emulator != null)
                    {
                        // Are we using a supported version?
                        var version = Emulator.MainModuleWow64Safe().FileVersionInfo;
                        foreach (var productVersion in process.Value)
                        {
                            if (productVersion.Key == version.ProductVersion)
                            {
                                // In snes9x source, this is defined in memmap.h
                                // uint *CMemory.SRAM
                                int sramBase = productVersion.Value;

                                Module = new MemoryWatcher<GameModule>(new DeepPointer(sramBase, 0x10));
                                Progress = new MemoryWatcher<GameState>(new DeepPointer(sramBase, 0xF3C5));
                                Pendants = new MemoryWatcher<Pendant>(new DeepPointer(sramBase, 0xF374));
                                Crystals = new MemoryWatcher<Crystal>(new DeepPointer(sramBase, 0xF37A));

                                // Randomizer only: Swappable inventory for bottle/flute
                                SwappableInventory = new MemoryWatcher<Randomizer.InventorySwap>(new DeepPointer(sramBase, 0xF412));
                                SwappableInventory2 = new MemoryWatcher<Randomizer.InventorySwap2>(new DeepPointer(sramBase, 0xF414));

                                // Items
                                Hammer = new MemoryWatcher<byte>(new DeepPointer(sramBase, 0xF34B));
                                Gloves = new MemoryWatcher<byte>(new DeepPointer(sramBase, 0xF354));
                                Boots = new MemoryWatcher<byte>(new DeepPointer(sramBase, 0xF355));

                                Log.Info("Using " + process.Key + ", " + version);
                                return true;
                            }
                        }

                        MessageBox.Show("Unsupported " + process.Key + " version: " + version.ProductVersion, "LTTP AutoSplitter", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
