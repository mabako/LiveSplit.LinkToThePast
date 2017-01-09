using LiveSplit.UI.Components;
using System;
using LiveSplit.Model;

[assembly: ComponentFactory(typeof(LiveSplit.LinkToThePast.Factory))]

namespace LiveSplit.LinkToThePast
{
    class Factory : IComponentFactory
    {
        public ComponentCategory Category => ComponentCategory.Other;

        public string ComponentName => "LTTP AutoSplitter";

        public string Description { get { return "Splits randomized Link to the Past runs"; } }

        public string UpdateName => this.ComponentName;

        public string UpdateURL => "https://raw.githubusercontent.com/mabako/alttp-splitter/master/";

        public Version Version => new Version(1, 0);

        public string XMLURL => this.UpdateURL + "LiveSplit.LinkToThePast.Updates.xml";

        public IComponent Create(LiveSplitState state)
        {
            return new ALTTPComponent(state);
        }
    }
}
