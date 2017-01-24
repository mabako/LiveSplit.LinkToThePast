namespace LiveSplit.LinkToThePast
{
    internal interface EmulatorOffsets
    {
        string Process { get; }
        string Version { get; }
        bool Is64Bit { get; }

        int RAM { get; }
        int TotalEmulatedFrames { get; }
    }

    internal class Snes9xOffsets : EmulatorOffsets
    {
        public string Process { get; internal set; }
        public string Version { get; internal set; }

        // Pointer to Memory.RAM
        public int RAM { get; internal set; }

        // Pointer to IPPU.TotalEmulatedFrames
        public int TotalEmulatedFrames { get; internal set; }

        public bool Is64Bit
        {
            get
            {
                return Process == "snes9x-x64";
            }
        }
    }
}