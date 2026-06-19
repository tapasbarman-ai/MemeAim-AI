package main

import (
	"encoding/json"
	"log"
	"math/rand"
	"net/http"
	"strconv"
	"time"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
	"github.com/redis/go-redis/v9"
)

type TargetGIF struct {
	ID          string                 `json:"id"`
	Name        string                 `json:"name"`
	StoragePath string                 `json:"storage_path"`
	Tags        map[string]interface{} `json:"tags"`
}

type RoundConfig struct {
	RuleID          string      `json:"rule_id"`
	RuleType        string      `json:"rule_type"`
	RuleTitle       string      `json:"rule_title"`
	RuleDescription string      `json:"rule_description"`
	Targets         []TargetGIF `json:"targets"`
}

type MatchSubmitRequest struct {
	UserID         string  `json:"user_id"`
	Username       string  `json:"username"`
	Score          int     `json:"score"`
	Accuracy       float64 `json:"accuracy"`
	ReactionTimeMS int     `json:"reaction_time_ms"`
	HeadshotCount  int     `json:"headshot_count"`
	MissCount      int     `json:"miss_count"`
	CivilianHits   int     `json:"civilian_hits"`
	Mode           string  `json:"mode"`
}

type TelemetryEvent struct {
	TimestampMS      int64   `json:"timestamp_ms"`
	IsHit            bool    `json:"is_hit"`
	CoordinateX      float64 `json:"coordinate_x"`
	CoordinateY      float64 `json:"coordinate_y"`
	TargetGIFID      *string `json:"target_gif_id"`
	TargetSpeed      float64 `json:"target_speed"`
	TargetDirectionX float64 `json:"target_direction_x"`
	TargetDirectionY float64 `json:"target_direction_y"`
	ActiveRule       string  `json:"active_rule"`
	IsCivilian       bool    `json:"is_civilian"`
}

type TelemetrySubmitRequest struct {
	MatchID string           `json:"match_id"`
	Events  []TelemetryEvent `json:"events"`
}

type LeaderboardEntry struct {
	Rank     int    `json:"rank"`
	Username string `json:"username"`
	Score    int    `json:"score"`
}

