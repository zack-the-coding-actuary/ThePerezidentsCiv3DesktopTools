using QueryCiv3;
using QueryCiv3.Sav;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Civ3Tools
{
    public static class TurnOrchestrator
    {
        public static GAME? GetSavData(string savPath, string? biqPath = null)
        {
            biqPath ??= InferBiqPath(savPath);
            var saveBytes = Util.ReadFile(savPath);
            var biqBytes = Util.ReadFile(biqPath);
            var savData = new SavData(saveBytes, biqBytes);
            return savData.Game;
        }

        // Walk up two directories from the SAV (e.g. Saves/Auto/ -> Conquests/) and find the first .biq file.
        private static string InferBiqPath(string savPath)
        {
            string conquestsDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(savPath)!, "..", ".."));
            string[] biqs = Directory.GetFiles(conquestsDir, "*.biq");
            if (biqs.Length == 0)
                throw new FileNotFoundException($"Could not find a .biq file in '{conquestsDir}'. Pass biqPath explicitly.");
            return biqs[0];
        }
    }
}
