using Godot;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

public partial class TargetSpawner : Node3D
{
    [Export] public string BackendUrl = "http://localhost:8080";
    [Export] public float RuleIntervalSeconds = 15.0f;
    [Export] public int MaxActiveTargets = 5;      // total on screen at once
    [Export] public int MaxEnemies   = 3;          // max enemies at once
    [Export] public int MaxCivilians = 2;          // max civilians at once
    [Export] public float SpawnIntervalSeconds = 2.0f; // gap between individual spawns
    
    private readonly System.Net.Http.HttpClient _httpClient = new System.Net.Http.HttpClient();
    private List<Dictionary<string, object>> _availableTargets   = new List<Dictionary<string, object>>();
    private List<Dictionary<string, object>> _enemyTargets      = new List<Dictionary<string, object>>();
    private List<Dictionary<string, object>> _civilianTargets   = new List<Dictionary<string, object>>();
    private string _activeRuleType = "";
    private string _activeRuleTitle = "";
    private string _activeRuleDescription = "";
    private float _timeSinceLastRuleChange = 0.0f;
    private float _timeSinceLastSpawn = 0.0f;

    private PlayerController _player;
    private HUD _hud;

    public override void _Ready()
    {
        try { _player = GetNode<PlayerController>("../PlayerCamera"); }
        catch { GD.PrintErr("TargetSpawner: Could not find PlayerCamera node"); }

        try { _hud = GetNode<HUD>("../HUD"); }
        catch { GD.PrintErr("TargetSpawner: Could not find HUD node"); }

        FetchNewRoundConfig();
    }

    public override void _Process(double delta)
    {
        _timeSinceLastRuleChange += (float)delta;
        _timeSinceLastSpawn      += (float)delta;
        
        if (_timeSinceLastRuleChange >= RuleIntervalSeconds)
        {
            FetchNewRoundConfig();
            _timeSinceLastRuleChange = 0.0f;
        }

        // Count active enemies vs civilians currently in scene
        int activeEnemies = 0, activeCivilians = 0;
        foreach (Node child in GetChildren())
        {
            if (child is MemeTarget t)
            {
                if (t.IsCivilian) activeCivilians++;
                else              activeEnemies++;
            }
        }

        // Spawn on interval if we still need more targets
        bool needEnemy    = activeEnemies   < MaxEnemies;
        bool needCivilian = activeCivilians < MaxCivilians;

        if (_timeSinceLastSpawn >= SpawnIntervalSeconds && (needEnemy || needCivilian))
        {
            if (_availableTargets.Count > 0)
            {
                SpawnTarget(preferCivilian: !needEnemy && needCivilian);
                _timeSinceLastSpawn = 0.0f;
            }
        }
    }

    private async void FetchNewRoundConfig()
    {
        GD.Print("Fetching new round rules and meme collection...");
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"{BackendUrl}/api/game/round");
            if (response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                _activeRuleType = root.GetProperty("rule_type").GetString();
                _activeRuleTitle = root.GetProperty("rule_title").GetString();
                _activeRuleDescription = root.GetProperty("rule_description").GetString();

                // Propagate rule change to player controller for telemetry logging
                if (_player != null)
                    _player.UpdateCurrentRule(_activeRuleTitle);

                // Notify HUD to flash the rule change effect
                if (_hud != null)
                    _hud.OnRuleChanged(_activeRuleTitle);

                GD.Print($"[Active Rule] {_activeRuleTitle}: {_activeRuleDescription}");

                // Parse targets list — split into enemies & civilians
                _availableTargets.Clear();
                _enemyTargets.Clear();
                _civilianTargets.Clear();

                var targetsJson = root.GetProperty("targets");
                foreach (var targetElement in targetsJson.EnumerateArray())
                {
                    var targetDict = new Dictionary<string, object>
                    {
                        { "id",           targetElement.GetProperty("id").GetString() },
                        { "name",         targetElement.GetProperty("name").GetString() },
                        { "storage_path", targetElement.GetProperty("storage_path").GetString() }
                    };

                    var tagsDict = new Dictionary<string, bool>();
                    foreach (var tagProp in targetElement.GetProperty("tags").EnumerateObject())
                        tagsDict[tagProp.Name] = tagProp.Value.GetBoolean();
                    targetDict.Add("tags", tagsDict);

                    _availableTargets.Add(targetDict);

                    bool friendly = tagsDict.ContainsKey("friendly") && tagsDict["friendly"];
                    if (friendly) _civilianTargets.Add(targetDict);
                    else          _enemyTargets.Add(targetDict);
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error fetching round configuration: {ex.Message}. Using default rules.");
            _activeRuleType = "shoot_enemy";
            _activeRuleTitle = "Shoot Angry Faces!";
            _activeRuleDescription = "Shoot enemy targets. Avoid animals and grandmas!";
            if (_player != null) _player.UpdateCurrentRule(_activeRuleTitle);
            if (_hud != null) _hud.OnRuleChanged(_activeRuleTitle);
        }
    }

