using System.Collections.Generic;
using System.Linq;
using Godot;
[Tool]
public partial class WaterGen : Node3D
{
	[Export] PackedScene LakeWater1x1;
	public void HandleSpawningForChunk(Vector2I mesh_chunk_world_pos, LakeSpawningData lake_spawning_data, Node3D parent_node)
	{
		GD.Print($"HandleSpawningForChunk, pos: {lake_spawning_data.pos}");

		var water_node = (Node3D)LakeWater1x1.Instantiate();
		parent_node.AddChild(water_node);

		water_node.Scale = lake_spawning_data.scale;
		water_node.Position = lake_spawning_data.pos;
	}
	public struct ChunkHeightGrid
	{
		public float[] grid;
		public uint grid_width;
		public float this[int x, int y]
		{
			get
			{
				return grid[x + y * grid_width];
			}
			set
			{
				grid[x + y * grid_width] = value;
			}
		}
		public ChunkHeightGrid(uint grid_width)
		{
			this.grid_width = grid_width;
			grid = new float[grid_width * grid_width];
		}
	}

	[Export] float lake_height;
	[Export] float river_start_height;
	[Export] uint mesh_chunks_per_river_chunk;
	[Export] uint height_checks_per_chunk_sqrt;
	[Export] uint height_checks_for_lake_system_sqrt;
	[Export] float water_lever_offset;
	public class LakeSpawningData
	{
		float min_x = float.MaxValue;
		float max_x = float.MinValue;

		float min_z = float.MaxValue;
		float max_z = float.MinValue;
		float water_height;
		public Vector3 scale;
		public Vector3 pos;

		public void handle_new_vertex(Vector3 vertex)
		{

			if (vertex.Y > water_height)
				return;
			// GD.Print("handle_new_vertex- is under the water_height");
			// there is a faster way
			min_x = Mathf.Min(vertex.X, min_x);
			max_x = Mathf.Max(vertex.X, max_x);
			min_z = Mathf.Min(vertex.Z, min_z);
			max_z = Mathf.Max(vertex.Z, max_z);
		}
		public LakeSpawningData(float water_height)
		{
			this.water_height = water_height;
		}
		public void FinishCalculation()
		{
			var length_x = (max_x - min_x) / 2f;
			var length_y = (max_z - min_z) / 2f;
			scale = new(length_x, 1, length_y);
			pos = new Vector3(min_x + length_x, water_height, min_z + length_y);
		}
	}
	public class OutputData
	{
		public Dictionary<Vector2I, LakeData> world_pos_lakes;
		public OutputData(Dictionary<Vector2I, LakeData> world_pos_lakes)
		{
			this.world_pos_lakes = world_pos_lakes;
		}
	}
	public OutputData GenerateCell(Vector2I base_pos, ThreadSafeGroundMeshGen mesh_gen, int mesh_chunk_size)
	{
		List<LakeData> lakes = new();
		List<Vector2I> river_start_points = new();

		ChunkHeightGrid grid = new(mesh_chunks_per_river_chunk);
		for (int x = 0; x < mesh_chunks_per_river_chunk; x++)
		{
			for (int y = 0; y < mesh_chunks_per_river_chunk; y++)
			{
				var chunk_base_pos = base_pos + new Vector2(x, y) * mesh_chunk_size;
				var height = AverageChunkHeight(mesh_chunk_size, chunk_base_pos, mesh_gen);
				grid[x, y] = height;
				AddChunkToLakesOrRivers(height, new(x, y), ref lakes, ref river_start_points);
			}
		}

		Dictionary<Vector2I, LakeData> world_pos_lakes = new();
		var connected_lakes = ConnectLakeTiles(lakes);
		foreach (var lake_system in connected_lakes)
		{
			foreach (var lake in lake_system.Value)
			{
				var world_pos = base_pos + lake.pos * mesh_chunk_size;
				world_pos_lakes.Add(world_pos, lake);
			}
		}

		return new(world_pos_lakes);
	}

