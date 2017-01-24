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
        public const String START = "This seed is the worst.";

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

            Log.Info("Splitting on " + e.SplitName + " (randomized: " + gameData.IsRandomized + ")");
            if (gameData.IsRandomized)
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

                string oldSplit = state.CurrentSplit?.Name;
                Log.Info("current split: " + oldSplit);

                if (oldSplit == TRIFORCE || oldSplit == START)
                {
                    Image icon = null;
                    icons.TryGetValue(e.SplitName, out icon);

                    state.CurrentSplit.Name = e.SplitName;
                    state.CurrentSplit.Icon = icon;

                    // If our new split is the Triforce room; OR if we're just overwriting the first split, don't add any further splits.
                    if (e.SplitName != TRIFORCE && oldSplit != START)
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
            if (gameData.IsRandomized)
            {
                e.State.Run.Clear();

                // LiveSplit pretends its in Timer-only mode if there is only one segment (Triforce), which means new segments won't be added
                e.State.Run.AddSegment(START);

                e.State.Run.AddSegment(TRIFORCE, icon: icons[TRIFORCE]);
            }

            timer.Reset();
            timer.Start();

            e.State.IsGameTimePaused = true;
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

            if (state.IsGameTimeInitialized)
                timer.InitializeGameTime();
            if (state.CurrentPhase == TimerPhase.Running)
                state.SetGameTime(gameData.GameTime);
        }
    }
}
