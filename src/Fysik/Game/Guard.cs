using System;
using System.Collections.Generic;

namespace Fysik.Game
{
    internal static class Guard
    {
        private static readonly HashSet<string> s_reported = new HashSet<string>();

        public static void Report(string site, Exception e)
        {
            if (s_reported.Add(site))
                Plugin.Log.LogError($"{site} failed; vanilla behaviour continues. Further errors here are not logged.\n{e}");
        }
    }
}
