using Godot;
using System;
using System.Collections.Generic;

public partial class MemeTarget : Area3D
{
    public enum TargetVisualType
    {
        Drone,
        Balloon,
        Hoverboard,
        UFO
    }

    public enum TargetMovementType
    {
        Float,
        Hover,
        Slide,
        Erratic
    }

    // --- Data fields set by spawner ---
    public string TargetID   = "";
    public string TargetName = "";
    public string TexturePath = ""; // e.g. "res://gifs/giphy_target_1.png"
    public double Speed    = 2.5;
    public Vector3 Direction = Vector3.Right;
    public bool IsCivilian   = false;
    public Dictionary<string, bool> Tags = new Dictionary<string, bool>();

    public TargetVisualType VisualType = TargetVisualType.Drone;
    public TargetMovementType MovementType = TargetMovementType.Hover;

    // --- Civilian lifetime ---
    private const float CivilianLifetime = 3.5f;
    private const float FadeStartAt      = 2.5f;
    private float _aliveTimer = 0f;

    // --- Movement boundaries (inside the voxel arena) ---
    private Vector3 _minBoundary = new Vector3(-11f, 1.2f, -22f);
    private Vector3 _maxBoundary = new Vector3(11f, 5f, -6f);
    private Vector3 _velocity;

    // --- Animations and pivots ---
    private Node3D _visualRoot;
    private List<MeshInstance3D> _droneRotors = new List<MeshInstance3D>();
    private MeshInstance3D _ufoRing;
    private float _bobTimer = 0f;
    private float _bobFreq = 2.2f;
    private float _bobAmp = 0.08f;

    // Erratic movement variables
    private float _erraticTimer = 0f;
    private bool _isDashing = false;
    private Vector3 _dashDirection;

    // GIF animation variables
    private List<Texture2D> _frames = new List<Texture2D>();
    private int _currentFrame = 0;
    private float _frameTimer = 0f;
    private float _frameDuration = 0.06f; // ~16 FPS standard GIF speed
    private Sprite3D _sprite;

    // Colors
    private static readonly Color ColEnemy   = new Color(0.85f, 0.15f, 0.1f);
    private static readonly Color ColCivilian = new Color(0.1f, 0.6f, 0.25f);
    private static readonly Color ColDark    = new Color(0.12f, 0.12f, 0.14f);

