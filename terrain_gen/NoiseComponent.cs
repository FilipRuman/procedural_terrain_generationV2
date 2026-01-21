using Godot;
using System.Linq;
[Tool, GlobalClass]
public partial class NoiseComponent : Resource
{
    [Export] NoisePart[] noises;
    public float Sample(Vector2 pos)
    {
        return noises.Sum((NoisePart part) => { return part.Sample(pos); });
    }
    public float Amplitude
    {
        get { return noises.Sum((NoisePart part) => { return part.amplitude; }); }
    }
}


