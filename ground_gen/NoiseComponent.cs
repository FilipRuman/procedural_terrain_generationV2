using Godot;
[Tool, GlobalClass]
public partial class NoiseComponent: Resource
{

    [Export] public FastNoiseLite noise;
    [Export] public float amplitude;
    [Export] public float frequency = 1f;
    public float GetHeight(Vector2 pos)
    {
        return noise.GetNoise2Dv(pos * frequency) * amplitude;
    }
}
