using System.Collections.Generic;
using Godot;
[Tool]
public partial class GroundMeshGen : MeshInstance3D
{
    [Export] Gradient influence_gradient;
    [Export] int resolution;

    private int triangle_count_per_dimension;
    private int triangle_size;
    public void Run(Biome[] biomes, BiomeGenerator.OutputData biome_data, int size)
    {
        size += 1;
        triangle_count_per_dimension = size /* * resolution */;
        triangle_size = 1/* size / resolution */;

        GD.Print("run");
        var arrayMesh = GenerateTerrainMesh(biomes, biome_data);
        Mesh = arrayMesh;

    }


    private ArrayMesh GenerateTerrainMesh(Biome[] biomes, BiomeGenerator.OutputData biome_data)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);


        GenerateVertexes(st, biomes, biome_data);

        GenerateIndexes(st);

        st.GenerateNormals();
        st.GenerateTangents();

        // TODO: Generate normals by hand to avoid seams.https://www.youtube.com/watch?v=NpeYTcS7n-M&t=432s
        // Also make uvs generated based on the possition of the tile in the real space not on the tile it sefl to avoid seams 


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


    private Vector2 RealPosition(uint x, uint z)
    {
        return new(x * triangle_size, z * triangle_size);
    }

    private float CalculateHeight(Vector2 uv, Vector2 real_pos, Biome[] biomes, BiomeGenerator.OutputData biome_data)
    {
        List<BiomeGenerator.OutputData.BiomeInfluenceOutput> biome_influences = biome_data.SampleBiomeDataForMesh(uv);
        var output = 0f;
        foreach (var biome_influence_data in biome_influences)
        {
            var biome = biomes[biome_influence_data.biome_type_index - 1];
            // TODO: Bake gradient
            output += influence_gradient.Sample(biome_influence_data.influence).R * biome.terrain_mesh_noise.Sample(real_pos);
        }
        return output;
    }

    private void GenerateVertexes(SurfaceTool st, Biome[] biomes, BiomeGenerator.OutputData biome_data)
    {
        for (uint x = 0; x < triangle_count_per_dimension; x++)
        {
            for (uint z = 0; z < triangle_count_per_dimension; z++)
            {

                var uv = new Vector2(x / (float)triangle_count_per_dimension, z / (float)triangle_count_per_dimension);
                st.SetUV(uv);

                Vector2 real_pos = RealPosition(x, z);
                float height = CalculateHeight(uv, real_pos, biomes, biome_data);

                st.AddVertex(new(real_pos.X,/*  height */1, real_pos.Y));
            }
        }

    }
}
