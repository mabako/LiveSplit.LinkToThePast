namespace LiveSplit.LinkToThePast
{
    class Randomizer
    {
        public enum InventorySwap : byte
        {
            BlueBoomerang = 0x80,
            RedBoomerang = 0x40,
            Mushroom = 0x20,
            MagicPowder = 0x10,
            // nothing is at 0x08
            Shovel = 0x04,
            FakeFlute = 0x02,
            WorkingFlute = 0x01
        }

        public enum InventorySwap2 : byte
        {
            Bow = 0x80,
            BowWithSilverArrows = 0x40
        }
    }
}
