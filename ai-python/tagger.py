import os
import json
import glob
import requests
import psycopg2
from dotenv import load_dotenv

# Load environment variables
load_dotenv(dotenv_path=os.path.join(os.path.dirname(__file__), "../backend-go/.env"))

DB_URL = os.getenv("DATABASE_URL")
OLLAMA_URL = os.getenv("OLLAMA_HOST", "http://localhost:11434")
OLLAMA_MODEL = os.getenv("OLLAMA_MODEL", "qwen2.5:3b")

def get_db_connection():
    if not DB_URL:
        raise ValueError("DATABASE_URL environment variable is not set")
    return psycopg2.connect(DB_URL)

def tag_gif_with_ollama(filename):
    """
    Prompt Ollama to tag the GIF based on its filename and generate standard taxonomy.
    """
    prompt = f"""
    You are an automated tagger for MemeAim AI - an aim trainer game.
    Given the filename of a GIF: "{filename}", generate tag attributes for the gameplay target.
    
    You must return a JSON object ONLY, with the following boolean fields:
    - animal: true if it represents an animal, false otherwise.
    - human: true if it represents a human or human-like character, false otherwise.
    - yellow: true if yellow is a dominant color, false otherwise.
    - dancing: true if the character is likely performing a dance or active movement, false otherwise.
    - enemy: true if the character is an enemy/threat target (e.g. clown, gunman, zombie), false if friendly (e.g. dog, grandma, capybara).
    - scary: true if the character is scary or creepy, false otherwise.
    - hat: true if the character is wearing a hat or headwear, false otherwise.
    - skeleton: true if it is a skeleton, false otherwise.
    - zombie: true if it is a zombie, false otherwise.
    - bird: true if it is a bird, false otherwise.

    Output format MUST be valid JSON and nothing else:
    {{
      "animal": false,
      "human": true,
      "yellow": false,
      "dancing": true,
      "enemy": true,
      "scary": false,
      "hat": true,
      "skeleton": false,
      "zombie": false,
      "bird": false
    }}
    """
    
    try:
        response = requests.post(
            f"{OLLAMA_URL}/api/generate",
            json={
                "model": OLLAMA_MODEL,
                "prompt": prompt,
                "stream": False,
                "format": "json"
            },
            timeout=10
        )
        if response.status_code == 200:
            result = response.json()
            tags = json.loads(result.get("response", "{}"))
            return tags
        else:
            print(f"Ollama request failed with status code {response.status_code}")
    except Exception as e:
        print(f"Error calling Ollama for {filename}: {e}")
    
    # Fallback heuristics based on filename if Ollama fails
    print("Falling back to name heuristics for tagging...")
    filename_lower = filename.lower()
    return {
        "animal": any(x in filename_lower for x in ["cat", "dog", "duck", "penguin", "capybara"]),
        "human": any(x in filename_lower for x in ["businessman", "clown", "grandma", "zombie", "skeleton"]),
        "yellow": "banana" in filename_lower,
        "dancing": any(x in filename_lower for x in ["dancing", "skeleton", "banana"]),
        "enemy": any(x in filename_lower for x in ["clown", "zombie", "businessman", "gun", "duck", "skeleton"]),
        "scary": any(x in filename_lower for x in ["clown", "zombie", "skeleton"]),
        "hat": "clown" in filename_lower or "businessman" in filename_lower,
        "skeleton": "skeleton" in filename_lower,
        "zombie": "zombie" in filename_lower,
        "bird": any(x in filename_lower for x in ["duck", "penguin"])
    }

def process_gif_folder(folder_path):
    if not os.path.exists(folder_path):
        os.makedirs(folder_path)
        print(f"Created folder: {folder_path}. Drop your animated GIFs here!")
        return

    gif_files = glob.glob(os.path.join(folder_path, "*.gif"))
    if not gif_files:
        print(f"No GIFs found in {folder_path}. Please add some GIF files.")
        return

    conn = get_db_connection()
    cur = conn.cursor()

    for file_path in gif_files:
        filename = os.path.basename(file_path)
        print(f"Processing {filename}...")
        
        # Check if already tagged and indexed
        cur.execute("SELECT id FROM public.gifs WHERE name = %s", (filename,))
        if cur.fetchone():
            print(f"GIF '{filename}' is already indexed in the database. Skipping.")
            continue

        # Get tags from Ollama or heuristic fallback
        tags = tag_gif_with_ollama(filename)
        storage_path = f"gifs/{filename}" # Mock Supabase storage path

        # Insert into Database
        try:
            cur.execute(
                "INSERT INTO public.gifs (name, storage_path, tags) VALUES (%s, %s, %s)",
                (filename, storage_path, json.dumps(tags))
            )
            conn.commit()
            print(f"Successfully indexed '{filename}' with tags: {tags}")
        except Exception as e:
            conn.rollback()
            print(f"Failed to insert '{filename}' into database: {e}")

    cur.close()
    conn.close()

if __name__ == "__main__":
    gifs_dir = os.path.join(os.path.dirname(__file__), "../gifs_source")
    print(f"Scanning directory: {os.path.abspath(gifs_dir)}")
    process_gif_folder(gifs_dir)
