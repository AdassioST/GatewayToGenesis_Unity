using System.Collections.Generic;
using UnityEngine;

public enum PixelIcon { Compass, Crew, Crown, Home, Book, Beast, Culture, Chronicle, Rumour, Expand, Collapse, Idle, Supply, Music, Play, Pause, Help, Settings }

/// <summary>Small silhouettes drawn on a fixed pixel grid, independent of font glyph coverage.</summary>
public static class PixelIcons
{
    private static readonly Dictionary<PixelIcon, Sprite> Cache = new Dictionary<PixelIcon, Sprite>();
    public static Sprite Get(PixelIcon icon)
    {
        if (Cache.TryGetValue(icon, out var sprite) && sprite != null) return sprite;
        string[] rows;
        switch (icon)
        {
            case PixelIcon.Crew: rows = new[] {"................", "..###.....###...", "..###.....###...", "...#.......#....", ".#####...#####..", ".#####...#####..", "..###.....###...", "..#.#.....#.#..."}; break;
            case PixelIcon.Crown: rows = new[] {"................", ".......##.......", ".#.....##.....#.", ".##...####...##.", "..##.######.##..", "..############..", "...##########...", "...##########..."}; break;
            case PixelIcon.Home: rows = new[] {".......##.......", ".....######.....", "...##########...", ".##############.", "...##########...", "...###....###...", "...###....###...", "...###....###..."}; break;
            case PixelIcon.Book: rows = new[] {".######..######.", ".#....#..#....#.", ".#....#..#....#.", ".#....#..#....#.", ".#....#..#....#.", ".#....#..#....#.", ".##############.", ".......##......."}; break;
            case PixelIcon.Beast: rows = new[] {"..##........##..", "..####....####..", "...##########...", "...##..##..##...", "...##########...", "....##.##.##....", ".....######.....", "......####......"}; break;
            case PixelIcon.Culture: rows = new[] {"......####......", "....########....", "..############..", "...##..##..##...", "...##..##..##...", "...##..##..##...", "..############..", "..############.."}; break;
            case PixelIcon.Chronicle: rows = new[] {"..############..", "..##........##..", "...#..####..#...", "...#........#...", "...#..####..#...", "...#........#...", "..##........##..", "..############.."}; break;
            case PixelIcon.Rumour: rows = new[] {"..############..", ".##..........##.", ".#..##.##.##..#.", ".#............#.", ".##..........##.", "..############..", "....###.........", "....##.........."}; break;
            case PixelIcon.Expand: rows = new[] {"................", "....##..........", "......##........", "........##......", "........##......", "......##........", "....##..........", "................"}; break;
            case PixelIcon.Collapse: rows = new[] {"................", "..........##....", "........##......", "......##........", "......##........", "........##......", "..........##....", "................"}; break;
            case PixelIcon.Idle: rows = new[] {".....######.....", "....##....##....", "...##..#...##...", "...#...#....#...", "...#...###..#...", "...##......##...", "....##....##....", ".....######....."}; break;
            case PixelIcon.Supply: rows = new[] {"......####......", "....########....", "...##########...", "...##......##...", "...##..##..##...", "...##.####.##...", "...##..##..##...", "...##########..."}; break;
            case PixelIcon.Music: rows = new[] {".....#########..", ".....##.....##..", ".....##.....##..", ".....##.....##..", ".....##.....##..", "..#####..#####..", ".######.######..", "..####...####..."}; break;
            case PixelIcon.Play: rows = new[] {"....##..........", "....####........", "....######......", "....########....", "....########....", "....######......", "....####........", "....##.........."}; break;
            case PixelIcon.Pause: rows = new[] {"...###....###...", "...###....###...", "...###....###...", "...###....###...", "...###....###...", "...###....###...", "...###....###...", "...###....###..."}; break;
            case PixelIcon.Help: rows = new[] {".....######.....", "....##....##....", "..........##....", "........###.....", ".......##.......", "................", ".......##.......", ".......##......."}; break;
            case PixelIcon.Settings: rows = new[] {"......####......", "..##..####..##..", "...##########...", "..####....####..", "..####....####..", "...##########...", "..##..####..##..", "......####......"}; break;
            default: rows = new[] {".......##.......", "......####......", ".....######.....", "....########....", "...####..####...", "..####....####..", ".####......####.", "..##........##.."}; break;
        }
        // Double the row height so every symbol occupies the same 16 x 16 art grid.
        var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false) { name = "UI " + icon, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color32[256];
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
            pixels[y * 16 + x] = rows[7 - y / 2][x] == '#' ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
        texture.SetPixels32(pixels); texture.Apply(false, true);
        sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(.5f, .5f), 16);
        sprite.name = icon.ToString(); sprite.hideFlags = HideFlags.HideAndDontSave;
        return Cache[icon] = sprite;
    }
}
