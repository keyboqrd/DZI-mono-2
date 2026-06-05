using NetVips;
using static NetVips.Enums;

namespace DZI_gen.Options
{
    public sealed class TilingOptions
    {
        public const string Position = "Tiling";

        public int TileSize { get; set; } = 1024;

        public ForeignDzLayout Layout { get; set; } = ForeignDzLayout.Dz;
    }
}
