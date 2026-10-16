// Pieces of the original IES2 executable that the ECU modules depend on but
// that live in WinForms files (Program.cs). Reproduced verbatim in behaviour.

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("MarelliIawEcuStudio.Tests")]

namespace IES_2
{
    internal static class LegacyExtensions
    {
        /// <summary>Copy of <c>Program.GetBit</c> from IES2.</summary>
        public static bool GetBit(this byte value, byte index)
        {
            return ((value & (byte)(1 << index)) != 0);
        }
    }
}
