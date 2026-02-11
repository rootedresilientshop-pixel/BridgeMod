using System;

namespace BridgeMod.Tools.ModPackager;

class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("ModPackager v0.2.0");
        Console.WriteLine("Usage: ModPackager [options] <mod-directory>");
        Console.WriteLine();
        Console.WriteLine("This tool is under development.");
        Console.WriteLine("See: https://github.com/rootedresilientshop-pixel/BridgeMod");

        if (args.Length == 0)
        {
            return 1;
        }

        return 0;
    }
}
