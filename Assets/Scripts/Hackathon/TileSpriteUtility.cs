using System.Reflection;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class TileSpriteUtility
{
    public static Sprite GetSprite(TileBase tile)
    {
        if (tile == null)
        {
            return null;
        }

        if (tile is UnityEngine.Tilemaps.Tile simpleTile)
        {
            return simpleTile.sprite;
        }

        Sprite[] animatedSprites = GetAnimatedSprites(tile);
        if (animatedSprites != null && animatedSprites.Length > 0)
        {
            return animatedSprites[0];
        }

        FieldInfo spriteField = tile.GetType().GetField(
            "m_Sprite",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
        );
        if (spriteField != null && spriteField.GetValue(tile) is Sprite fieldSprite)
        {
            return fieldSprite;
        }

        PropertyInfo spriteProperty = tile.GetType().GetProperty("sprite", BindingFlags.Instance | BindingFlags.Public);
        if (spriteProperty != null && spriteProperty.GetValue(tile) is Sprite propertySprite)
        {
            return propertySprite;
        }

        return null;
    }

    private static Sprite[] GetAnimatedSprites(TileBase tile)
    {
        FieldInfo field = tile.GetType().GetField(
            "m_AnimatedSprites",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
        );
        if (field != null && field.GetValue(tile) is Sprite[] sprites)
        {
            return sprites;
        }

        PropertyInfo property = tile.GetType().GetProperty(
            "animatedSprites",
            BindingFlags.Instance | BindingFlags.Public
        );
        if (property != null && property.GetValue(tile) is Sprite[] propertySprites)
        {
            return propertySprites;
        }

        return null;
    }
}