	private float GetWatterLevelOfLakeSystem(List<LakeData> lake_system, int mesh_chunk_size, Vector2I world_base_pos, ThreadSafeGroundMeshGen mesh_gen)
	{
		// Find the lowest 'natural' point in all of the lakes in this system
		// subtract some value and return
		var distance_per_check = mesh_chunk_size / height_checks_for_lake_system_sqrt;
		var lowest_point = float.MaxValue;
		foreach (var lake in lake_system)
		{
			var chunk_base_pos = world_base_pos + lake.pos * mesh_chunk_size;
			for (int x = 0; x < height_checks_for_lake_system_sqrt; x++)
			{
				for (int y = 0; y < height_checks_for_lake_system_sqrt; y++)
				{
					var pos = chunk_base_pos + new Vector2(x, y) * distance_per_check;
					lowest_point = Mathf.Min(lowest_point, mesh_gen.CalculateHeight(pos));
				}
			}
		}
		return lowest_point - water_lever_offset;
	}


	// TODO: CleanUp
	private Dictionary<int, List<LakeData>> ConnectLakeTiles(List<LakeData> all_lakes)
	{
		// Build spatial lookup for O(1) neighbor checks
		var lake_map = all_lakes.ToDictionary(l => l.pos);

		// Find neighbors and connect
		foreach (var lake in all_lakes)
		{
			var neighbors = new[] {
			lake.pos + new Vector2I(1, 0),
			lake.pos + new Vector2I(-1, 0),
			lake.pos + new Vector2I(0, 1),
			lake.pos + new Vector2I(0, -1)
		};

			foreach (var neighbor_pos in neighbors)
			{
				if (lake_map.TryGetValue(neighbor_pos, out var neighbor))
				{
					if (!lake.connected_lakes.Contains(neighbor))
					{
						lake.connected_lakes.Add(neighbor);
						neighbor.connected_lakes.Add(lake);
					}
				}
			}
		}

		// Assign system IDs using flood fill
		int current_system_id = 0;
		foreach (var lake in all_lakes)
		{
			if (lake.lake_system_id == -1)
			{
				FloodFillSystemId(lake, current_system_id);
				current_system_id++;
			}
		}

		// Group by system ID
		return all_lakes
			.GroupBy(l => l.lake_system_id)
			.ToDictionary(g => g.Key, g => g.ToList());
	}

	private void FloodFillSystemId(LakeData start, int system_id)
	{
		var stack = new Stack<LakeData>();
		stack.Push(start);

		while (stack.Count > 0)
		{
			var current = stack.Pop();

			if (current.lake_system_id != -1)
				continue;

			current.lake_system_id = system_id;

			foreach (var neighbor in current.connected_lakes)
			{
				if (neighbor.lake_system_id == -1)
				{
					stack.Push(neighbor);
				}
			}
		}
	}
	private void SetConnectedLakeIdRecursive(LakeData lake, int id, HashSet<LakeData> done)
	{
		lake.lake_system_id = id;
		foreach (var connected in lake.connected_lakes)
		{
			if (done.Contains(connected))
				continue;
			connected.lake_system_id = id;
			done.Add(connected);
			SetConnectedLakeIdRecursive(connected, id, done);
		}
	}
	private static int ManhattanDistance(Vector2I a, Vector2I b)
	{
		return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
	}

	public class LakeData
	{
		public int lake_system_id = -1;
		public Vector2I pos;//?
		public float water_height;
		public List<LakeData> connected_lakes;
		public LakeData(Vector2I pos)
		{
			this.connected_lakes = new();
			this.pos = pos;
		}
	}
	private void AddChunkToLakesOrRivers(float height, Vector2I chunk, ref List<LakeData> lakes, ref List<Vector2I> river_start_points)
	{
		if (height < lake_height)
		{
			lakes.Add(new(chunk));
			return;
		}
		if (height > river_start_height)
		{
			river_start_points.Add(chunk);
		}

	}
	private float AverageChunkHeight(int mesh_chunk_size, Vector2 chunk_base_pos, ThreadSafeGroundMeshGen mesh_gen)
	{
		var sum = 0f;
		var distance_per_check = mesh_chunk_size / height_checks_per_chunk_sqrt;
		for (int x = 0; x < height_checks_per_chunk_sqrt; x++)
		{
			for (int y = 0; y < height_checks_per_chunk_sqrt; y++)
			{
				var pos = chunk_base_pos + new Vector2(x, y) * distance_per_check;
				sum += mesh_gen.CalculateHeight(pos);
			}
		}
		sum /= height_checks_per_chunk_sqrt * height_checks_per_chunk_sqrt;
		return sum;
	}


}
