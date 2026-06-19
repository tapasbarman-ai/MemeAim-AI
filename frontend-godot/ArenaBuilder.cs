using Godot;
using System;
using System.Collections.Generic;

public partial class ArenaBuilder : Node3D
{
    private static readonly Color ColorStone = new Color(0.35f, 0.35f, 0.38f);
    private static readonly Color ColorGrass = new Color(0.22f, 0.55f, 0.18f);
    private static readonly Color ColorWood = new Color(0.5f, 0.35f, 0.2f);
    private static readonly Color ColorBrick = new Color(0.6f, 0.25f, 0.2f);
    private static readonly Color ColorWater = new Color(0.1f, 0.45f, 0.8f, 0.7f);
    private static readonly Color ColorLeaves = new Color(0.12f, 0.4f, 0.12f);
    private static readonly Color ColorIron = new Color(0.6f, 0.6f, 0.65f);

    private StandardMaterial3D _matStone;
    private StandardMaterial3D _matGrass;
    private StandardMaterial3D _matWood;
    private StandardMaterial3D _matBrick;
    private StandardMaterial3D _matWater;
    private StandardMaterial3D _matLeaves;
    private StandardMaterial3D _matIron;

    private Node3D _voxelContainer;

    // Center of the arena in global coords
    private Vector3 _center = new Vector3(0, 0, -10);

    public override void _Ready()
    {
        GD.Print("Voxel Arena Builder: Constructing Minecraft/Voxel Arena...");

        // Setup materials with sharp voxel lighting (roughness = 0.9, flat shading optional)
        _matStone = CreateMaterial(ColorStone, 0.9f, 0.0f);
        _matGrass = CreateMaterial(ColorGrass, 0.9f, 0.0f);
        _matWood = CreateMaterial(ColorWood, 0.9f, 0.0f);
        _matBrick = CreateMaterial(ColorBrick, 0.9f, 0.0f);
        _matWater = CreateMaterial(ColorWater, 0.2f, 0.1f, true); // semi-transparent water
        _matLeaves = CreateMaterial(ColorLeaves, 0.95f, 0.0f);
        _matIron = CreateMaterial(ColorIron, 0.4f, 0.8f); // metallic look

        _voxelContainer = new Node3D();
        _voxelContainer.Name = "VoxelContainer";
        AddChild(_voxelContainer);

        // Hide or delete the old floor mesh to prevent overlap
        var oldFloorMesh = GetNodeOrNull<MeshInstance3D>("Floor/MeshInstance3D");
        if (oldFloorMesh != null)
        {
            oldFloorMesh.Visible = false;
        }

        // Build the Voxel Arena
        GenerateFloor();
        GenerateWalls();
        GenerateElevationsAndStairs();
        GenerateCrates();
        GenerateMemeBillboards();
    }

    private StandardMaterial3D CreateMaterial(Color color, float roughness, float metallic, bool transparent = false)
    {
        var mat = new StandardMaterial3D();
        mat.AlbedoColor = color;
        mat.Roughness = roughness;
        mat.Metallic = metallic;
        if (transparent)
        {
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        }
        return mat;
    }

    private void SpawnBlock(Vector3 localPos, Vector3 size, Material material)
    {
        var meshInstance = new MeshInstance3D();
        var boxMesh = new BoxMesh();
        boxMesh.Size = size;
        meshInstance.Mesh = boxMesh;
        meshInstance.MaterialOverride = material;
        meshInstance.Position = _center + localPos;
        _voxelContainer.AddChild(meshInstance);
    }

