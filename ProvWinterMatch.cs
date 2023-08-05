using LicariousPDXLibrary;

namespace WinterTerrainMapper
{
    internal class ProvWinterMatch
    {
        public float winterValue;
        public int sharedPixels;

        public ProvWinterMatch(Province prov, Province winter) {
            winterValue = winter.winter;
            sharedPixels = prov.coords.Intersect(winter.coords).Count();
        }
    }
}
