using LiveSplit.UI.Components;
using System;
using LiveSplit.Model;
using LiveSplit.UI;
using System.Xml;
using System.Windows.Forms;
using System.Drawing;
using LiveSplit.Options;

namespace LiveSplit.LinkToThePast
{
    public class ALTTPComponent : LogicComponent
    {
        public const String TRIFORCE = "Triforce";

        private GameData gameData;
        private TimerModel timer;

        private SplitIcons icons = new SplitIcons();

        public override string ComponentName => "LTTP AutoSplitter";

        public ALTTPComponent(LiveSplitState state)
        {
            timer = new TimerModel { CurrentState = state };

            gameData = new GameData();
            gameData.OnNewGame += OnNewGame;
            gameData.Split += Split;
        }

        private void Split(object sender, SplitEventArgs e)
        {
            var state = e.State;
            var run = state.Run;

            if (IsRandomized(run))
            {
                if (e.Item)
                {
                    // If this is an item, loading the game would add it again
                    foreach (ISegment segment in state.Run)
                    {
                        if (segment.Name == e.SplitName)
                        {
                            Log.Info("Skipping a split on " + e.SplitName);
                            return;
                        }
                    }
                }

                // Ganon is the last split after no further splits should be added
                if (state.CurrentSplit.Name == TRIFORCE)
                {
                    Image icon = null;
                    icons.TryGetValue(e.SplitName, out icon);

                    state.CurrentSplit.Name = e.SplitName;
                    state.CurrentSplit.Icon = icon;

                    if (e.SplitName != TRIFORCE)
                    {
                        // add a new unknown segment
                        run.AddSegment(TRIFORCE, icon: icons[TRIFORCE]);
                    }

                    timer.Split();
                }
                else
                {
                    Log.Warning("Currently at split " + state.CurrentSplit.Name + " while trying to split " + e.SplitName);
                }
            }
            else if (!e.Item)
                // split without fancy extra
                timer.Split();
        }

        /// <summary>
        /// Reset and start the timer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnNewGame(object sender, StateEventArgs e)
        {
            if (IsRandomized(e.State.Run))
            {
                e.State.Run.Clear();
                e.State.Run.AddSegment(TRIFORCE, icon: icons[TRIFORCE]);
            }

            timer.Reset();
            timer.Start();
        }

        /// <summary>
        /// All runs containing 'random' in the category name (random%, randomized, ...) or 'seed' (e.g. seed 12345) will be treated as randomized.
        /// There's probably a better way to grab that directly from the RAM.
        /// </summary>
        /// <param name="run"></param>
        /// <returns></returns>
        private bool IsRandomized(IRun run)
        {
            if (String.IsNullOrEmpty(run.CategoryName))
                return false;

            return run.CategoryName.ToLower().Contains("random") || run.CategoryName.ToLower().Contains("seed");
        }

        public override void Dispose()
        {
        }

        public override XmlNode GetSettings(XmlDocument document)
        {
            return document.CreateElement("x");
        }

        public override Control GetSettingsControl(LayoutMode mode)
        {
            return null;
        }

        public override void SetSettings(XmlNode settings)
        {
        }

        public override void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
        {
            gameData.Update(state, IsRandomized(state.Run));
        }
    }
}
