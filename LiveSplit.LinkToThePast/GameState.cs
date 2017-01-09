namespace LiveSplit.LinkToThePast
{
    /// <summary>
    /// Current state of the story
    /// </summary>
    enum GameState : byte
    {
        Start = 0x00,
        GotSword = 0x01,
        RescuedZelda = 0x02,
        BeatLightworldAgahnim = 0x03
    }
}
