using VRage.Utils;

namespace ZoneControl
{
    public static class Log
    {
        public static bool Debug;
        public static void Msg(string msg)
        {
            MyLog.Default.WriteLine($"ZoneControl: {msg}");
        }
    }
}
