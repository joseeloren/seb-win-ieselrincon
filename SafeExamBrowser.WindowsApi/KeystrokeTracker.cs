using System.Threading;

namespace SafeExamBrowser.WindowsApi
{
    public static class KeystrokeTracker
    {
        private static int globalKeystrokes = 0;
        private static int sebKeystrokes = 0;

        public static int GlobalKeystrokes
        {
            get { return globalKeystrokes; }
        }

        public static int SebKeystrokes
        {
            get { return sebKeystrokes; }
        }

        public static void IncrementGlobal()
        {
            Interlocked.Increment(ref globalKeystrokes);
        }

        public static void IncrementSeb()
        {
            Interlocked.Increment(ref sebKeystrokes);
        }
    }
}
