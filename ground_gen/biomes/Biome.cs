using Godot;
[Tool, GlobalClass]
public partial class Biome : Resource
{
    [Export] public NoiseComponent noise;
    [Export] public byte type_index;
}
