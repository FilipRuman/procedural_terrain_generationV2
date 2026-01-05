using System.Collections.Generic;
using Godot;
using System.Linq;
using static Biome;

[Tool, GlobalClass]
public partial class BlackList : Resource, IBiomeGenerationCondition
{
    [Export] Biome[] black_list;
    HashSet<Biome> black_hash_set;
    public bool CheckCondition(BiomeGenerator.GridCell[] neighbors)
    {
        foreach (BiomeGenerator.GridCell to_check in neighbors)
        {
            if (black_hash_set.Contains(to_check.biome))
            {
                return false;
            }
        }
        return true;
    }

    public void InitialSetup()
    {
        black_hash_set = black_list.ToHashSet();
    }
}
