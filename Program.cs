using System;

namespace CatDetective
{
    /// <summary>
    /// Entry point. MonoGame requires [STAThread] on Windows for COM interop.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            using var game = new Game1();
            game.Run();
        }
    }
}