    private void GenerateFloor()
    {
        // Grid size: 32x32 blocks centered at (0, 0, 0) relative to _center
        for (int x = -16; x <= 16; x++)
        {
            for (int z = -16; z <= 16; z++)
            {
                float distSq = x * x + z * z;
                Vector3 pos = new Vector3(x, -0.5f, z);

                // 1. Central Fountain Pool (Radius = 3m)
                if (distSq < 9)
                {
                    // Water in the absolute center
                    if (distSq <= 2)
                    {
                        SpawnBlock(new Vector3(x, -0.7f, z), Vector3.One, _matWater);
                    }
                    else // Stone rim around fountain
                    {
                        SpawnBlock(new Vector3(x, -0.4f, z), Vector3.One, _matStone);
                    }
                }
                // 2. Axial Pathways (Brick paths running North-South, East-West)
                else if (Math.Abs(x) <= 2 || Math.Abs(z) <= 2)
                {
                    // Checkboard pattern on paths
                    Material pathMat = ((x + z) % 2 == 0) ? _matBrick : _matStone;
                    SpawnBlock(pos, Vector3.One, pathMat);
                }
                // 3. Grass lawn in corners
                else
                {
                    SpawnBlock(pos, Vector3.One, _matGrass);
                }
            }
        }

        // Central fountain spout base
        SpawnBlock(new Vector3(0, 0.1f, 0), new Vector3(0.8f, 1.2f, 0.8f), _matStone);
        // Red gem/fountain core on top
        var gemMat = new StandardMaterial3D();
        gemMat.AlbedoColor = new Color(1.0f, 0.1f, 0.1f);
        gemMat.EmissionEnabled = true;
        gemMat.Emission = new Color(1.0f, 0.1f, 0.1f);
        gemMat.EmissionEnergyMultiplier = 2.0f;
        SpawnBlock(new Vector3(0, 0.8f, 0), new Vector3(0.5f, 0.5f, 0.5f), gemMat);
    }

    private void GenerateWalls()
    {
        // Enclose the arena with 5m tall walls in a square pattern
        int wallDist = 16;

        for (int h = 0; h < 5; h++)
        {
            Material wallMat = (h == 0 || h == 4) ? _matBrick : _matStone;

            for (int i = -wallDist; i <= wallDist; i++)
            {
                // North Wall
                SpawnBlock(new Vector3(i, h, -wallDist), Vector3.One, wallMat);
                // South Wall
                SpawnBlock(new Vector3(i, h, wallDist), Vector3.One, wallMat);
                // West Wall
                SpawnBlock(new Vector3(-wallDist, h, i), Vector3.One, wallMat);
                // East Wall
                SpawnBlock(new Vector3(wallDist, h, i), Vector3.One, wallMat);
            }
        }

        // Tall voxel pillars (trees) in the corners
        int[] corners = { -15, 15 };
        foreach (int cx in corners)
        {
            foreach (int cz in corners)
            {
                // Trunk (Wood)
                for (int h = 0; h < 4; h++)
                {
                    SpawnBlock(new Vector3(cx, h, cz), new Vector3(0.8f, 1.0f, 0.8f), _matWood);
                }
                // Leaves (Green foliage)
                for (int lx = -1; lx <= 1; lx++)
                {
                    for (int lz = -1; lz <= 1; lz++)
                    {
                        for (int ly = 4; ly <= 5; ly++)
                        {
                            SpawnBlock(new Vector3(cx + lx, ly, cz + lz), Vector3.One, _matLeaves);
                        }
                    }
                }
            }
        }
    }

    private void GenerateElevationsAndStairs()
    {
        // Wooden gallery / balcony along North and South walls (at height Y=2, width=2)
        for (int x = -15; x <= 15; x++)
        {
            // Skip the corners and central path openings slightly
            if (Math.Abs(x) <= 2) continue;

            // North wooden platform
            SpawnBlock(new Vector3(x, 1.5f, -14), Vector3.One, _matWood);
            // South wooden platform
            SpawnBlock(new Vector3(x, 1.5f, 14), Vector3.One, _matWood);

            // Add wooden supporting pillars down to the ground
            if (x % 4 == 0)
            {
                SpawnBlock(new Vector3(x, 0.5f, -14), new Vector3(0.4f, 1.0f, 0.4f), _matWood);
                SpawnBlock(new Vector3(x, 0.5f, 14), new Vector3(0.4f, 1.0f, 0.4f), _matWood);
            }
        }

        // Wooden stairs to access the platforms
        // Stairs at North-East: x=11, 12, 13
        SpawnBlock(new Vector3(13, 0.2f, -13), new Vector3(1.0f, 0.4f, 1.0f), _matWood);
        SpawnBlock(new Vector3(12, 0.6f, -13), new Vector3(1.0f, 0.8f, 1.0f), _matWood);
        SpawnBlock(new Vector3(11, 1.0f, -13), new Vector3(1.0f, 1.2f, 1.0f), _matWood);

        // Stairs at South-West: x=-13, -12, -11
        SpawnBlock(new Vector3(-13, 0.2f, 13), new Vector3(1.0f, 0.4f, 1.0f), _matWood);
        SpawnBlock(new Vector3(-12, 0.6f, 13), new Vector3(1.0f, 0.8f, 1.0f), _matWood);
        SpawnBlock(new Vector3(-11, 1.0f, 13), new Vector3(1.0f, 1.2f, 1.0f), _matWood);
    }

