using System;
using System.Collections.Generic;
using System.Linq;
using Bedrockix;
using Bedrockix.Windows;

static class Program
{
    static void Main(string[] args)
    {
        var game = Minecraft.Release;

        if (!game.Installed) { Console.WriteLine("Minecraft: Bedrock Edition isn't installed!"); return; }

        List<Library> libraries = [];
        foreach (var arg in args)
        {
            Library library = new(arg);
            if (!library.Valid) { Console.WriteLine($"Found invalid dynamic link library: \"{library.Path}\""); continue; }
            libraries.Add(library);
        }

        game.Terminate(); game.Loader.Launch(libraries);
    }
}