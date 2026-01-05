using System.Collections.Generic;
using Godot;
[Tool]
public partial class GroundMeshGen : MeshInstance3D
{


    private int triangle_count_per_dimension;
    private float triangle_size;
    public void Run(Biome[] biomes, BiomeGenerator.OutputData biome_data, int size, Vector2 base_world_position, int resolution, float uv_margin)
    {
        // needed because otherwise there will be a gap of 1 triangle size
        size += 1;
        triangle_count_per_dimension = size * resolution;
        triangle_size = 1f / resolution;

        var arrayMesh = GenerateTerrainMesh(biomes, biome_data, base_world_position, resolution, uv_margin);
        Mesh = arrayMesh;

    }


    private ArrayMesh GenerateTerrainMesh(Biome[] biomes, BiomeGenerator.OutputData biome_data, Vector2 base_world_position, int resolution, float uv_margin)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);


        GenerateVertexes(st, biomes, biome_data, base_world_position, resolution, uv_margin);

        GenerateIndexes(st);

        st.GenerateNormals();
        st.GenerateTangents();


        return st.Commit();
    }

    private void GenerateIndexes(SurfaceTool st)
    {
        for (int x = 0; x < triangle_count_per_dimension - 1; x++)
        {
            for (int z = 0; z < triangle_count_per_dimension - 1; z++)
            {
                int i = x + z * triangle_count_per_dimension;
                st.AddIndex(i);
                st.AddIndex(i + triangle_count_per_dimension);
                st.AddIndex(i + 1);

                st.AddIndex(i + 1);
                st.AddIndex(i + triangle_count_per_dimension);
                st.AddIndex(i + 1 + triangle_count_per_dimension);
            }
        }
    }


    private Vector2 CalculateVertexWorldPosition(uint relative_x, uint relative_z, Vector2 base_world_position)
    {
        return new Vector2(relative_x * triangle_size, relative_z * triangle_size) + base_world_position;
    }

    private float CalculateHeight(Vector2 uv_with_margin_included, Vector2 world_position, Biome[] biomes, BiomeGenerator.OutputData biome_data)
    {
        List<BiomeGenerator.OutputData.BiomeInfluenceOutput> biome_influences = biome_data.SampleBiomeDataForMesh(uv_with_margin_included);
        var output = 0f;
        foreach (var biome_influence_data in biome_influences)
        {
            var biome = biomes[biome_influence_data.biome_type_index];
            // TODO: Bake gradient
            output += biome.terrain_mesh_noise.Sample(world_position) * biome_influence_data.influence;
        }
        return output;
    }

    private void GenerateVertexes(SurfaceTool st, Biome[] biomes, BiomeGenerator.OutputData biome_data, Vector2 base_world_position, int resolution, float uv_margin)
    {
        for (uint relative_x = 0; relative_x < triangle_count_per_dimension; relative_x++)
        {
            for (uint relative_z = 0; relative_z < triangle_count_per_dimension; relative_z++)
            {

                var uv = new Vector2(relative_x / (float)triangle_count_per_dimension, relative_z / (float)triangle_count_per_dimension);
                st.SetUV(uv);


                Vector2 world_pos = CalculateVertexWorldPosition(relative_x, relative_z, base_world_position);
                // needed for smooth transitions between chunks, because margins are added to the biome maps so that the biome map sampling can be modified by noise 
                var uv_with_margin_included = new Vector2(uv_margin, uv_margin) + uv * (1f - 2f * uv_margin);
                float height = CalculateHeight(uv_with_margin_included, world_pos, biomes, biome_data);

                st.AddVertex(new(world_pos.X, height, world_pos.Y));
            }
        }

    }
}
