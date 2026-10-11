namespace PsycheVR.OpsConsole.Core
{
    /// <summary>
    /// How a ping reaches the DSN tab: start in place only when that tab is on screen and no page slide
    /// is in progress (a pending slide would re-show a tab under the ping); otherwise slide to it.
    /// </summary>
    public static class PingRoute
    {
        /// <summary>
        /// 0 to start the ping at once, else the slide direction (+1 forward, -1 back) to the DSN tab.
        /// <paramref name="shownTab"/> is -1 when no tab shows.
        /// </summary>
        public static int SlideDirection(int shownTab, int dsnTab, bool sliding)
        {
            if (shownTab == dsnTab && !sliding) return 0;
            return shownTab < dsnTab ? +1 : -1;
        }
    }
}
