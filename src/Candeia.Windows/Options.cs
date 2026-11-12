namespace Candeia;

// The player's choices for this session. They are chosen on the boot screen and kept until the game
// closes; saving them between sessions comes with the save file.
static class Options
{
    // Soft scares: no flashes (lightning, the match), a quieter capture that fades instead of striking,
    // and the system panels never leave the screen.
    public static bool SoftScares { get; set; }

    // The difficulty of the nights opened from now on: Night, or Vigil for people who never programmed.
    // The campaign's menu chooses it in delivery 9B; for now, V on the boot screen switches it.
    public static Night.Difficulty Difficulty { get; set; } = Night.Difficulty.Night;
}
