# 🎯 MemeAim AI

[![Godot 4.7](https://img.shields.io/badge/Godot-4.7_C%23-blue?logo=godot-engine&logoColor=white)](https://godotengine.org/)
[![Go](https://img.shields.io/badge/Go-1.21+-00ADD8?logo=go&logoColor=white)](https://go.dev/)
[![Python](https://img.shields.io/badge/Python-3.10+-3776AB?logo=python&logoColor=white)](https://www.python.org/)
[![Supabase](https://img.shields.io/badge/Supabase-PostgreSQL-3ECF8E?logo=supabase&logoColor=white)](https://supabase.com/)
[![Redis](https://img.shields.io/badge/Redis-Cache-DC382D?logo=redis&logoColor=white)](https://redis.io/)
[![PyTorch](https://img.shields.io/badge/PyTorch-AI-EE4C2C?logo=pytorch&logoColor=white)](https://pytorch.org/)
[![Ollama](https://img.shields.io/badge/Ollama-LLM-black)](https://ollama.com/)

> **"Aim Lab meets WarioWare meets Internet Memes"** — A dynamic, data-driven 3D FPS aim trainer where you shoot animated Giphy memes riding Drones, Balloons, Hoverboards, and UFOs. The game dynamically changes rules mid-match (WarioWare style), logs every mouse click, analyzes your tracking errors using a PyTorch neural network, and roasts your gameplay via a local Ollama AI model.

---

## 📸 Overview & Mechanics

MemeAim AI isn't your average static clicker. It features:
- **Dynamic Rule Engine:** Every 15 seconds, the backend switches active gameplay rules (e.g., "Shoot Angry Faces", "Don't Shoot Animals", "Shoot Dancing Targets"). 
- **Moddable Lua Rule Engine:** Client-side hit evaluation is powered by an embedded Lua script (`RuleEngine.lua`), enabling hot-swappable client rules.
- **Data-Driven Targets:** Targets are built on a single reusable Godot Prefab scene configured dynamically via JSON payloads. Memes are rendered as frame-by-frame animated sprite sheet animations extracted directly from Giphy.
- **Granular Telemetry Pipeline:** Every shot (hit, miss, speed, target coordinate, direction, active rule, and civilian status) is streamed to a Go service and buffered into PostgreSQL.
- **AI Coach (PyTorch + Ollama):** An offline AI coach analyzes your coordinate telemetry using a Feedforward Neural Network to diagnose movement flaws (e.g., `panic_left`, `hesitant`, `trigger_happy`, or `steady`) and generates custom sarcastic critiques.

---

## 🏗️ Architecture Design

```mermaid
graph TD
    Client[Godot 4 Client C#] -->|1. Fetch Round Info| GoBackend[Go Chi Backend]
    GoBackend -->|Read Meme Data| Postgres[(Supabase PostgreSQL)]
    
    Client -->|2. Pull Giphy Frames| FileSystem[Local Assets cache]
    
    Client -->|3. Evaluate Hits| LuaEngine[Lua Rules Engine]
    
    Client -->|4. Push Match & Telemetry| GoBackend
    GoBackend -->|Save Match / Telemetry| Postgres
    GoBackend -->|Cache Leaderboard| Redis[(Redis Cash)]
    
    Client -->|5. Request Critique| AICoach[Python Coach Server]
    AICoach -->|Analyze Telemetry| PyTorch[PyTorch Classifier]
    AICoach -->|Query Match telemetry| Postgres
    AICoach -->|Send Stats + Prompt| Ollama[Ollama LLM]
    Ollama -->|Sarcastic Roast| AICoach
    AICoach -->|Feedback Json| Client
```

---

## 📂 Project Structure

```
Aim Labs/
├── frontend-godot/          # Godot 4.7 Game client (C# Engine Integration)
│   ├── Arena.tscn           # Main game environment
│   ├── ArenaBuilder.cs      # Spawning zones, boundaries, and round sequencer
│   ├── PlayerController.cs  # FPS movement, mouse-look, raycast click logic, telemetry buffer
│   ├── TargetSpawner.cs     # Fetches active rounds from backend, instantiates data-driven targets
│   ├── MemeTarget.cs        # Reusable target entity with 4 vehicle structures & frame player
│   ├── RuleEngine.lua       # Custom Lua rules validator run on hit events
│   └── gifs/                # Extracted frame sequences from 17 local meme targets
│
├── backend-go/              # Go REST Service
│   ├── main.go              # Database connections, chi router configuration, middleware
│   ├── handlers.go          # Core APIs (Round generation, match & telemetry submissions, leaderboards)
│   └── .env.example         # Environment template
│
├── ai-python/               # PyTorch telemetry analyzer & Sarcastic AI Coach HTTP server
│   ├── analysis.py          # Feature extractor & PyTorch AimWeaknessClassifier Neural Network
│   ├── coach.py             # Sarcastic Critique generator (HTTP Port 5000) using local Ollama model
│   ├── requirements.txt     # Python system packages
│   └── tagger.py            # Automated tagging script for Giphy targets using local VLMs
│
└── supabase/                # Database Migrations & Scripts
    ├── migrations/          # DDL files (Profiles, Gifs, Match History, Telemetry schemas)
    ├── apply_migrations.py  # Automation tool to apply SQL files to Supabase
    ├── seed_db.py           # Pre-population scripts for Giphy database rows
    ├── download_assets.py   # Bulk asset helper
    └── import_giphy_gifs.py # CLI to pull directly from Giphy API & convert to C# PNG sheets
```

---

## 🛠️ Installation & Setup

### Prerequisites
- **Godot Engine 4.7+ (Mono/C# version)** installed.
- **Go 1.21+** runtime.
- **Python 3.10+** (with `pip` package manager).
- **PostgreSQL / Supabase account** (free tier is fine).
- **Redis Server** (optional, fallback to Postgres for leaderboard is built-in).
- **Ollama CLI** (optional, fallback static sarcastic engine is built-in).

---

### Step 1: Database Setup & Seed
1. Go to your [Supabase Dashboard](https://supabase.com) and create a new project.
2. Retrieve your connection string from **Project Settings > Database > Connection string (URI mode)**.
3. Configure your Environment file. Create `backend-go/.env` based on `backend-go/.env.example`:
   ```ini
   PORT=8080
   DATABASE_URL=postgresql://postgres.yourprojectid:yourpassword@aws-0-us-east-1.pooler.supabase.com:6543/postgres
   REDIS_URL=localhost:6379
   REDIS_PASSWORD=
   ```
4. Navigate to the `supabase` folder, install requirements, run migrations, and seed the default memes:
   ```bash
   cd supabase
   pip install -r ../ai-python/requirements.txt
   python apply_migrations.py
   python seed_db.py
   ```

---

### Step 2: Running the Go Backend
The Go backend handles match scoring, leaderboard management via Redis/PostgreSQL, telemetry batching, and dynamic rule switches.

```bash
cd backend-go
go mod tidy
go run .
```
The server will boot on `http://localhost:8080`.

---

### Step 3: Setting Up the AI Coach (Python + PyTorch)
The Python service analyzes telemetry post-match, runs coordinate tracking evaluations, and roasts you via Ollama.

1. Install Ollama from [Ollama's website](https://ollama.com).
2. Download the default model (`qwen2.5:3b` is recommended for latency/speed balance):
   ```bash
   ollama run qwen2.5:3b
   ```
3. Boot the coach server:
   ```bash
   cd ai-python
   pip install -r requirements.txt
   python coach.py
   ```
The Python AI Coach HTTP server will run on `http://localhost:5000`.

---

### Step 4: Launching the Godot Game
1. Open the **Godot Engine** launcher.
2. Click **Import**, navigate to `frontend-godot/project.godot`, and open it.
3. Ensure you have the .NET SDK installed so Godot can compile the C# solution.
4. Press **F5** (or click the Play icon in the top-right corner) to run the game!

---

## 🎮 How to Play

1. **Aim & Shoot:** Click on targets. Keep track of the active rule shown on the top HUD banner!
2. **Observe Rules:**
   - **Shoot Angry Faces:** Only target angry/enemy targets. Shooting friendly creatures (like the Capybara or Grandma) results in a severe score penalty.
   - **Don't Shoot Animals:** Avoid anything animal-related, target human enemies.
   - **Shoot Dancing Targets:** Target moving, dancing memes.
3. **Target Lifespans:** Enemies will float/dash around indefinitely. Friendly/civilian targets automatically disappear after `3.5` seconds, so let them fade out naturally without shooting them.
4. **Roast Generation:** When the round timer ends (or you submit), the game will contact the local PyTorch pipeline to classify your aim errors, fetch a sarcastic review based on your mistakes, and present it on the screen!

---

## 🧠 AI Coach Classification Schema

The PyTorch Neural Network maps player performance into one of **4 distinct classes**:

| Weakness Class | Heuristic Trigger Conditions | Sample Sarcastic AI Coach Critique |
| :--- | :--- | :--- |
| **`steady`** | High overall accuracy, low civilian hits, low misses. | *"Wait, you actually did okay? Decent accuracy, fast reaction, zero grandmas killed. I'm almost disappointed, I had a great roast lined up. Increase the difficulty, show-off."* |
| **`panic_left`** | Accuracy on left-moving targets is significantly lower than on right-moving ones. | *"Your tracking when moving left is tragic. Are you a NASCAR driver who only knows how to turn right? Drag that mouse to the left, soldier!"* |
| **`hesitant`** | High delay / penalty spike immediately after active gameplay rule switches. | *"Your reaction speed after a rule change drops to absolute snail levels. The game rules update and your brain takes a literal 5-second tea break."* |
| **`trigger_happy`** | Over 2 civilian/friendly hits OR low accuracy coupled with a high miss count. | *"Trigger-happy maniac alert! You hit friendly targets. Stop playing like a caffeinated squirrel and choose your targets with some sanity."* |

---

## 🛠️ API Specifications

### `GET /api/game/round`
Returns the active round setup, including the current rule constraint and the pool of valid Giphy memes.
* **Response:**
  ```json
  {
    "rule_id": "31b15809-5431-48cb-a89e-2fe654e588db",
    "rule_type": "shoot_enemy",
    "rule_title": "Shoot Angry Faces!",
    "rule_description": "Shoot enemies and angry faces. Avoid animals and grandmas!",
    "targets": [
      {
        "id": "11111111-1111-1111-1111-111111111111",
        "name": "angry_businessman.gif",
        "storage_path": "gifs/angry_businessman.gif",
        "tags": { "enemy": true, "angry": true, "human": true }
      }
    ]
  }
  ```

### `POST /api/match/submit`
Saves the overall match metrics to Supabase PostgreSQL and updates the Redis Leaderboard.
* **Request:**
  ```json
  {
    "user_id": "uuid-here",
    "username": "MemeSlayer99",
    "score": 1250,
    "accuracy": 0.76,
    "reaction_time_ms": 380,
    "headshot_count": 12,
    "miss_count": 4,
    "civilian_hits": 1,
    "mode": "meme_hunter"
  }
  ```

### `POST /api/telemetry/submit`
Performs batch ingestion of click-level telemetry events.
* **Request:**
  ```json
  {
    "match_id": "match-uuid-here",
    "events": [
      {
        "timestamp_ms": 1781816000,
        "is_hit": true,
        "coordinate_x": 0.35,
        "coordinate_y": -0.12,
        "target_gif_id": "11111111-1111-1111-1111-111111111111",
        "target_speed": 4.5,
        "target_direction_x": -1.0,
        "target_direction_y": 0.0,
        "active_rule": "shoot_enemy",
        "is_civilian": false
      }
    ]
  }
  ```

### `GET /coach/<match_id>` (AI Coach Service)
Queries Postgres telemetry data for a match, evaluates it using PyTorch, and outputs the AI roast.
* **Response:**
  ```json
  {
    "weakness": "panic_left",
    "metrics": {
      "overall_accuracy": 0.65,
      "left_accuracy": 0.32,
      "right_accuracy": 0.88,
      "avg_reaction_time_seconds": 0.42,
      "rule_switch_penalty_seconds": 0.58,
      "civilian_hits": 0,
      "total_misses": 8
    },
    "critique": "Your tracking when moving left is tragic. Are you a NASCAR driver who only knows how to turn right? Seriously, targets slip left and you just freeze up like a deer in high-beams. Drag that mouse to the left, soldier!",
    "ai_generated": false
  }
  ```

---

## 📜 License
This project is licensed under the [MIT License](LICENSE).
