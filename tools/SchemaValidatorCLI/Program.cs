using System;

namespace BridgeMod.Tools.SchemaValidator;

class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("SchemaValidator v0.2.0");
        Console.WriteLine("Usage: SchemaValidator [options] <schema-file>");
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
