import os
import json
import requests
from dotenv import load_dotenv
from analysis import extract_features_from_telemetry, classify_aim_weakness

# Load environment variables
load_dotenv(dotenv_path=os.path.join(os.path.dirname(__file__), "../backend-go/.env"))

OLLAMA_URL = os.getenv("OLLAMA_HOST", "http://localhost:11434")
OLLAMA_MODEL = os.getenv("OLLAMA_MODEL", "qwen2.5:3b")

# Hardcoded funny roasts database for zero-dependency local fallback
FALLBACK_ROASTS = {
    "panic_left": [
        "Your tracking when moving left is tragic. Are you a NASCAR driver who only knows how to turn right? Seriously, targets slip left and you just freeze up like a deer in high-beams. Drag that mouse to the left, soldier!",
        "You have a literal allergy to the left side of your screen. Left-moving targets have a 100% survival rate against you. Did a left arrow hurt you in your childhood? Fix that track direction!",
        "Left-side panic detected! If this trainer was a steering wheel, we'd be driving into a ditch. Track those left-moving targets instead of staring at them with hope."
    ],
    "hesitant": [
        "Your reaction speed after a rule change drops to absolute snail levels. The game rules update and your brain takes a literal 5-second tea break. This is an aim trainer, not a chess tournament. Shoot faster!",
        "Hesitant much? You spent so much time contemplating the rule changes that the targets died of old age. When the banner says 'Shoot Animals', you don't need to consult a biologist. Just shoot them!",
        "Simon says 'WAKE UP'. Your reaction time post-rule change is measured in calendar days, not milliseconds. Switch targets instantly or go play turn-based RPGs."
    ],
    "trigger_happy": [
        "You shot a total of {civilian_hits} grandmas and friendly animals. They just wanted to exist, and you treated them like a threat! You click on anything that breathes. Chill out and actually read the rule!",
        "Trigger-happy maniac alert! You hit {civilian_hits} civilian targets. Your tracking is just 'click everything and pray'. Stop playing like a caffeinated squirrel and choose your targets with some sanity.",
        "You shot civilians. {civilian_hits} of them, to be exact. If this was a real mission, you'd be court-martialed on day one. Accuracy is 0% when you're shooting the friendly capybaras!"
    ],
    "steady": [
        "Wait, you actually did okay? Decent accuracy, fast reaction, zero grandmas killed. I'm almost disappointed, I had a great roast lined up. Increase the difficulty, show-off.",
        "A steady performance. You didn't embarrass yourself, which is a miracle. Overall accuracy was {overall_accuracy_pct}%. Let's see if you can keep that up when the rules speed up.",
        "Okay, you can aim. Fine. But can you do it under absolute chaos? Go trigger Infinite Chaos mode and let's see how long your sanity lasts."
    ]
}

def generate_coach_critique(match_id):
    """
    Extracts telemetry, runs PyTorch classification, and requests Ollama (or fallback)
    to write a funny sarcastic critique.
    """
    try:
        # Extract features and classify weakness
        features = extract_features_from_telemetry(match_id)
        report = classify_aim_weakness(features)
    except Exception as e:
        print(f"Error calculating telemetry features: {e}")
        return "Hey, my sensors are broken! I can't roast you right now, but I'm sure your aim was garbage anyway."

    weakness = report["weakness"]
    metrics = report["metrics"]
    overall_acc_pct = int(metrics["overall_accuracy"] * 100)
    
    # Structure the prompt for Ollama
    prompt = f"""
    You are the sarcastic, witty AI Coach of MemeAim AI, a funny aim-trainer game.
    Analyze the following player performance stats and write a brief, funny, sarcastic, and slightly mean roast/critique of their gameplay.
    Ensure you give them actual advice wrapped in humor. Keep it to 3 sentences maximum.

    Gameplay Performance:
    - Target Weakness Identified: {weakness.upper()} (Steady/Panic Left/Hesitant/Trigger Happy)
    - Overall Accuracy: {overall_acc_pct}%
    - Avg Reaction Time: {metrics['avg_reaction_time_seconds']:.3f} seconds
    - Rule Switch Lag: {metrics['rule_switch_penalty_seconds']:.3f} seconds
    - Civilian Hits (e.g. shot grandmas/friendly capybaras): {metrics['civilian_hits']}
    - Missed Shots: {metrics['total_misses']}

    Rule definitions:
    - Panic Left: Accuracy drops when targets move left.
    - Hesitant: Takes too long to react when a new rule is announced.
    - Trigger Happy: Shoots civilian/friendly targets frequently, misses a lot.
    - Steady: Decent stats all-around.

    Write the roast now:
    """

    try:
        response = requests.post(
            f"{OLLAMA_URL}/api/generate",
            json={
                "model": OLLAMA_MODEL,
                "prompt": prompt,
                "stream": False
            },
            timeout=10
        )
        if response.status_code == 200:
            ai_critique = response.json().get("response", "").strip()
            if ai_critique:
                return {
                    "weakness": weakness,
                    "metrics": metrics,
                    "critique": ai_critique,
                    "ai_generated": True
                }
    except Exception as e:
        print(f"Ollama coach call failed: {e}. Running fallback roast engine...")

    # Fallback roast selector
    import random
    roast_template = random.choice(FALLBACK_ROASTS[weakness])
    formatted_roast = roast_template.format(
        civilian_hits=metrics["civilian_hits"],
        overall_accuracy_pct=overall_acc_pct
    )

    return {
        "weakness": weakness,
        "metrics": metrics,
        "critique": formatted_roast,
        "ai_generated": False
    }

from http.server import HTTPServer, BaseHTTPRequestHandler

class CoachHTTPRequestHandler(BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path.startswith("/coach/"):
            match_id = self.path.split("/")[-1]
            if "?" in match_id:
                match_id = match_id.split("?")[0]
            try:
                print(f"Generating critique for Match ID: {match_id}")
                report = generate_coach_critique(match_id)
                self.send_response(200)
                self.send_header("Content-Type", "application/json")
                self.send_header("Access-Control-Allow-Origin", "*")
                self.end_headers()
                self.wfile.write(json.dumps(report).encode("utf-8"))
            except Exception as e:
                print(f"Error handling request: {e}")
                self.send_response(500)
                self.send_header("Content-Type", "application/json")
                self.send_header("Access-Control-Allow-Origin", "*")
                self.end_headers()
                self.wfile.write(json.dumps({"error": str(e)}).encode("utf-8"))
        else:
            self.send_response(404)
            self.end_headers()

def run_server():
    server = HTTPServer(("localhost", 5000), CoachHTTPRequestHandler)
    print("AI Coach HTTP Server running on http://localhost:5000...")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nStopping AI Coach Server...")

if __name__ == "__main__":
    run_server()
