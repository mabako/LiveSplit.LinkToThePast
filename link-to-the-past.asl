// Tested with snes9x 1.53

state("snes9x-64")
{
	byte module: "snes9x-64.exe", 0x405EC8, 0x10;
	byte gameState: "snes9x-64.exe", 0x405EC8, 0xF3C5;
}

state("snes9x")
{
	byte module: "snes9x.exe", 0x2EFBA4, 0x10;
	byte gameState: "snes9x.exe", 0x2EFBA4, 0xF3C5;
}

split {
	// 2: escape is complete, or 3: agahnim dead is dead
	if(current.gameState >= 2 && old.gameState + 1 == current.gameState)
		return true;

	// after the victory animation.
	// this used to compare pendants/crystals before, but that got updated as soon as you got it, not once you left the dungeon
	if((old.module == 0x16 || old.module == 0x13) && old.module != current.module)
		return true;

	// after fat ganon bombed in the pyramid
	if(old.module == 0x18 && current.module != 0x18)
		return true;

	// entered the triforce room, skip through the remaining splits
	return current.module == 0x19;
}

start {
	return old.module == 5 && current.module == 7;
}

reset {
	// You can't save & quit before you finished the escape (state 0 or 1).
	return old.module == 5 && current.gameState < 2;
}