    private void SpawnTarget(bool preferCivilian = false)
    {
        List<Dictionary<string, object>> pool;
        if (preferCivilian && _civilianTargets.Count > 0)
            pool = _civilianTargets;
        else if (!preferCivilian && _enemyTargets.Count > 0)
            pool = _enemyTargets;
        else
            pool = _availableTargets;  // fallback to any

        if (pool.Count == 0) return;

        Random rand = new Random();
        var targetData = pool[rand.Next(pool.Count)];

        // Create new MemeTarget
        var memeTarget = new MemeTarget();
        memeTarget.TargetID = (string)targetData["id"];
        memeTarget.TargetName = (string)targetData["name"];
        
        // Storage path from backend: e.g. "gifs/giphy_target_1.gif"
        string storagePath = (string)targetData["storage_path"];
        
        var tags = (Dictionary<string, bool>)targetData["tags"];
        memeTarget.Tags = tags;
        
        // Define if target is civilian or enemy
        bool isFriendly = tags.ContainsKey("friendly") && tags["friendly"];
        memeTarget.IsCivilian = isFriendly;

        // Enable collision detection — layer 1, mask 1 (same as environment)
        memeTarget.CollisionLayer = 1;
        memeTarget.CollisionMask = 1;

        // Custom speed scaling
        memeTarget.Speed = rand.NextDouble() * 4.0 + 2.0; // Speed range 2.0 - 6.0

        // Random starting position inside the 3D boundary box
        float x = (float)(rand.NextDouble() * 22.0 - 11.0);
        float y = (float)(rand.NextDouble() * 4.0 + 1.5f);
        float z = (float)(rand.NextDouble() * 14.0 - 20.0);
        memeTarget.Position = new Vector3(x, y, z);

        // Derive the PNG filename from the storage_path (e.g. "gifs/giphy_target_1.gif" → "giphy_target_1.png")
        string storageFilename = System.IO.Path.GetFileNameWithoutExtension(storagePath); // e.g. "giphy_target_1"
        string pngResPath = $"res://gifs/{storageFilename}.png";

        // Also try the name directly as fallback (for older robohash assets)
        string namePng = $"res://gifs/{memeTarget.TargetName.Replace(".gif", ".png")}";

        string resPath = ResourceLoader.Exists(pngResPath) ? pngResPath
                       : ResourceLoader.Exists(namePng)  ? namePng
                       : "";

        // Randomly assign visual base vehicle
        var visualTypes = Enum.GetValues(typeof(MemeTarget.TargetVisualType));
        memeTarget.VisualType = (MemeTarget.TargetVisualType)visualTypes.GetValue(rand.Next(visualTypes.Length));

        // Match the movement behavior to the visual type
        switch (memeTarget.VisualType)
        {
            case MemeTarget.TargetVisualType.Drone:
                memeTarget.MovementType = MemeTarget.TargetMovementType.Hover;
                break;
            case MemeTarget.TargetVisualType.Balloon:
                memeTarget.MovementType = MemeTarget.TargetMovementType.Float;
                break;
            case MemeTarget.TargetVisualType.Hoverboard:
                memeTarget.MovementType = MemeTarget.TargetMovementType.Slide;
                break;
            case MemeTarget.TargetVisualType.UFO:
                memeTarget.MovementType = MemeTarget.TargetMovementType.Erratic;
                break;
        }

        memeTarget.TexturePath = resPath;
        AddChild(memeTarget);
    }
}
