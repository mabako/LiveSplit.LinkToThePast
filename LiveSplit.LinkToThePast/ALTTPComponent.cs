using LiveSplit.UI.Components;
using System;
using System.Collections.Generic;
using LiveSplit.Model;
using LiveSplit.UI;
using System.Xml;
using System.Windows.Forms;
using System.Drawing;

namespace LiveSplit.LinkToThePast
{
    public class ALTTPComponent : LogicComponent
    {
        private const String UNKNOWN = "???";

        private GameData gameData;
        private TimerModel timer;

        private static SplitIcons icons = new SplitIcons();

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
                // Hyrule Castle is the default split that is always visible, while Ganon is the last split after no further splits should be added
                if (e.SplitName == "Hyrule Castle" || e.SplitName == "Ganon")
                    timer.Split();
                else {
                    if (state.CurrentSplit.Name == UNKNOWN)
                    {
                        Image icon = null;
                        icons.TryGetValue(e.SplitName, out icon);

                        state.CurrentSplit.Name = e.SplitName;
                        state.CurrentSplit.Icon = icon;

                        // add a new unknown segment
                        run.AddSegment(UNKNOWN);
                    }
                    timer.Split();
                }
            } else
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
                SetupRandomizer(e.State.Run);
            }

            timer.Reset();
            timer.Start();
        }

        /// <summary>
        /// Always start with Hyrule Castle Escape
        /// </summary>
        /// <param name="run"></param>
        private void SetupRandomizer(IRun run)
        {
            run.Clear();

            Image icon = null;
            icons.TryGetValue("Hyrule Castle", out icon);
            run.AddSegment("Hyrule Castle", icon: icon);

            run.AddSegment(UNKNOWN);
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
            gameData.Update(state);
        }
    }
}
