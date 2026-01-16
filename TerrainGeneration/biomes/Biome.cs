using Godot;
[Tool, GlobalClass]
public partial class Biome : Resource
{
    public byte index_in_biomes_array;

    [Export] public Texture albedo;
    [Export] public Texture normal;
    [Export] public Texture roughness;
    [Export(PropertyHint.ColorNoAlpha)] public Color tint;
    [Export] public float saturation;
    [Export] public float scale;
}