    private void GenerateCrates()
    {
        Random rand = new Random(1337); // stable seed for generation consistency

        // Scatter 12 voxel crates around the arena floor
        for (int i = 0; i < 12; i++)
        {
            // Keep them away from center fountain
            float x = (float)(rand.NextDouble() * 20.0 - 10.0);
            float z = (float)(rand.NextDouble() * 20.0 - 10.0);

            // Avoid spawning in a 4m circle around the fountain
            if (x * x + z * z < 25)
            {
                x += Math.Sign(x) * 4.0f;
                z += Math.Sign(z) * 4.0f;
            }

            Vector3 size = new Vector3(1.2f, 1.2f, 1.2f);
            Vector3 pos = new Vector3(x, 0.1f, z);

            // Alternate between wood boxes and metal containers
            Material mat = (i % 3 == 0) ? _matIron : _matWood;
            SpawnBlock(pos, size, mat);

            // Draw crate strapping lines using minor overlay mesh
            if (mat == _matWood)
            {
                // A slightly smaller inner dark wood block to simulate detail
                SpawnBlock(pos + new Vector3(0, 0.02f, 0), size * 0.95f, _matStone);
            }
        }
    }

    private void GenerateMemeBillboards()
    {
        // 4 Large meme screens hanging high on the North, South, East, and West walls
        var screens = new List<(Vector3 pos, Vector3 size, Vector3 rotation, string texturePath)>
        {
            // North Screen
            (new Vector3(0, 3.2f, -15.4f), new Vector3(6f, 3f, 0.1f), new Vector3(0, 0, 0), "res://gifs/angry_businessman.png"),
            // South Screen
            (new Vector3(0, 3.2f, 15.4f), new Vector3(6f, 3f, 0.1f), new Vector3(0, 180, 0), "res://gifs/evil_clown.png"),
            // West Screen
            (new Vector3(-15.4f, 3.2f, 0), new Vector3(0.1f, 3f, 6f), new Vector3(0, 90, 0), "res://gifs/banana_gun.png"),
            // East Screen
            (new Vector3(15.4f, 3.2f, 0), new Vector3(0.1f, 3f, 6f), new Vector3(0, -90, 0), "res://gifs/dancing_skeleton.png")
        };

        foreach (var screen in screens)
        {
            // 1. Black outer frame
            var frameMesh = new MeshInstance3D();
            var frameBox = new BoxMesh();
            // Frame is slightly larger than screen
            frameBox.Size = screen.size + new Vector3(0.4f, 0.4f, 0.1f);
            frameMesh.Mesh = frameBox;
            
            var frameMat = new StandardMaterial3D();
            frameMat.AlbedoColor = new Color(0.1f, 0.1f, 0.1f); // Dark black frame
            frameMat.Roughness = 0.8f;
            frameMesh.MaterialOverride = frameMat;
            frameMesh.Position = _center + screen.pos;
            frameMesh.RotationDegrees = screen.rotation;
            _voxelContainer.AddChild(frameMesh);

            // 2. Screen Display Texture
            var displayMesh = new MeshInstance3D();
            var displayBox = new BoxMesh();
            displayBox.Size = screen.size;
            displayMesh.Mesh = displayBox;

            var displayMat = new StandardMaterial3D();
            if (ResourceLoader.Exists(screen.texturePath))
            {
                displayMat.AlbedoTexture = GD.Load<Texture2D>(screen.texturePath);
                displayMat.Roughness = 0.5f;
            }
            else
            {
                displayMat.AlbedoColor = new Color(0.2f, 0.2f, 0.3f); // Placeholder blue screen if texture missing
            }
            displayMesh.MaterialOverride = displayMat;
            
            // Offset slightly forward based on orientation to prevent z-fighting
            Vector3 offset = Vector3.Zero;
            if (screen.rotation.Y == 0) offset = new Vector3(0, 0, 0.06f);
            else if (screen.rotation.Y == 180) offset = new Vector3(0, 0, -0.06f);
            else if (screen.rotation.Y == 90) offset = new Vector3(0.06f, 0, 0);
            else if (screen.rotation.Y == -90) offset = new Vector3(-0.06f, 0, 0);

            displayMesh.Position = _center + screen.pos + offset;
            displayMesh.RotationDegrees = screen.rotation;
            _voxelContainer.AddChild(displayMesh);
        }
    }
}
