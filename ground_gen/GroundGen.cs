using Godot;
[Tool]
public partial class GroundGen : MeshInstance3D
{

    [Export] bool generate;
    [Export] float triangle_size;
    [Export] int triangle_count_per_dimension;
    [Export] Biome[] biomes;

    public override void _Process(double delta)
    {
        if (generate)
        {
            GD.Print("generating!");
            generate = false;
            var arrayMesh = GenerateTerrainMesh();
            Mesh = arrayMesh;
        }
        base._Process(delta);
    }


    private ArrayMesh GenerateTerrainMesh()
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);


        GenerateVertexes(st);
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

    private void GenerateVertexes(SurfaceTool st)
    {
        for (uint x = 0; x < triangle_count_per_dimension; x++)
        {
            for (uint z = 0; z < triangle_count_per_dimension; z++)
            {

                var uv = new Vector2(x / (float)triangle_count_per_dimension, z / (float)triangle_count_per_dimension);
                st.SetUV(uv);

                Vector2 real_pos = RealPosition(x, z);
                float height = Mathf.Lerp(biomes[0].noise.GetHeight(real_pos), biomes[1].noise.GetHeight(real_pos), uv.Y);

                st.AddVertex(new(real_pos.X, height, real_pos.Y));
            }
        }

    }
}
