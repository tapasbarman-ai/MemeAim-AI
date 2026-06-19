import os
import sys
import json
import urllib.request
import psycopg2
from PIL import Image
from dotenv import load_dotenv

# Load database config
dotenv_path = os.path.abspath(os.path.join(os.path.dirname(__file__), "../backend-go/.env"))
load_dotenv(dotenv_path=dotenv_path)
DB_URL = os.getenv("DATABASE_URL")

# List of 17 Giphy URLs provided by the user
giphy_urls = [
    "https://media.giphy.com/media/xoZWpxtSLecftH30K1/giphy.gif",
    "https://media.giphy.com/media/qDOI1FqYEyTxkW0MEI/giphy.gif",
    "https://media.giphy.com/media/2ViZJi3RLXAZ22PG08/giphy.gif",
    "https://media.giphy.com/media/hzzVv0SMDjyyVVtlnb/giphy.gif",
    "https://media.giphy.com/media/NAivbJDbjN9sgkLzzE/giphy.gif",
    "https://media.giphy.com/media/ACeIDlMpgc4yOf1Lyt/giphy.gif",
    "https://media.giphy.com/media/c4mMnJ7K2Q4pUhSQ2N/giphy.gif",
    "https://media.giphy.com/media/8l45ZgfyKM6YK64tmV/giphy.gif",
    "https://media.giphy.com/media/qwr614iHK9bqdXfeF8/giphy.gif",
    "https://media.giphy.com/media/18gEqArCQNlo5zowZn/giphy.gif",
    "https://media.giphy.com/media/BfpDxZIJrzmoobf2Zm/giphy.gif",
    "https://media.giphy.com/media/PfPgPP9VX3m5O05sba/giphy.gif",
    "https://media.giphy.com/media/yxy69FCE06Ql0Fjk4Z/giphy.gif",
    "https://media.giphy.com/media/98MaHVwJOmWMz4cz1K/giphy.gif",
    "https://media.giphy.com/media/mOuVjiA6qvLGB3NusU/giphy.gif",
    "https://media.giphy.com/media/rieB6hYg1l15KrJJnk/giphy.gif",
    "https://media.giphy.com/media/ZRzVLn5bAlM7XqcEcH/giphy.gif"
]

# Map each target to descriptive names and tags
# We alternate between enemies and civilians/animals
target_configs = [
    {"name": "dancing_shrek.gif", "tags": {"enemy": True, "dancing": True, "human": True}},
    {"name": "meme_dance.gif", "tags": {"enemy": True, "dancing": True, "human": True}},
    {"name": "cute_hamster.gif", "tags": {"friendly": True, "animal": True}},
    {"name": "angry_npc.gif", "tags": {"enemy": True, "angry": True, "human": True}},
    {"name": "crying_cat.gif", "tags": {"friendly": True, "animal": True}},
    {"name": "screaming_guy.gif", "tags": {"enemy": True, "angry": True, "human": True}},
    {"name": "dancing_dog.gif", "tags": {"friendly": True, "animal": True, "dancing": True}},
    {"name": "zombie_rage.gif", "tags": {"enemy": True, "zombie": True, "scary": True}},
    {"name": "old_grandma.gif", "tags": {"friendly": True, "grandma": True, "human": True}},
    {"name": "pepe_punch.gif", "tags": {"enemy": True, "angry": True}},
    {"name": "funny_duck.gif", "tags": {"friendly": True, "animal": True, "bird": True}},
    {"name": "scary_ghost.gif", "tags": {"enemy": True, "scary": True}},
    {"name": "friendly_clown.gif", "tags": {"friendly": True, "human": True, "hat": True}},
    {"name": "spiderman_dance.gif", "tags": {"enemy": True, "dancing": True}},
    {"name": "sleeping_capybara.gif", "tags": {"friendly": True, "animal": True}},
    {"name": "wizard_guy.gif", "tags": {"enemy": True, "human": True, "hat": True}},
    {"name": "dancing_otter.gif", "tags": {"friendly": True, "animal": True, "dancing": True}}
]

output_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "../frontend-godot/gifs"))

def import_gifs():
    if not os.path.exists(output_dir):
        os.makedirs(output_dir)

    print(f"Target directory for client assets: {output_dir}")
    
    # 1. Download and convert GIFs to PNGs
    imported_targets = []
    
    for idx, url in enumerate(giphy_urls):
        config = target_configs[idx]
        gif_filename = f"giphy_target_{idx+1}.gif"
        png_filename = f"giphy_target_{idx+1}.png"
        
        gif_path = os.path.join(output_dir, gif_filename)
        png_path = os.path.join(output_dir, png_filename)
        
        try:
            print(f"\n[{idx+1}/17] Downloading target: {config['name']} ({gif_filename})...")
            
            # Download actual GIF
            req = urllib.request.Request(
                url, 
                headers={'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)'}
            )
            with urllib.request.urlopen(req) as response:
                with open(gif_path, 'wb') as f:
                    f.write(response.read())
            print(f"Saved GIF: {gif_path}")
            
            # Extract first frame as transparent PNG (cover fallback)
            with Image.open(gif_path) as im:
                im.seek(0)
                frame = im.convert("RGBA")
                frame.save(png_path, "PNG")
                
                # Extract all frames as individual PNGs
                frame_idx = 0
                while True:
                    try:
                        im.seek(frame_idx)
                        frame_rgba = im.convert("RGBA")
                        frame_path_idx = os.path.join(output_dir, f"giphy_target_{idx+1}_{frame_idx}.png")
                        frame_rgba.save(frame_path_idx, "PNG")
                        frame_idx += 1
                    except EOFError:
                        break
            print(f"Extracted static frame and {frame_idx} animation frames for target {idx+1}")
            
            # Save configuration for SQL injection
            imported_targets.append({
                "name": config["name"],
                "storage_path": f"gifs/{gif_filename}",
                "tags": config["tags"]
            })
            
        except Exception as e:
            print(f"Error importing target {idx+1}: {e}")

    # 2. Update Supabase PostgreSQL DB
    if not DB_URL:
        print("Warning: DATABASE_URL not set in .env. Skipping database update. (The backend will run on seed fallback).")
        return

    print("\nConnecting to Supabase PostgreSQL database to upload new targets...")
    try:
        conn = psycopg2.connect(DB_URL)
        cur = conn.cursor()
        
        # Clear old targets
        print("Clearing old targets from public.gifs...")
        cur.execute("DELETE FROM public.gifs")
        
        # Insert 17 new targets
        for target in imported_targets:
            cur.execute(
                "INSERT INTO public.gifs (name, storage_path, tags) VALUES (%s, %s, %s)",
                (target["name"], target["storage_path"], json.dumps(target["tags"]))
            )
            print(f"Inserted into database: {target['name']}")
            
        conn.commit()
        cur.close()
        conn.close()
        print("Success! Supabase PostgreSQL updated with the 17 new target memes.")
    except Exception as e:
        print(f"Error seeding database: {e}")

if __name__ == "__main__":
    import_gifs()