// Built-in Seed GIF List for local development fallback
var seedGIFs = []TargetGIF{
	{ID: "11111111-1111-1111-1111-111111111111", Name: "dancing_shrek.gif", StoragePath: "gifs/giphy_target_1.gif", Tags: map[string]interface{}{"enemy": true, "dancing": true, "human": true}},
	{ID: "22222222-2222-2222-2222-222222222222", Name: "meme_dance.gif", StoragePath: "gifs/giphy_target_2.gif", Tags: map[string]interface{}{"enemy": true, "dancing": true, "human": true}},
	{ID: "33333333-3333-3333-3333-333333333333", Name: "cute_hamster.gif", StoragePath: "gifs/giphy_target_3.gif", Tags: map[string]interface{}{"friendly": true, "animal": true}},
	{ID: "44444444-4444-4444-4444-444444444444", Name: "angry_npc.gif", StoragePath: "gifs/giphy_target_4.gif", Tags: map[string]interface{}{"enemy": true, "angry": true, "human": true}},
	{ID: "55555555-5555-5555-5555-555555555555", Name: "crying_cat.gif", StoragePath: "gifs/giphy_target_5.gif", Tags: map[string]interface{}{"friendly": true, "animal": true}},
	{ID: "66666666-6666-6666-6666-666666666666", Name: "screaming_guy.gif", StoragePath: "gifs/giphy_target_6.gif", Tags: map[string]interface{}{"enemy": true, "angry": true, "human": true}},
	{ID: "77777777-7777-7777-7777-777777777777", Name: "dancing_dog.gif", StoragePath: "gifs/giphy_target_7.gif", Tags: map[string]interface{}{"friendly": true, "animal": true, "dancing": true}},
	{ID: "88888888-8888-8888-8888-888888888888", Name: "zombie_rage.gif", StoragePath: "gifs/giphy_target_8.gif", Tags: map[string]interface{}{"enemy": true, "zombie": true, "scary": true}},
	{ID: "99999999-9999-9999-9999-999999999999", Name: "old_grandma.gif", StoragePath: "gifs/giphy_target_9.gif", Tags: map[string]interface{}{"friendly": true, "grandma": true, "human": true}},
	{ID: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", Name: "pepe_punch.gif", StoragePath: "gifs/giphy_target_10.gif", Tags: map[string]interface{}{"enemy": true, "angry": true}},
	{ID: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", Name: "funny_duck.gif", StoragePath: "gifs/giphy_target_11.gif", Tags: map[string]interface{}{"friendly": true, "animal": true, "bird": true}},
	{ID: "cccccccc-cccc-cccc-cccc-cccccccccccc", Name: "scary_ghost.gif", StoragePath: "gifs/giphy_target_12.gif", Tags: map[string]interface{}{"enemy": true, "scary": true}},
	{ID: "dddddddd-dddd-dddd-dddd-dddddddddddd", Name: "friendly_clown.gif", StoragePath: "gifs/giphy_target_13.gif", Tags: map[string]interface{}{"friendly": true, "human": true, "hat": true}},
	{ID: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", Name: "spiderman_dance.gif", StoragePath: "gifs/giphy_target_14.gif", Tags: map[string]interface{}{"enemy": true, "dancing": true}},
	{ID: "ffffffff-ffff-ffff-ffff-ffffffffffff", Name: "sleeping_capybara.gif", StoragePath: "gifs/giphy_target_15.gif", Tags: map[string]interface{}{"friendly": true, "animal": true}},
	{ID: "00000000-0000-0000-0000-000000000001", Name: "wizard_guy.gif", StoragePath: "gifs/giphy_target_16.gif", Tags: map[string]interface{}{"enemy": true, "human": true, "hat": true}},
	{ID: "00000000-0000-0000-0000-000000000002", Name: "dancing_otter.gif", StoragePath: "gifs/giphy_target_17.gif", Tags: map[string]interface{}{"friendly": true, "animal": true, "dancing": true}},
}

// Built-in Rules configuration
type RuleTemplate struct {
	Type        string
	Title       string
	Description string
}

var ruleTemplates = []RuleTemplate{
	{Type: "shoot_enemy", Title: "Shoot Angry Faces!", Description: "Shoot enemies and angry faces. Avoid animals and grandmas!"},
	{Type: "avoid_animals", Title: "Don't Shoot Animals!", Description: "Shoot human-like targets only. Keep the animals safe!"},
	{Type: "shoot_dancing", Title: "Shoot Dancing Targets!", Description: "Target moving memes that are active dancers!"},
	{Type: "avoid_dancing", Title: "Don't Shoot Dancing Targets!", Description: "Only hit stationary or standard walking memes!"},
	{Type: "shoot_hats", Title: "Shoot Targets Wearing Hats!", Description: "Find characters wearing hats and take them down!"},
}

// GET /api/game/round
func (srv *Server) handleGetRound(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	rand.Seed(time.Now().UnixNano())

	// Select dynamic rule
	selectedRule := ruleTemplates[rand.Intn(len(ruleTemplates))]

	// Attempt to load GIFs from PostgreSQL
	var gifs []TargetGIF
	rows, err := srv.DB.Query(ctx, "SELECT id, name, storage_path, tags FROM public.gifs LIMIT 50")
	if err == nil {
		defer rows.Close()
		for rows.Next() {
			var gif TargetGIF
			var tagsBytes []byte
			if err := rows.Scan(&gif.ID, &gif.Name, &gif.StoragePath, &tagsBytes); err == nil {
				if err := json.Unmarshal(tagsBytes, &gif.Tags); err == nil {
					gifs = append(gifs, gif)
				}
			}
		}
	}

	// Fallback to seeds if database is empty or queries error out
	if len(gifs) == 0 {
		gifs = seedGIFs
	}

	config := RoundConfig{
		RuleID:          uuid.New().String(),
		RuleType:        selectedRule.Type,
		RuleTitle:       selectedRule.Title,
		RuleDescription: selectedRule.Description,
		Targets:         gifs,
	}

	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(http.StatusOK)
	json.NewEncoder(w).Encode(config)
}

// POST /api/match/submit
func (srv *Server) handlePostMatch(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	var req MatchSubmitRequest

	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		http.Error(w, "Invalid request body", http.StatusBadRequest)
		return
	}

	// Validate or mock UserID
	if req.UserID == "" {
		req.UserID = uuid.New().String()
	}
	if req.Username == "" {
		req.Username = "Guest"
	}
	if req.Mode == "" {
		req.Mode = "meme_hunter"
	}

	matchID := uuid.New().String()

	// 1. Insert into PostgreSQL (Supabase)
	query := `
		INSERT INTO public.match_history 
		(id, user_id, score, accuracy, reaction_time_ms, headshot_count, miss_count, civilian_hits, mode)
		VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)
	`
	_, err := srv.DB.Exec(ctx, query,
		matchID, req.UserID, req.Score, req.Accuracy, req.ReactionTimeMS,
		req.HeadshotCount, req.MissCount, req.CivilianHits, req.Mode,
	)
	if err != nil {
		log.Printf("Error inserting match: %v\n", err)
		http.Error(w, "Database error saving match history", http.StatusInternalServerError)
		return
	}

	// 2. Submit to Redis Leaderboard
	if srv.Redis != nil {
		leaderboardKey := "leaderboard:" + req.Mode
		err := srv.Redis.ZAdd(ctx, leaderboardKey, redis.Z{
			Score:  float64(req.Score),
			Member: req.Username,
		}).Err()
		if err != nil {
			log.Printf("Warning: Failed to update Redis leaderboard: %v\n", err)
		}
	}

	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(http.StatusOK)
	json.NewEncoder(w).Encode(map[string]string{
		"status":   "success",
		"match_id": matchID,
	})
}

// POST /api/telemetry/submit
func (srv *Server) handlePostTelemetry(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	var req TelemetrySubmitRequest

	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		http.Error(w, "Invalid request body", http.StatusBadRequest)
		return
	}

	if req.MatchID == "" {
		http.Error(w, "Missing match_id", http.StatusBadRequest)
		return
	}

	if len(req.Events) == 0 {
		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(http.StatusOK)
		json.NewEncoder(w).Encode(map[string]interface{}{"status": "success", "inserted": 0})
		return
	}

	// Batch insertion into PostgreSQL
	batch := &pgx.Batch{}
	for _, event := range req.Events {
		var targetID *uuid.UUID
		if event.TargetGIFID != nil && *event.TargetGIFID != "" {
			parsed, err := uuid.Parse(*event.TargetGIFID)
			if err == nil {
				targetID = &parsed
			}
		}

		query := `
			INSERT INTO public.telemetry_events 
			(match_id, timestamp_ms, is_hit, coordinate_x, coordinate_y, target_gif_id, target_speed, target_direction_x, target_direction_y, active_rule, is_civilian)
			VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)
		`
		batch.Queue(query,
			req.MatchID, event.TimestampMS, event.IsHit, event.CoordinateX, event.CoordinateY,
			targetID, event.TargetSpeed, event.TargetDirectionX, event.TargetDirectionY, event.ActiveRule, event.IsCivilian,
		)
	}

	br := srv.DB.SendBatch(ctx, batch)
	defer br.Close()

	for i := 0; i < len(req.Events); i++ {
		_, err := br.Exec()
		if err != nil {
			log.Printf("Batch insertion error at index %d: %v\n", i, err)
			http.Error(w, "Failed to batch save telemetry events", http.StatusInternalServerError)
			return
		}
	}

	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(http.StatusOK)
	json.NewEncoder(w).Encode(map[string]interface{}{
		"status":   "success",
		"inserted": len(req.Events),
	})
}

// GET /api/leaderboard
func (srv *Server) handleGetLeaderboard(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	mode := r.URL.Query().Get("mode")
	if mode == "" {
		mode = "meme_hunter"
	}

	limitStr := r.URL.Query().Get("limit")
	limit := 10
	if parsed, err := strconv.Atoi(limitStr); err == nil && parsed > 0 {
		limit = parsed
	}

	var leaderboard []LeaderboardEntry
	redisKey := "leaderboard:" + mode

	// Attempt Redis query
	if srv.Redis != nil {
		results, err := srv.Redis.ZRevRangeWithScores(ctx, redisKey, 0, int64(limit-1)).Result()
		if err == nil && len(results) > 0 {
			for idx, z := range results {
				leaderboard = append(leaderboard, LeaderboardEntry{
					Rank:     idx + 1,
					Username: z.Member.(string),
					Score:    int(z.Score),
				})
			}
		}
	}

	// Fallback to PostgreSQL if Redis fails or is empty
	if len(leaderboard) == 0 {
		log.Println("Redis leaderboard empty or failed, querying PostgreSQL instead")
		query := `
			SELECT mh.score, COALESCE(p.username, 'Guest')
			FROM public.match_history mh
			LEFT JOIN public.profiles p ON mh.user_id = p.id
			WHERE mh.mode = $1
			ORDER BY mh.score DESC
			LIMIT $2
		`
		rows, err := srv.DB.Query(ctx, query, mode, limit)
		if err == nil {
			defer rows.Close()
			rank := 1
			for rows.Next() {
				var entry LeaderboardEntry
				if err := rows.Scan(&entry.Score, &entry.Username); err == nil {
					entry.Rank = rank
					leaderboard = append(leaderboard, entry)
					rank++
				}
			}
		}
	}

	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(http.StatusOK)
	json.NewEncoder(w).Encode(leaderboard)
}