    public override void _Ready()
    {
        Random rand = new Random();
        _bobFreq = (float)(rand.NextDouble() * 0.8 + 1.8);

        // Randomise starting direction
        float angle = (float)(rand.NextDouble() * Math.PI * 2);
        Direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)).Normalized();
        _velocity = Direction * (float)Speed;

        Color themeColor = IsCivilian ? ColCivilian : ColEnemy;

        // Root node that handles tilting, rotation, and animation
        _visualRoot = new Node3D();
        _visualRoot.Name = "VisualRoot";
        AddChild(_visualRoot);

        // 1. Build the specific visual model base
        switch (VisualType)
        {
            case TargetVisualType.Drone:
                BuildDrone(themeColor);
                break;
            case TargetVisualType.Balloon:
                BuildBalloon(themeColor);
                break;
            case TargetVisualType.Hoverboard:
                BuildHoverboard(themeColor);
                break;
            case TargetVisualType.UFO:
                BuildUFO(themeColor);
                break;
        }

        // 2. Meme Face Sprite3D
        var sprite = new Sprite3D();
        sprite.Name = "MemeFaceSprite";
        sprite.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        sprite.PixelSize = 0.005f; // fits ~1.28m wide for a 256px texture
        _sprite = sprite;

        // Load frames if they exist
        if (!string.IsNullOrEmpty(TexturePath))
        {
            string basePath = TexturePath.Substring(0, TexturePath.LastIndexOf('.')); // e.g. "res://gifs/giphy_target_1"
            int frameIdx = 0;
            while (true)
            {
                string framePath = $"{basePath}_{frameIdx}.png";
                if (ResourceLoader.Exists(framePath))
                {
                    try
                    {
                        var frameTex = GD.Load<Texture2D>(framePath);
                        if (frameTex != null)
                        {
                            _frames.Add(frameTex);
                        }
                    }
                    catch (Exception ex)
                    {
                        GD.PrintErr($"Error loading frame {framePath}: {ex.Message}");
                    }
                    frameIdx++;
                }
                else
                {
                    break;
                }
            }

            // Fallback to static texture if no animation frames exist
            if (_frames.Count == 0 && ResourceLoader.Exists(TexturePath))
            {
                var staticTex = GD.Load<Texture2D>(TexturePath);
                if (staticTex != null)
                {
                    _frames.Add(staticTex);
                }
            }
        }

        // Apply initial texture
        if (_frames.Count > 0)
        {
            _sprite.Texture = _frames[0];
        }

        // Adjust position of the face relative to the mount point of each base model
        switch (VisualType)
        {
            case TargetVisualType.Drone:
                sprite.Position = new Vector3(0, 0.45f, 0);
                break;
            case TargetVisualType.Balloon:
                sprite.Position = new Vector3(0, -0.7f, 0);
                break;
            case TargetVisualType.Hoverboard:
                sprite.Position = new Vector3(0, 0.4f, 0);
                break;
            case TargetVisualType.UFO:
                sprite.Position = new Vector3(0, 0.25f, 0);
                break;
        }
        _visualRoot.AddChild(sprite);

        // 3. Build top status label
        BuildLabel();

        // 4. Build collision shape
        BuildCollision();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // ── Civilian auto-despawn ───────────────────────────────────
        if (IsCivilian)
        {
            _aliveTimer += dt;
            if (_aliveTimer >= CivilianLifetime) { QueueFree(); return; }

            if (_aliveTimer >= FadeStartAt)
            {
                float t = (_aliveTimer - FadeStartAt) / (CivilianLifetime - FadeStartAt);
                FadeChildren(1f - Mathf.Clamp(t, 0f, 1f));
            }
        }

        // ── Bob timer accumulation ──────────────────────────────────
        _bobTimer += dt;

        // ── GIF Frame Animation ─────────────────────────────────────
        if (_frames.Count > 1 && _sprite != null)
        {
            _frameTimer += dt;
            if (_frameTimer >= _frameDuration)
            {
                _frameTimer = 0f;
                _currentFrame = (_currentFrame + 1) % _frames.Count;
                _sprite.Texture = _frames[_currentFrame];
            }
        }

        // ── Movement and Physics ─────────────────────────────────────
        Vector3 newPos = Position;
        
        switch (MovementType)
        {
            case TargetMovementType.Float:
                // Gentle floating: slow horizontal drift + slow vertical bobbing
                float verticalSpeed = 0.6f;
                _velocity.Y = Mathf.Sin(_bobTimer * 1.5f) * verticalSpeed;
                newPos += _velocity * dt;
                break;

            case TargetMovementType.Hover:
                // Mid-air hover: standard strafe with gentle bob
                newPos += _velocity * dt;
                break;

            case TargetMovementType.Slide:
                // Ground slide: constrain to low Y, slide horizontally
                newPos.Y = 1.3f;
                _velocity.Y = 0;
                newPos += _velocity * dt;
                break;

            case TargetMovementType.Erratic:
                // UFO style: dash and pause
                _erraticTimer += dt;
                if (_isDashing)
                {
                    if (_erraticTimer >= 0.5f)
                    {
                        _isDashing = false;
                        _erraticTimer = 0f;
                        _velocity = Vector3.Zero;
                    }
                    else
                    {
                        _velocity = _dashDirection * (float)(Speed * 2.2);
                    }
                }
                else
                {
                    if (_erraticTimer >= 0.4f)
                    {
                        _isDashing = true;
                        _erraticTimer = 0f;
                        Random r = new Random();
                        float angle = (float)(r.NextDouble() * Math.PI * 2);
                        _dashDirection = new Vector3(Mathf.Cos(angle), (float)(r.NextDouble() * 0.4 - 0.2), Mathf.Sin(angle)).Normalized();
                    }
                    else
                    {
                        _velocity = Vector3.Zero;
                    }
                }
                newPos += _velocity * dt;
                break;
        }

        // Apply boundaries
        if (newPos.X < _minBoundary.X || newPos.X > _maxBoundary.X)
        {
            _velocity.X  = -_velocity.X;
            _dashDirection.X = -_dashDirection.X;
            Direction.X  = -Direction.X;
            newPos.X = Mathf.Clamp(newPos.X, _minBoundary.X, _maxBoundary.X);
        }
        if (newPos.Y < _minBoundary.Y || newPos.Y > _maxBoundary.Y)
        {
            if (MovementType != TargetMovementType.Slide)
            {
                _velocity.Y  = -_velocity.Y;
                _dashDirection.Y = -_dashDirection.Y;
                Direction.Y  = -Direction.Y;
                newPos.Y = Mathf.Clamp(newPos.Y, _minBoundary.Y, _maxBoundary.Y);
            }
        }
        if (newPos.Z < _minBoundary.Z || newPos.Z > _maxBoundary.Z)
        {
            _velocity.Z  = -_velocity.Z;
            _dashDirection.Z = -_dashDirection.Z;
            Direction.Z  = -Direction.Z;
            newPos.Z = Mathf.Clamp(newPos.Z, _minBoundary.Z, _maxBoundary.Z);
        }
        Position = newPos;

        // Face movement direction (except Balloon which is symmetrical)
        if (VisualType != TargetVisualType.Balloon && _velocity.LengthSquared() > 0.01f)
        {
            var lookDir = new Vector3(_velocity.X, 0, _velocity.Z).Normalized();
            if (lookDir != Vector3.Zero)
                LookAt(GlobalPosition + lookDir, Vector3.Up);
        }

        // ── Model Animations ─────────────────────────────────────────
        if (_visualRoot != null)
        {
            switch (VisualType)
            {
                case TargetVisualType.Drone:
                    // Rotors spin
                    foreach (var rotor in _droneRotors)
                        rotor.RotateY(dt * 30.0f);
                    
                    // Lean/tilt into velocity
                    float targetDroneTiltZ = -_velocity.X * 0.06f;
                    float targetDroneTiltX = _velocity.Z * 0.06f;
                    _visualRoot.Rotation = new Vector3(
                        Mathf.LerpAngle(_visualRoot.Rotation.X, targetDroneTiltX, dt * 10f),
                        _visualRoot.Rotation.Y,
                        Mathf.LerpAngle(_visualRoot.Rotation.Z, targetDroneTiltZ, dt * 10f)
                    );
                    break;

                case TargetVisualType.Balloon:
                    // Sway gently in wind
                    float swayX = Mathf.Sin(_bobTimer * 2f) * 0.12f;
                    float swayZ = Mathf.Cos(_bobTimer * 1.5f) * 0.12f;
                    _visualRoot.Rotation = new Vector3(swayX, _visualRoot.Rotation.Y, swayZ);
                    break;

                case TargetVisualType.Hoverboard:
                    // Tilt deck on lateral strafes
                    float targetBoardTiltZ = -_velocity.X * 0.09f;
                    float targetBoardTiltX = _velocity.Z * 0.05f;
                    _visualRoot.Rotation = new Vector3(
                        Mathf.LerpAngle(_visualRoot.Rotation.X, targetBoardTiltX, dt * 8f),
                        _visualRoot.Rotation.Y,
                        Mathf.LerpAngle(_visualRoot.Rotation.Z, targetBoardTiltZ, dt * 8f)
                    );
                    break;

                case TargetVisualType.UFO:
                    // Spin saucer ring
                    if (_ufoRing != null)
                        _ufoRing.RotateY(dt * 6.0f);
                    
                    // Hover wiggle
                    float wiggleX = Mathf.Sin(_bobTimer * 4.0f) * 0.03f;
                    float wiggleZ = Mathf.Cos(_bobTimer * 3.5f) * 0.03f;
                    _visualRoot.Rotation = new Vector3(wiggleX, _visualRoot.Rotation.Y, wiggleZ);
                    break;
            }
        }
    }

    public bool OnHit()
    {
        Random r = new Random();
        bool isHeadshot = r.Next(0, 100) < 25;

        // Spawn debris in the parent scene
        Node parent = GetParent();
        if (parent != null)
        {
            Color debrisColor = IsCivilian ? ColCivilian : ColEnemy;
            SpawnDebris(parent, debrisColor, r);
        }

        QueueFree();
        return isHeadshot;
    }

    // ─────────────────────────────────────────────────────────────────
    // MODEL BUILDERS
    // ─────────────────────────────────────────────────────────────────
    private void BuildDrone(Color themeColor)
    {
        // 1. Main body chassis
        var chassis = new MeshInstance3D();
        var chassisMesh = new CylinderMesh();
        chassisMesh.TopRadius = 0.2f;
        chassisMesh.BottomRadius = 0.2f;
        chassisMesh.Height = 0.08f;
        chassis.Mesh = chassisMesh;
        chassis.MaterialOverride = MakeMat(new Color(0.2f, 0.22f, 0.25f), 0.3f, 0.8f);
        _visualRoot.AddChild(chassis);

        // 2. Central dome
        var dome = new MeshInstance3D();
        var domeMesh = new SphereMesh();
        domeMesh.Radius = 0.12f;
        domeMesh.Height = 0.15f;
        dome.Mesh = domeMesh;
        dome.MaterialOverride = MakeMat(themeColor, 0.2f, 0.5f);
        dome.Position = new Vector3(0, 0.04f, 0);
        _visualRoot.AddChild(dome);

        // 3. Arms & Rotors
        float armLength = 0.35f;
        float[] angles = { 45f, 135f, 225f, 315f };
        foreach (float angle in angles)
        {
            float rad = Mathf.DegToRad(angle);
            Vector3 armPos = new Vector3(Mathf.Cos(rad) * armLength * 0.5f, 0, Mathf.Sin(rad) * armLength * 0.5f);

            var arm = new MeshInstance3D();
            var armMesh = new BoxMesh();
            armMesh.Size = new Vector3(armLength, 0.03f, 0.03f);
            arm.Mesh = armMesh;
            arm.MaterialOverride = chassis.MaterialOverride;
            arm.Position = armPos;
            arm.Rotation = new Vector3(0, -rad, 0);
            _visualRoot.AddChild(arm);

            Vector3 rotorPos = new Vector3(Mathf.Cos(rad) * armLength, 0.04f, Mathf.Sin(rad) * armLength);
            var rotor = new MeshInstance3D();
            var rotorMesh = new BoxMesh();
            rotorMesh.Size = new Vector3(0.18f, 0.005f, 0.02f);
            rotor.Mesh = rotorMesh;
            rotor.MaterialOverride = MakeMat(new Color(0.9f, 0.9f, 0.9f), 0.5f, 0.1f);
            rotor.Position = rotorPos;
            _visualRoot.AddChild(rotor);
            _droneRotors.Add(rotor);
        }
    }

    private void BuildBalloon(Color themeColor)
    {
        // 1. Balloon envelope
        var envelope = new MeshInstance3D();
        var sphere = new SphereMesh();
        sphere.Radius = 0.35f;
        sphere.Height = 0.8f;
        envelope.Mesh = sphere;
        envelope.MaterialOverride = MakeMat(themeColor, 0.1f, 0.1f);
        envelope.Position = new Vector3(0, 0.4f, 0);
        _visualRoot.AddChild(envelope);

        // 2. String
        var stringNode = new MeshInstance3D();
        var stringMesh = new CylinderMesh();
        stringMesh.TopRadius = 0.003f;
        stringMesh.BottomRadius = 0.003f;
        stringMesh.Height = 0.7f;
        stringNode.Mesh = stringMesh;
        stringNode.MaterialOverride = MakeMat(new Color(0.8f, 0.8f, 0.8f), 0.9f, 0f);
        stringNode.Position = new Vector3(0, -0.05f, 0);
        _visualRoot.AddChild(stringNode);
    }

    private void BuildHoverboard(Color themeColor)
    {
        // 1. Deck board
        var deck = new MeshInstance3D();
        var deckMesh = new BoxMesh();
        deckMesh.Size = new Vector3(0.45f, 0.04f, 1.0f);
        deck.Mesh = deckMesh;
        deck.MaterialOverride = MakeMat(new Color(0.15f, 0.15f, 0.17f), 0.4f, 0.7f);
        deck.Position = new Vector3(0, -0.15f, 0);
        _visualRoot.AddChild(deck);

        // 2. Neon trim
        var trim = new MeshInstance3D();
        var trimMesh = new BoxMesh();
        trimMesh.Size = new Vector3(0.47f, 0.02f, 0.98f);
        trim.Mesh = trimMesh;
        trim.MaterialOverride = MakeMat(themeColor, 0.2f, 0.9f);
        trim.Position = new Vector3(0, -0.15f, 0);
        _visualRoot.AddChild(trim);

        // 3. Thruster bells
        float[] zOffsets = { -0.3f, 0.3f };
        foreach (float zOffset in zOffsets)
        {
            var thruster = new MeshInstance3D();
            var tm = new CylinderMesh();
            tm.TopRadius = 0.05f;
            tm.BottomRadius = 0.03f;
            tm.Height = 0.08f;
            thruster.Mesh = tm;
            thruster.MaterialOverride = MakeMat(new Color(0.3f, 0.3f, 0.3f), 0.5f, 0.8f);
            thruster.Position = new Vector3(0, -0.21f, zOffset);
            thruster.Rotation = new Vector3(Mathf.DegToRad(90), 0, 0);
            _visualRoot.AddChild(thruster);

            var jet = new MeshInstance3D();
            var jm = new CylinderMesh();
            jm.TopRadius = 0.04f;
            jm.BottomRadius = 0.04f;
            jm.Height = 0.02f;
            jet.Mesh = jm;
            var jetMat = new StandardMaterial3D();
            jetMat.AlbedoColor = new Color(0f, 0.8f, 1.0f);
            jetMat.EmissionEnabled = true;
            jetMat.Emission = new Color(0f, 0.8f, 1.0f);
            jetMat.EmissionEnergyMultiplier = 2f;
            jet.MaterialOverride = jetMat;
            jet.Position = new Vector3(0, -0.05f, 0);
            thruster.AddChild(jet);
        }

        // 4. Exhaust particles
        var particles = new CpuParticles3D();
        particles.Name = "ThrusterParticles";
        particles.Amount = 15;
        particles.Lifetime = 0.4f;
        particles.Spread = 20f;
        particles.Gravity = new Vector3(0, -3.0f, 0);
        particles.InitialVelocityMin = 0.8f;
        particles.InitialVelocityMax = 1.8f;
        particles.Position = new Vector3(0, -0.25f, -0.4f);
        particles.Direction = new Vector3(0, -1, -1).Normalized();
        
        var pMesh = new BoxMesh();
        pMesh.Size = new Vector3(0.04f, 0.04f, 0.04f);
        particles.Mesh = pMesh;

        var pMat = new StandardMaterial3D();
        pMat.AlbedoColor = themeColor;
        pMat.EmissionEnabled = true;
        pMat.Emission = themeColor;
        pMat.EmissionEnergyMultiplier = 1.5f;
        particles.MaterialOverride = pMat;
        _visualRoot.AddChild(particles);
    }

    private void BuildUFO(Color themeColor)
    {
        // 1. Saucer outer ring
        _ufoRing = new MeshInstance3D();
        _ufoRing.Name = "UfoRing";
        var ringMesh = new CylinderMesh();
        ringMesh.TopRadius = 0.45f;
        ringMesh.BottomRadius = 0.45f;
        ringMesh.Height = 0.06f;
        _ufoRing.Mesh = ringMesh;
        _ufoRing.MaterialOverride = MakeMat(new Color(0.6f, 0.63f, 0.65f), 0.1f, 0.9f);
        _visualRoot.AddChild(_ufoRing);

        for (int i = 0; i < 6; i++)
        {
            float angle = Mathf.DegToRad(i * 60f);
            var pod = new MeshInstance3D();
            var pm = new SphereMesh();
            pm.Radius = 0.04f;
            pm.Height = 0.08f;
            pod.Mesh = pm;

            var podMat = new StandardMaterial3D();
            podMat.AlbedoColor = themeColor;
            podMat.EmissionEnabled = true;
            podMat.Emission = themeColor;
            podMat.EmissionEnergyMultiplier = 2.0f;
            pod.MaterialOverride = podMat;
            pod.Position = new Vector3(Mathf.Cos(angle) * 0.44f, 0, Mathf.Sin(angle) * 0.44f);
            _ufoRing.AddChild(pod);
        }

        // 2. Dome
        var dome = new MeshInstance3D();
        var dm = new SphereMesh();
        dm.Radius = 0.24f;
        dm.Height = 0.32f;
        dome.Mesh = dm;

        var domeMat = new StandardMaterial3D();
        domeMat.AlbedoColor = new Color(0.1f, 0.7f, 1.0f, 0.35f);
        domeMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        domeMat.Roughness = 0.1f;
        domeMat.Metallic = 0.1f;
        dome.MaterialOverride = domeMat;
        dome.Position = new Vector3(0, 0.05f, 0);
        _visualRoot.AddChild(dome);

        // 3. Tractor beam
        var beam = new MeshInstance3D();
        var bm = new CylinderMesh();
        bm.TopRadius = 0.15f;
        bm.BottomRadius = 0.25f;
        bm.Height = 0.3f;
        beam.Mesh = bm;

        var beamMat = new StandardMaterial3D();
        beamMat.AlbedoColor = new Color(themeColor.R, themeColor.G, themeColor.B, 0.2f);
        beamMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        beamMat.EmissionEnabled = true;
        beamMat.Emission = themeColor;
        beamMat.EmissionEnergyMultiplier = 1.0f;
        beam.MaterialOverride = beamMat;
        beam.Position = new Vector3(0, -0.18f, 0);
        _visualRoot.AddChild(beam);

        // 4. Spot glow
        var light = new OmniLight3D();
        light.LightColor = themeColor;
        light.LightEnergy = 1.5f;
        light.OmniRange = 3.0f;
        light.Position = new Vector3(0, -0.2f, 0);
        _visualRoot.AddChild(light);
    }

    private void BuildLabel()
    {
        var label = new Label3D();
        label.Name = "NameLabel";
        label.Text = (IsCivilian ? "✅" : "❌") + " " +
                     TargetName.Replace(".gif","").Replace("_"," ").ToUpper();
        label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        label.FontSize = 14;
        label.OutlineSize = 5;
        label.OutlineModulate = new Color(0,0,0,1);
        label.Modulate = IsCivilian ? new Color(0.3f,1f,0.3f) : new Color(1f,0.3f,0.3f);
        
        float labelHeight = 1.1f;
        switch (VisualType)
        {
            case TargetVisualType.Drone:
                labelHeight = 1.1f;
                break;
            case TargetVisualType.Balloon:
                labelHeight = 1.0f;
                break;
            case TargetVisualType.Hoverboard:
                labelHeight = 1.1f;
                break;
            case TargetVisualType.UFO:
                labelHeight = 0.9f;
                break;
        }
        label.Position = new Vector3(0, labelHeight, 0);
        AddChild(label);
    }

    private void BuildCollision()
    {
        var col = new CollisionShape3D();
        var box = new BoxShape3D();
        
        float width = 1.0f;
        float height = 1.6f;
        float depth = 0.8f;
        Vector3 center = new Vector3(0, 0, 0);

        switch (VisualType)
        {
            case TargetVisualType.Drone:
                width = 0.9f;
                height = 1.2f;
                depth = 0.9f;
                center = new Vector3(0, 0.2f, 0);
                break;
            case TargetVisualType.Balloon:
                width = 0.8f;
                height = 1.7f;
                depth = 0.8f;
                center = new Vector3(0, -0.1f, 0);
                break;
            case TargetVisualType.Hoverboard:
                width = 0.8f;
                height = 1.3f;
                depth = 1.1f;
                center = new Vector3(0, 0.15f, 0);
                break;
            case TargetVisualType.UFO:
                width = 1.1f;
                height = 1.0f;
                depth = 1.1f;
                center = new Vector3(0, 0.05f, 0);
                break;
        }

        box.Size = new Vector3(width, height, depth);
        col.Shape = box;
        col.Position = center;
        AddChild(col);
        CollisionLayer = 1;
        CollisionMask = 1;
    }

    // ─────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────
    private static StandardMaterial3D MakeMat(Color color, float rough, float metal)
    {
        var m = new StandardMaterial3D();
        m.AlbedoColor = color;
        m.Roughness   = rough;
        m.Metallic    = metal;
        return m;
    }

    private void SpawnDebris(Node parent, Color color, Random rand)
    {
        Color debrisColor;
        switch (VisualType)
        {
            case TargetVisualType.Drone:
                debrisColor = rand.Next(2) == 0 ? new Color(0.2f, 0.22f, 0.25f) : color;
                break;
            case TargetVisualType.Balloon:
                debrisColor = color;
                break;
            case TargetVisualType.Hoverboard:
                debrisColor = rand.Next(2) == 0 ? new Color(0.15f, 0.15f, 0.17f) : color;
                break;
            case TargetVisualType.UFO:
                debrisColor = rand.Next(2) == 0 ? new Color(0.6f, 0.63f, 0.65f) : new Color(0.1f, 0.7f, 1.0f);
                break;
            default:
                debrisColor = color;
                break;
        }

        for (int i = 0; i < 8; i++)
        {
            var cube = new MeshInstance3D();
            var cm   = new BoxMesh();
            float sz = (float)(rand.NextDouble() * 0.18 + 0.08);
            cm.Size  = new Vector3(sz, sz, sz);
            cube.Mesh = cm;
            cube.MaterialOverride = MakeMat(debrisColor, 0.9f, 0f);
            cube.Position = GlobalPosition + new Vector3(
                (float)(rand.NextDouble() * 0.6 - 0.3),
                (float)(rand.NextDouble() * 0.8),
                (float)(rand.NextDouble() * 0.6 - 0.3));
            parent.AddChild(cube);

            Vector3 velocity = new Vector3(
                (float)(rand.NextDouble() * 6 - 3),
                (float)(rand.NextDouble() * 5 + 2),
                (float)(rand.NextDouble() * 6 - 3));

            var tween = cube.CreateTween();
            tween.TweenProperty(cube, "position",
                cube.Position + velocity * 0.6f, 0.6)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);

            tween.TweenCallback(Callable.From(() =>
            {
                if (IsInstanceValid(cube)) cube.QueueFree();
            }));
        }
    }

    private void FadeChildren(float alpha)
    {
        SetNodeAlpha(this, alpha);
    }

    private void SetNodeAlpha(Node node, float alpha)
    {
        if (node is MeshInstance3D meshInstance)
        {
            if (meshInstance.MaterialOverride is StandardMaterial3D matOverride)
            {
                matOverride.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                Color c = matOverride.AlbedoColor;
                c.A = alpha;
                matOverride.AlbedoColor = c;
            }
            int count = meshInstance.GetSurfaceOverrideMaterialCount();
            for (int i = 0; i < count; i++)
            {
                if (meshInstance.GetSurfaceOverrideMaterial(i) is StandardMaterial3D matSurface)
                {
                    matSurface.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                    Color c = matSurface.AlbedoColor;
                    c.A = alpha;
                    matSurface.AlbedoColor = c;
                }
            }
        }
        else if (node is Label3D label)
        {
            label.Modulate = new Color(label.Modulate.R, label.Modulate.G, label.Modulate.B, alpha);
        }
        else if (node is Sprite3D sprite)
        {
            sprite.Modulate = new Color(1, 1, 1, alpha);
        }

        foreach (Node child in node.GetChildren())
        {
            SetNodeAlpha(child, alpha);
        }
    }
}
