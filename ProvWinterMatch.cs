using LicariousPDXLibrary;
using System.Linq;

namespace WinterTerrainMapper
{
    internal class ProvWinterMatch
    {
        public float WinterValue { get; }
        public int SharedPixels { get; }

        public ProvWinterMatch(Province prov, Province winter) {
            WinterValue = winter.Winter;
            SharedPixels = prov.Coords.Intersect(winter.Coords).Count();
        }
    }
}
