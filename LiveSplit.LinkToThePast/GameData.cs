using LiveSplit.ComponentUtil;
using LiveSplit.Model;
using LiveSplit.Options;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace LiveSplit.LinkToThePast
{
    class GameData
    {
        public event EventHandler<StateEventArgs> OnNewGame;
        public event EventHandler<SplitEventArgs> Split;

        public MemoryWatcher<GameModule> Module;
        public MemoryWatcher<GameState> Progress;
        public MemoryWatcher<Pendant> Pendants;
        public MemoryWatcher<Crystal> Crystals;

        private Process Emulator;

        private Pendant donePendants;
        private Crystal doneCrystals;

        private LiveSplitState currentState;

        public void Update(LiveSplitState state)
        {
            currentState = state;
            if (!FindEmulator())
                return;

            Progress.Update(Emulator);
            Module.Update(Emulator);

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
                    Split?.Invoke(this, new SplitEventArgs(currentState, "Ganon"));
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
            if((donePendants & p) == 0 && (Pendants.Current & p) == p)
            {
                donePendants = Pendants.Current;
                Split?.Invoke(this, new SplitEventArgs(currentState, dungeonName));
            }
        }

        private void CheckCrystal(String dungeonName, Crystal c)
        {
            if((doneCrystals & c) == 0 && (Crystals.Current & c) == c)
            {
                doneCrystals = Crystals.Current;
                Split?.Invoke(this, new SplitEventArgs(currentState, dungeonName));
            }
        }

        /// <summary>
        /// Returns a compatible emulator instance.
        /// </summary>
        /// <returns>true if any emulator is running</returns>
        private bool FindEmulator()
        {
            if (Emulator == null || Emulator.HasExited)
            {
                // try to find snes9x.
                // the addresses have been tested with snes9x 1.53.
                Emulator = Process.GetProcessesByName("snes9x").FirstOrDefault();
                if (Emulator != null)
                {
                    Module = new MemoryWatcher<GameModule>(new DeepPointer(0x2EFBA4, 0x10));
                    Progress = new MemoryWatcher<GameState>(new DeepPointer(0x2EFBA4, 0xF3C5));
                    Pendants = new MemoryWatcher<Pendant>(new DeepPointer(0x2EFBA4, 0xF374));
                    Crystals = new MemoryWatcher<Crystal>(new DeepPointer(0x2EFBA4, 0xF37A));
                }
                else
                {
                    // Try to find the 64-bit snes9x
                    Emulator = Process.GetProcessesByName("snes9x-64").FirstOrDefault();
                    if (Emulator != null)
                    {
                        Module = new MemoryWatcher<GameModule>(new DeepPointer(0x405EC8, 0x10));
                        Progress = new MemoryWatcher<GameState>(new DeepPointer(0x405EC8, 0xF3C5));
                        Pendants = new MemoryWatcher<Pendant>(new DeepPointer(0x405EC8, 0xF374));
                        Crystals = new MemoryWatcher<Crystal>(new DeepPointer(0x405EC8, 0xF37A));
                    }
                }
            }

            return Emulator != null;
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

        public SplitEventArgs(LiveSplitState state, string SplitName) : base(state)
        {
            this.SplitName = SplitName;
        }
    }
}
