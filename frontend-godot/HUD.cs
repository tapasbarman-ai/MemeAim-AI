using Godot;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

public partial class HUD : CanvasLayer
{
    [Export] public string BackendUrl = "http://localhost:8080";
    [Export] public float RoundDurationSeconds = 90.0f;

    private Label _scoreLabel;
    private Label _ruleLabel;
    private Label _accuracyLabel;
    private Label _timerLabel;
    private Label _coachLabel;
    private Label _ruleChangeEffect;
    private PanelContainer _coachPanel;
    private ColorRect _crosshair;

    private PlayerController _player;
    private float _timeRemaining;
    private bool _roundOver = false;
    private float _ruleEffectTimer = 0.0f;

    private readonly System.Net.Http.HttpClient _httpClient = new System.Net.Http.HttpClient();

    public override void _Ready()
    {
        _scoreLabel       = GetNodeOrNull<Label>("ScoreLabel");
        _ruleLabel        = GetNodeOrNull<Label>("RuleLabel");
        _accuracyLabel    = GetNodeOrNull<Label>("AccuracyLabel");
        _timerLabel       = GetNodeOrNull<Label>("TimerLabel");
        _coachPanel       = GetNodeOrNull<PanelContainer>("CoachPanel");
        _coachLabel       = GetNodeOrNull<Label>("CoachPanel/CoachLabel");
        _ruleChangeEffect = GetNodeOrNull<Label>("RuleChangeEffect");

        try { _player = GetNode<PlayerController>("../PlayerCamera"); }
        catch { GD.PrintErr("HUD: Could not find PlayerCamera node"); }

        _timeRemaining = RoundDurationSeconds;

        if (_ruleLabel != null)
            _ruleLabel.Text = "🎯 Connecting to server...";

        // Add a clean crosshair in the center of the screen
        _crosshair = new ColorRect();
        _crosshair.Name = "Crosshair";
        _crosshair.Color = new Color(1, 1, 1, 0.8f);
        _crosshair.Size = new Vector2(6, 6);
        AddChild(_crosshair);
        
        CenterCrosshair();
        GetViewport().SizeChanged += CenterCrosshair;
    }

    private void CenterCrosshair()
    {
        if (_crosshair != null && IsInstanceValid(_crosshair))
        {
            _crosshair.Position = (GetViewport().GetVisibleRect().Size - _crosshair.Size) / 2f;
        }
    }

    public override void _Process(double delta)
    {
        if (_roundOver) return;

        // Countdown timer
        _timeRemaining -= (float)delta;
        if (_timeRemaining <= 0)
        {
            _timeRemaining = 0;
            EndRound();
        }

        // Update HUD labels
        if (_player != null)
        {
            _scoreLabel.Text    = $"Score: {_player._score}";
            _ruleLabel.Text     = $"🎯 {_player._currentRule}";
            int accPct          = _player._totalShots > 0 ? (int)((double)_player._hits / _player._totalShots * 100) : 0;
            _accuracyLabel.Text = $"Accuracy: {accPct}%  |  Misses: {_player._misses}  |  Civs: {_player._civilianHits}";
        }

        // Timer color turns red when under 15 seconds
        int secs = Mathf.CeilToInt(_timeRemaining);
        _timerLabel.Text = $"⏱ {secs}s";
        _timerLabel.Modulate = secs <= 15 ? new Color(1, 0.2f, 0.2f) : new Color(1, 1, 1);

        // Flash RULE CHANGED label
        if (_ruleChangeEffect.Visible)
        {
            _ruleEffectTimer -= (float)delta;
            float alpha = Mathf.Clamp(_ruleEffectTimer / 1.5f, 0, 1);
            _ruleChangeEffect.Modulate = new Color(1, 0.3f, 0.1f, alpha);
            if (_ruleEffectTimer <= 0) _ruleChangeEffect.Visible = false;
        }
    }

    // Called by TargetSpawner when a new rule is loaded
    public void OnRuleChanged(string newRule)
    {
        // Directly update rule label text immediately
        if (_ruleLabel != null)
            _ruleLabel.Text = $"🎯 {newRule}";

        if (_ruleChangeEffect != null)
        {
            _ruleChangeEffect.Visible = true;
            _ruleChangeEffect.Text = $"NEW RULE!\n{newRule}";
            _ruleEffectTimer = 1.5f;
        }
    }

    private async void EndRound()
    {
        _roundOver = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        GD.Print("Round over! Fetching AI coach analysis...");

        // Submit match data and fetch coach critique
        if (_player != null)
        {
            await _player.EndMatchAndSubmit();
            await FetchCoachCritique(_player.ActiveMatchID);
        }
    }

    private async Task FetchCoachCritique(string matchId)
    {
        if (string.IsNullOrEmpty(matchId))
        {
            ShowCoach("Hmm, I lost my notes. But I'm SURE your aim was garbage. Try again!");
            return;
        }

        try
        {
            string url = $"http://localhost:5000/coach/{matchId}";
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(body);
                string critique = doc.RootElement.GetProperty("critique").GetString() ?? "No critique available.";
                ShowCoach(critique);
            }
            else
            {
                ShowCoach(GetFallbackRoast());
            }
        }
        catch
        {
            ShowCoach(GetFallbackRoast());
        }
    }

    private void ShowCoach(string text)
    {
        _coachLabel.Text = $"🤖 AI Coach Says:\n\n\"{text}\"";
        _coachPanel.Visible = true;

        // Auto-hide after 8 seconds
        GetTree().CreateTimer(8.0).Timeout += () => _coachPanel.Visible = false;
    }

    private string GetFallbackRoast()
    {
        if (_player == null) return "Try harder next time.";
        int civs = _player._civilianHits;
        int acc  = _player._totalShots > 0 ? (int)((double)_player._hits / _player._totalShots * 100) : 0;

        if (civs > 3)
            return $"You shot {civs} innocent civilians. They had FAMILIES. The grandmas are pressing charges.";
        if (acc < 40)
            return $"Only {acc}% accuracy? A blindfolded capybara could do better. Stop panicking and AIM.";
        if (_player._score > 2000)
            return $"Okay fine, {_player._score} points is decent. Don't let it go to your head.";
        return "Mediocre performance. The memes are laughing at you. Try again.";
    }
}
