using Godot;
using static Biome;
[Tool, GlobalClass]
public partial class BoolCondition : Resource, IBiomeGenerationCondition
{
    [Export] bool output;

    public bool CheckCondition(BiomeGenerator.GridCell[] neighbors)
    {
        return output;
    }

    public void InitialSetup()
    {
    }
}
