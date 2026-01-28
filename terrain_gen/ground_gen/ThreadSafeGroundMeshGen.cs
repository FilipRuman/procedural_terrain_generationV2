using Godot;
[Tool]
public partial class ThreadSafeGroundMeshGen : Node
{
    [Export] private TerrainAspectsSolver terrain_aspects_solver;
    [Export] private NoiseComponent high_frequency_noise;
    [Export] private NoiseComponent medium_frequency_noise;
    [Export] private NoiseComponent low_frequency_noise;
    [Export] private TerrainAspectEffectOnMesh moisture_effect;
    [Export] private TerrainAspectEffectOnMesh elevation_effect;
    [Export] private TerrainAspectEffectOnMesh temperature_effect;
    [Export] private TerrainAspectEffectOnMesh roughness_effect;


    public static void ApplyData(OutputData data, MeshInstance3D mesh_instance)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);

        GD.Print($"indices: {data.indices.Length}");
        GD.Print($"vertices: {data.vertices.Length}");
        arrays[(int)Mesh.ArrayType.Vertex] = data.vertices;
        arrays[(int)Mesh.ArrayType.Index] = data.indices;
        arrays[(int)Mesh.ArrayType.Normal] = data.normals;
        arrays[(int)Mesh.ArrayType.TexUV] = data.uvs;
        // arrays[(int)Mesh.ArrayType.Tangent] = data.tangents;


        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        mesh_instance.Mesh = mesh;
    }
    public struct OutputData
    {
        public Vector3[] vertices;
        public int[] indices;
        public Vector3[] normals;
        public Vector2[] uvs;
        public float[] tangents;
        public OutputData(
                 Vector3[] vertices,
                 int[] indices,
                 Vector3[] normals,
                 Vector2[] uvs,
                 float[] tangents
        )
        {

            this.vertices = vertices;
            this.indices = indices;
            this.normals = normals;
            this.uvs = uvs;
            this.tangents = tangents;
        }
    }
    public OutputData GenerateChunk(Vector2 base_world_position, Config config)
    {
        int[] indices = GenerateIndices(config);
        var triangle_count_per_dimension = config.triangle_count_per_dimension;

        int vertsPerRow = triangle_count_per_dimension + 1;
        int paddedWidth = vertsPerRow + 2;

        var vertices_padded = new Vector3[paddedWidth * paddedWidth];

        Vector3[] verticesPadded;
        Vector3[] vertices;
        Vector2[] uvs;
        GenerateUvAndVertex(base_world_position, config, out verticesPadded, out vertices, out uvs);

        var normals = GetNormals(config, verticesPadded);
        var tangents = GenerateTangents(vertices, normals, uvs, indices);


        return new OutputData(vertices, indices, normals, uvs, tangents);
    }
    private void GenerateUvAndVertex(Vector2 base_world_position, Config config,
                                     out Vector3[] verticesPadded, out Vector3[] vertices, out Vector2[] uvs)
    {
        int Q = config.triangle_count_per_dimension;
        int paddedWidth = Q + 2;

        verticesPadded = new Vector3[paddedWidth * paddedWidth];
        vertices = new Vector3[Q * Q];
        uvs = new Vector2[Q * Q];

        for (int x = -1; x < Q + 1; x++)
        {
            for (int z = -1; z < Q + 1; z++)
            {
                Vector2 worldPos =
                    new Vector2(x * config.triangle_size, z * config.triangle_size)
                    + base_world_position;

                float height = CalculateHeight(config, worldPos);
                Vector3 v = new(worldPos.X, height, worldPos.Y);

                verticesPadded[(x + 1) + (z + 1) * paddedWidth] = v;

                if (x < 0 || z < 0 || x >= Q || z >= Q)
                    continue;

                int i = x + z * Q;

                vertices[i] = v;
                uvs[i] = new Vector2(
                    x / (float)Q,
                    z / (float)Q
                );
            }
        }
    }

    private static Vector2 CalculateVertexWorldPosition(Config config, int relative_x, int relative_z, Vector2 base_world_position)
    {
        return new Vector2(relative_x * config.triangle_size, relative_z * config.triangle_size) + base_world_position;
    }

    private float CalculateHeight(Config config, Vector2 world_position)
    {
        var terrain_aspects = terrain_aspects_solver.SolveForPos(world_position);

        TerrainAspectEffectOnMesh.OutputData noise_amplitude_data = new();
        moisture_effect.AddEffectToOutput(terrain_aspects.moisture, ref noise_amplitude_data);
        elevation_effect.AddEffectToOutput(terrain_aspects.elevation, ref noise_amplitude_data);
        temperature_effect.AddEffectToOutput(terrain_aspects.temperature, ref noise_amplitude_data);
        roughness_effect.AddEffectToOutput(terrain_aspects.roughness, ref noise_amplitude_data);

        float output_height
             = high_frequency_noise.Sample(world_position) * noise_amplitude_data.high_freq_noise_amplitude
             + medium_frequency_noise.Sample(world_position) * noise_amplitude_data.medium_freq_noise_amplitude
             + low_frequency_noise.Sample(world_position) * noise_amplitude_data.low_freq_noise_amplitude
             + noise_amplitude_data.base_height;


        return output_height;
    }
    static Vector3[] GetNormals(Config config, Vector3[] verticesPadded)
    {
        int Q = config.triangle_count_per_dimension;       // main vertex count per dimension
        int paddedWidth = Q + 2;                           // width of padded vertex array
        Vector3[] normals = new Vector3[Q * Q];           // output normals for main vertices

        for (int x = 0; x < Q; x++)
        {
            for (int z = 0; z < Q; z++)
            {
                // padded indices
                int center = (x + 1) + (z + 1) * paddedWidth;
                int left = (x + 0) + (z + 1) * paddedWidth;
                int right = (x + 2) + (z + 1) * paddedWidth;
                int down = (x + 1) + (z + 0) * paddedWidth;
                int up = (x + 1) + (z + 2) * paddedWidth;

                // central difference for normal
                Vector3 n = new Vector3(
                    verticesPadded[left].Y - verticesPadded[right].Y,
                    2.0f,
                    verticesPadded[down].Y - verticesPadded[up].Y
                ).Normalized();

                // store normal in main vertex array
                normals[x + z * Q] = n;
            }
        }

        return normals;
    }

    /// TODO: Clean up
    public static float[] GenerateTangents(
        Vector3[] vertices,
        Vector3[] normals,
        Vector2[] uvs,
        int[] indices)
    {
        int vertexCount = vertices.Length;

        var tan1 = new Vector3[vertexCount];
        var tan2 = new Vector3[vertexCount];
        var tangents = new float[vertexCount * 4];

        // Accumulate tangents per triangle
        for (int i = 0; i < indices.Length; i += 3)
        {
            int i0 = indices[i];
            int i1 = indices[i + 1];
            int i2 = indices[i + 2];

            Vector3 v0 = vertices[i0];
            Vector3 v1 = vertices[i1];
            Vector3 v2 = vertices[i2];

            Vector2 w0 = uvs[i0];
            Vector2 w1 = uvs[i1];
            Vector2 w2 = uvs[i2];

            Vector3 e1 = v1 - v0;
            Vector3 e2 = v2 - v0;

            float x1 = w1.X - w0.X;
            float x2 = w2.X - w0.X;
            float y1 = w1.Y - w0.Y;
            float y2 = w2.Y - w0.Y;

            float r = x1 * y2 - x2 * y1;
            if (Mathf.Abs(r) < 1e-8f)
                continue;

            float invR = 1.0f / r;

            Vector3 sdir = (e1 * y2 - e2 * y1) * invR;
            Vector3 tdir = (e2 * x1 - e1 * x2) * invR;

            tan1[i0] += sdir;
            tan1[i1] += sdir;
            tan1[i2] += sdir;

            tan2[i0] += tdir;
            tan2[i1] += tdir;
            tan2[i2] += tdir;
        }

        // Orthonormalize and store
        for (int i = 0; i < vertexCount; i++)
        {
            Vector3 n = normals[i];
            Vector3 t = tan1[i];

            // Gram-Schmidt orthogonalization
            Vector3 tangent = (t - n * n.Dot(t)).Normalized();

            // Handedness
            float w = (n.Cross(t).Dot(tan2[i]) < 0.0f) ? -1.0f : 1.0f;

            int baseIndex = i * 4;
            tangents[baseIndex + 0] = tangent.X;
            tangents[baseIndex + 1] = tangent.Y;
            tangents[baseIndex + 2] = tangent.Z;
            tangents[baseIndex + 3] = w;
        }

        return tangents;
    }


    public static int[] GenerateIndices(Config config)
    {
        int Q = config.triangle_count_per_dimension;
        int vertsPerRow = Q;

        int[] indices = new int[(Q - 1) * (Q - 1) * 6];
        int idx = 0;

        for (int z = 0; z < Q - 1; z++)
        {
            for (int x = 0; x < Q - 1; x++)
            {
                int i = x + z * Q;

                indices[idx++] = i;
                indices[idx++] = i + 1;
                indices[idx++] = i + Q;

                indices[idx++] = i + Q + 1;
                indices[idx++] = i + Q;
                indices[idx++] = i + 1;
            }
        }

        return indices;
    }


    public struct Config
    {
        public int triangle_count_per_dimension;
        public float triangle_size;

        public Config(int size, int resolution)
        {
            size += 1;
            triangle_count_per_dimension = size * resolution;
            triangle_size = 1f / resolution;
        }
    }
}
