using Godot;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public partial class PlayerController : Camera3D
{
    [Export] public float MouseSensitivity = 0.002f;
    [Export] public string BackendUrl = "http://localhost:8080";
    
    // Gameplay stats
    public string UserID = "33333333-3333-3333-3333-333333333333";
    public string Username = "AimMaster";
    public string ActiveMatchID = "";

    // Public so HUD.cs can read them live
    public int _score = 0;
    public int _totalShots = 0;
    public int _hits = 0;
    public int _misses = 0;
    public int _civilianHits = 0;
    public int _headshots = 0;
    private long _matchStartTime = 0;
    public string _currentRule = "Shoot Angry Faces!";
    
    // Telemetry storage
    private List<Dictionary<string, object>> _telemetryEvents = new List<Dictionary<string, object>>();
    private readonly System.Net.Http.HttpClient _httpClient = new System.Net.Http.HttpClient();

    // Weapon visual variables
    private Node3D _gunContainer;
    private Vector3 _gunDefaultPosition = new Vector3(0.3f, -0.25f, -0.6f);
    private float _recoilOffset = 0f;
    private MeshInstance3D _muzzleFlash;
    private OmniLight3D _muzzleLight;
    private float _flashTimer = 0f;

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Captured;
        _matchStartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        GD.Print("PlayerController initialized. Aim training started!");

        // Construct 3D Weapon Model programmatically as a child of the Camera3D node
        _gunContainer = new Node3D();
        _gunContainer.Name = "GunContainer";
        _gunContainer.Position = _gunDefaultPosition;
        AddChild(_gunContainer);

        // Gun Barrel
        var barrel = new MeshInstance3D();
        barrel.Name = "GunBarrel";
        var boxMesh = new BoxMesh();
        boxMesh.Size = new Vector3(0.08f, 0.08f, 0.5f);
        barrel.Mesh = boxMesh;
        
        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0.18f, 0.18f, 0.22f);
        mat.Metallic = 0.85f;
        mat.Roughness = 0.15f;
        barrel.MaterialOverride = mat;
        barrel.Position = new Vector3(0, 0, -0.25f);
        _gunContainer.AddChild(barrel);

        // Gun Handle
        var handle = new MeshInstance3D();
        handle.Name = "GunHandle";
        var handleMesh = new BoxMesh();
        handleMesh.Size = new Vector3(0.07f, 0.15f, 0.07f);
        handle.Mesh = handleMesh;
        handle.MaterialOverride = mat;
        handle.Position = new Vector3(0, -0.1f, -0.1f);
        handle.RotationDegrees = new Vector3(-15, 0, 0);
        _gunContainer.AddChild(handle);

        // Muzzle Flash Mesh
        _muzzleFlash = new MeshInstance3D();
        _muzzleFlash.Name = "MuzzleFlashMesh";
        var sphere = new SphereMesh();
        sphere.Radius = 0.08f;
        sphere.Height = 0.16f;
        _muzzleFlash.Mesh = sphere;
        
        var flashMat = new StandardMaterial3D();
        flashMat.AlbedoColor = new Color(1.0f, 0.85f, 0.2f);
        flashMat.EmissionEnabled = true;
        flashMat.Emission = new Color(1.0f, 0.85f, 0.2f);
        flashMat.EmissionEnergyMultiplier = 4f;
        _muzzleFlash.MaterialOverride = flashMat;
        _muzzleFlash.Position = new Vector3(0, 0, -0.5f);
        _muzzleFlash.Visible = false;
        _gunContainer.AddChild(_muzzleFlash);

        // Muzzle Flash Light
        _muzzleLight = new OmniLight3D();
        _muzzleLight.Name = "MuzzleFlashLight";
        _muzzleLight.LightColor = new Color(1.0f, 0.85f, 0.2f);
        _muzzleLight.LightEnergy = 3.0f;
        _muzzleLight.OmniRange = 4.0f;
        _muzzleLight.Position = new Vector3(0, 0, -0.6f);
        _muzzleLight.Visible = false;
        _gunContainer.AddChild(_muzzleLight);
    }

    public override void _Input(InputEvent @event)
    {
        // FPS mouse look
        if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            RotateY(-mouseMotion.Relative.X * MouseSensitivity);
            
            // Limit vertical rotation to prevent flipping
            float change = -mouseMotion.Relative.Y * MouseSensitivity;
            float currentRotationX = Rotation.X;
            float newRotationX = Mathf.Clamp(currentRotationX + change, -Mathf.Pi / 2.2f, Mathf.Pi / 2.2f);
            Rotation = new Vector3(newRotationX, Rotation.Y, Rotation.Z);
        }

        // Shooting input
        if (@event.IsActionPressed("shoot") || (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left && mouseButton.Pressed))
        {
            FireWeapon();
        }

        // Esc to release mouse
        if (@event.IsActionPressed("ui_cancel"))
        {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured 
                ? Input.MouseModeEnum.Visible 
                : Input.MouseModeEnum.Captured;
        }
    }

    public override void _Process(double delta)
    {
        // Smoothly return the gun from recoil position
        if (_gunContainer != null)
        {
            _recoilOffset = Mathf.Lerp(_recoilOffset, 0f, (float)delta * 16f);
            _gunContainer.Position = _gunDefaultPosition + new Vector3(0, 0, _recoilOffset);
        }

        // Muzzle flash duration timer
        if (_flashTimer > 0f)
        {
            _flashTimer -= (float)delta;
            if (_flashTimer <= 0f)
            {
                if (_muzzleFlash != null) _muzzleFlash.Visible = false;
                if (_muzzleLight != null) _muzzleLight.Visible = false;
            }
        }
    }

    private void FireWeapon()
    {
        _totalShots++;
        
        // Trigger Recoil and Muzzle Flash
        _recoilOffset = 0.15f;
        _flashTimer = 0.06f;
        if (_muzzleFlash != null) _muzzleFlash.Visible = true;
        if (_muzzleLight != null) _muzzleLight.Visible = true;
        
        // Shoot raycast from screen center
        Vector2 screenCenter = GetViewport().GetVisibleRect().Size / 2;
        Vector3 origin = ProjectRayOrigin(screenCenter);
        Vector3 normal = ProjectRayNormal(screenCenter);
        
        var spaceState = GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(origin, origin + normal * 100f);
        query.CollideWithAreas = true; // Crucial: allow raycast to hit Area3D targets!
        var result = spaceState.IntersectRay(query);
        
        bool isHit = false;
        string hitTargetID = "";
        double targetSpeed = 0.0;
        Vector2 targetDir = Vector2.Zero;
        bool isCivilian = false;

        if (result.Count > 0)
        {
            isHit = true;
            var collider = result["collider"].As<Node>();
            
            // Retrieve target state — cast to MemeTarget for direct field access
            if (collider is MemeTarget memeTarget)
            {
                hitTargetID = memeTarget.TargetID;
                targetSpeed = memeTarget.Speed;
                Vector3 dir3D = memeTarget.Direction;
                targetDir = new Vector2(dir3D.X, dir3D.Z);
                isCivilian = memeTarget.IsCivilian;

                bool isHeadshot = memeTarget.OnHit();
                _hits++;
                if (isCivilian)
                {
                    _civilianHits++;
                    _score = Math.Max(0, _score - 500);
                    GD.Print($"Friendly hit! Penalty -500. Score: {_score}");
                }
                else
                {
                    _score += isHeadshot ? 150 : 100;
                    if (isHeadshot) _headshots++;
                    GD.Print($"Hit: {memeTarget.TargetName}. Score: {_score}");
                }
            }
            else
            {
                // Hit wall/floor — miss penalty
                _misses++;
                _score = Math.Max(0, _score - 50);
            }
        }
        else
        {
            _misses++;
            _score = Math.Max(0, _score - 50);
        }

        // Record telemetry event
        var telemetryEvent = new Dictionary<string, object>
        {
            { "timestamp_ms", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
            { "is_hit", isHit },
            { "coordinate_x", screenCenter.X },
            { "coordinate_y", screenCenter.Y },
            { "target_gif_id", string.IsNullOrEmpty(hitTargetID) ? null : hitTargetID },
            { "target_speed", targetSpeed },
            { "target_direction_x", targetDir.X },
            { "target_direction_y", targetDir.Y },
            { "active_rule", _currentRule },
            { "is_civilian", isCivilian }
        };
        _telemetryEvents.Add(telemetryEvent);
    }

    public void UpdateCurrentRule(string ruleName)
    {
        _currentRule = ruleName;
        GD.Print($"Rule changed to: {_currentRule}");
    }

    public async Task EndMatchAndSubmit()
    {
        GD.Print("Match ended. Submitting stats & telemetry to Go Backend...");
        
        long endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        double accuracy = _totalShots > 0 ? (double)_hits / _totalShots : 0.0;
        int reactionTime = _hits > 0 ? (int)((endTime - _matchStartTime) / _hits) : 0;

        // 1. Submit Match History
        var matchData = new
        {
            user_id = UserID,
            username = Username,
            score = _score,
            accuracy = accuracy,
            reaction_time_ms = reactionTime,
            headshot_count = _headshots,
            miss_count = _misses,
            civilian_hits = _civilianHits,
            mode = "meme_hunter"
        };

        try
        {
            string matchJson = JsonSerializer.Serialize(matchData);
            var content = new StringContent(matchJson, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _httpClient.PostAsync($"{BackendUrl}/api/match/submit", content);
            
            if (response.IsSuccessStatusCode)
            {
                string resBody = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<Dictionary<string, string>>(resBody);
                ActiveMatchID = result["match_id"];
                GD.Print($"Match history registered successfully. Match ID: {ActiveMatchID}");

                // 2. Submit Telemetry Events in Batch
                if (_telemetryEvents.Count > 0)
                {
                    var telemetryData = new
                    {
                        match_id = ActiveMatchID,
                        events = _telemetryEvents
                    };
                    
                    string telemetryJson = JsonSerializer.Serialize(telemetryData);
                    var teleContent = new StringContent(telemetryJson, Encoding.UTF8, "application/json");
                    HttpResponseMessage teleResponse = await _httpClient.PostAsync($"{BackendUrl}/api/telemetry/submit", teleContent);
                    
                    if (teleResponse.IsSuccessStatusCode)
                    {
                        GD.Print($"Submitted {_telemetryEvents.Count} telemetry events to backend successfully.");
                    }
                    else
                    {
                        GD.Print($"Failed to submit telemetry: {teleResponse.StatusCode}");
                    }
                }
            }
            else
            {
                GD.Print($"Failed to submit match history: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            GD.Print($"Error connecting to backend API: {ex.Message}");
        }
    }
}
