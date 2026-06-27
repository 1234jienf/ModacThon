public static class TileBlendLayouts
{
    public const int PathGroundTileCount = 7;
    public const int LakeTileCount = 9;

    public static readonly string[] PathGroundSlotNames =
    {
        "top_left",
        "top",
        "top_right",
        "bottom_left",
        "bottom",
        "bottom_right",
        "center"
    };

    // 0=l, 1=tl, 2=t, 3=tr, 4=r, 5=br, 6=b, 7=bl, 8=c
    public static readonly string[] LakeSlotNames =
    {
        "l",
        "tl",
        "t",
        "tr",
        "r",
        "br",
        "b",
        "bl",
        "c"
    };

    public static string[] GetSlotNames(string category)
    {
        return category == "lake" ? LakeSlotNames : PathGroundSlotNames;
    }

    public static int GetSlotCount(string category)
    {
        return category == "lake" ? LakeTileCount : PathGroundTileCount;
    }
}
