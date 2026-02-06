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

    private int triangles_per_dimension;
    private float triangle_size;


    public void ApplyData(OutputData data, MeshInstance3D mesh_instance, CollisionShape3D collider)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);

        arrays[(int)Mesh.ArrayType.Vertex] = data.vertices;
        arrays[(int)Mesh.ArrayType.Index] = data.indices;
        arrays[(int)Mesh.ArrayType.Normal] = data.normals;
        arrays[(int)Mesh.ArrayType.TexUV] = data.uvs;
        arrays[(int)Mesh.ArrayType.Tangent] = data.tangents;

        HeightMapShape3D height_map = new();

        height_map.MapWidth = triangles_per_dimension;
        height_map.MapDepth = triangles_per_dimension;
        height_map.MapData = data.height_map;
        collider.Shape = height_map;
        collider.Scale = new Vector3(triangle_size, 1, triangle_size);
        collider.Position = data.chunk_base_pos;


        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        mesh_instance.Mesh = mesh;
    }
    public struct OutputData
    {
        public Vector3 chunk_base_pos;
        public Vector3[] vertices;
        public int[] indices;
        public Vector3[] normals;
        public Vector2[] uvs;
        public float[] tangents;
        public float[] height_map;
        public OutputData(
                 Vector3[] vertices,
                 int[] indices,
                 Vector3[] normals,
                 Vector2[] uvs,
                 float[] tangents,
                float[] height_map,
                Vector3 chunk_base_pos
        )
        {

            this.vertices = vertices;
            this.indices = indices;
            this.normals = normals;
            this.uvs = uvs;
            this.tangents = tangents;
            this.height_map = height_map;
            this.chunk_base_pos = chunk_base_pos;
        }
    }
    public OutputData GenerateChunk(Vector2 base_world_position, int resolution, int size)
    {
        triangles_per_dimension = resolution + 1;
        triangle_size = size / (float)resolution;

        Vector3[] verticesPadded;
        Vector3[] vertices;
        Vector2[] uvs;
        float[] height_map;
        GenerateUvAndVertex(base_world_position, out verticesPadded, out vertices, out uvs, out height_map);

        int[] indices = GenerateIndices();
        var normals = GetNormals(verticesPadded);
        var tangents = GenerateTangents(vertices, normals, uvs, indices);


        return new OutputData(vertices, indices, normals, uvs, tangents, height_map, new Vector3(base_world_position.X + size / 2f, 0, base_world_position.Y + size / 2f));
    }
    private void GenerateUvAndVertex(Vector2 base_world_position,
                                     out Vector3[] verticesPadded, out Vector3[] vertices, out Vector2[] uvs, out float[] height_map)
    {
        int paddedWidth = triangles_per_dimension + 2;

        verticesPadded = new Vector3[paddedWidth * paddedWidth];
        vertices = new Vector3[triangles_per_dimension * triangles_per_dimension];
        height_map = new float[triangles_per_dimension * triangles_per_dimension];

        uvs = new Vector2[triangles_per_dimension * triangles_per_dimension];

        for (int x = -1; x < triangles_per_dimension + 1; x++)
        {
            for (int z = -1; z < triangles_per_dimension + 1; z++)
            {
                Vector2 worldPos = new Vector2(x, z) * triangle_size + base_world_position;

                float height = CalculateHeight(worldPos);
                Vector3 vartex_pos = new(worldPos.X, height, worldPos.Y);

                verticesPadded[(x + 1) + (z + 1) * paddedWidth] = vartex_pos;

                if (x < 0 || z < 0 || x >= triangles_per_dimension || z >= triangles_per_dimension)
                    continue;

                int i = x + z * triangles_per_dimension;

                height_map[i] = height;
                vertices[i] = vartex_pos;
                uvs[i] = new Vector2(
                    x / (float)triangles_per_dimension,
                    z / (float)triangles_per_dimension
                );
            }
        }
    }

    private Vector2 CalculateVertexWorldPosition(int relative_x, int relative_z, Vector2 base_world_position)
    {
        return new Vector2(relative_x, relative_z) * triangle_size + base_world_position;
    }

    public float CalculateHeight(Vector2 world_position)
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

    public float CalculateHeight(Vector2 world_position, out TerrainAspectsSolver.TerrainAspects terrain_aspects)
    {
        terrain_aspects = terrain_aspects_solver.SolveForPos(world_position);

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

    Vector3[] GetNormals(Vector3[] verticesPadded)
    {
        int paddedWidth = triangles_per_dimension + 2;
        Vector3[] normals = new Vector3[triangles_per_dimension * triangles_per_dimension];

        for (int x = 0; x < triangles_per_dimension; x++)
        {
            for (int z = 0; z < triangles_per_dimension; z++)
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

                normals[x + z * triangles_per_dimension] = n;
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


    public int[] GenerateIndices()
    {

        int vertex_count = triangles_per_dimension - 1;
        int[] indices = new int[vertex_count * vertex_count * 6];
        int array_idx = 0;

        for (int z = 0; z < vertex_count; z++)
        {
            for (int x = 0; x < vertex_count; x++)
            {
                int vertex_idx = x + z * triangles_per_dimension;

                indices[array_idx++] = vertex_idx;
                indices[array_idx++] = vertex_idx + 1;
                indices[array_idx++] = vertex_idx + triangles_per_dimension;

                indices[array_idx++] = vertex_idx + triangles_per_dimension + 1;
                indices[array_idx++] = vertex_idx + triangles_per_dimension;
                indices[array_idx++] = vertex_idx + 1;
            }
        }

        return indices;
    }


}
