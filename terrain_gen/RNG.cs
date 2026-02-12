using Godot;
public static class RNG
{
        public static ulong seed_base;

        public static ulong GenerateSeed(Vector2 pos)
        {
                return seed_base ^
                (ulong)Mathf.FloorToInt(pos.X) * 73856093UL ^
                (ulong)Mathf.FloorToInt(pos.Y) * 19349663UL;
        }
        public static ulong GenerateSeed(Vector3 pos)
        {
                return seed_base ^
                (ulong)Mathf.FloorToInt(pos.Z) * 53876596UL ^
                (ulong)Mathf.FloorToInt(pos.X) * 73856093UL ^
                (ulong)Mathf.FloorToInt(pos.Y) * 19349663UL;
        }



        public static int Range(int min, int max)
        {
                return (int)GD.Randi() % (max - min) + min;
        }
        public static int Range(Vector2 seed, int min, int max)
        {
                GD.Seed(GenerateSeed(seed));
                return (int)GD.Randi() % (max - min) + min;
        }
        public static float Float(Vector2 seed)
        {
                GD.Seed(GenerateSeed(seed));
                return GD.Randf();
        }

        public static float Float(Vector3 seed)
        {
                GD.Seed(GenerateSeed(seed));
                return GD.Randf();
        }
        public static Vector2 Vector2(Vector3 seed)
        {
                GD.Seed(GenerateSeed(seed));
                return new(GD.Randf(), GD.Randf());
        }

        public static Vector3 Vector3(Vector3 seed)
        {

                GD.Seed(GenerateSeed(seed));
                return new(GD.Randf(), GD.Randf(), GD.Randf());
        }
        public static Vector2 Vector2(Vector2 seed)
        {
                GD.Seed(GenerateSeed(seed));
                return new(GD.Randf(), GD.Randf());
        }
}

