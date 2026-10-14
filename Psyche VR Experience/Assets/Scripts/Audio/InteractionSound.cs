namespace PsycheVR.Audio
{
    /// <summary>
    /// The one-shot interaction sounds. Each maps to a set of clip variants in the
    /// <see cref="InteractionSoundLibrary"/> asset; scripts play them with <see cref="InteractionAudio.Play"/>.
    /// </summary>
    public enum InteractionSound
    {
        /// <summary>A slapped key, the PING dome or a plaque button going down.</summary>
        ButtonPress,
        /// <summary>A clicker pen's button.</summary>
        PenClick,
        /// <summary>A spacecraft puzzle piece locking into its snap zone: a click and a soft success tone.</summary>
        PuzzleSnap,
        /// <summary>A scrapped-plan sheet crumpling into a ball.</summary>
        PaperCrumple,
        /// <summary>A book opening when picked up.</summary>
        BookOpen,
        /// <summary>A book closing when let go.</summary>
        BookClose,
        /// <summary>A page turned over to the other side of the spine.</summary>
        PageTurn,
        /// <summary>A ball hitting the floor, a wall or furniture.</summary>
        BallBounce,
        /// <summary>A drawer reaching its fully open stop.</summary>
        DrawerStopOpen,
        /// <summary>A drawer shutting against the desk.</summary>
        DrawerStopClosed,
    }
}
