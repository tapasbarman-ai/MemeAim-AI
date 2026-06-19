import os
import sys
import json
import psycopg2
from dotenv import load_dotenv

# Load environment variables
dotenv_path = os.path.join(os.path.dirname(__file__), "../backend-go/.env")
load_dotenv(dotenv_path=dotenv_path)

DB_URL = os.getenv("DATABASE_URL")

default_gifs = [
    {"id": "11111111-1111-1111-1111-111111111111", "name": "angry_businessman.gif", "storage_path": "gifs/angry_businessman.gif", "tags": {"enemy": True, "angry": True, "human": True}},
    {"id": "22222222-2222-2222-2222-222222222222", "name": "evil_clown.gif", "storage_path": "gifs/evil_clown.gif", "tags": {"enemy": True, "scary": True, "human": True, "hat": True}},
    {"id": "33333333-3333-3333-3333-333333333333", "name": "dancing_skeleton.gif", "storage_path": "gifs/dancing_skeleton.gif", "tags": {"enemy": True, "dancing": True, "skeleton": True}},
    {"id": "44444444-4444-4444-4444-444444444444", "name": "zombie.gif", "storage_path": "gifs/zombie.gif", "tags": {"enemy": True, "zombie": True}},
    {"id": "55555555-5555-5555-5555-555555555555", "name": "banana_gun.gif", "storage_path": "gifs/banana_gun.gif", "tags": {"enemy": True, "banana": True, "dancing": True, "yellow": True}},
    {"id": "66666666-6666-6666-6666-666666666666", "name": "evil_duck.gif", "storage_path": "gifs/evil_duck.gif", "tags": {"enemy": True, "animal": True, "bird": True}},
    {"id": "77777777-7777-7777-7777-777777777777", "name": "cat.gif", "storage_path": "gifs/cat.gif", "tags": {"friendly": True, "animal": True}},
    {"id": "88888888-8888-8888-8888-888888888888", "name": "dog.gif", "storage_path": "gifs/dog.gif", "tags": {"friendly": True, "animal": True}},
    {"id": "99999999-9999-9999-9999-999999999999", "name": "grandma.gif", "storage_path": "gifs/grandma.gif", "tags": {"friendly": True, "human": True, "grandma": True}},
    {"id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "name": "penguin.gif", "storage_path": "gifs/penguin.gif", "tags": {"friendly": True, "animal": True, "bird": True}},
    {"id": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "name": "sleeping_capybara.gif", "storage_path": "gifs/sleeping_capybara.gif", "tags": {"friendly": True, "animal": True}}
]

def main():
    if not DB_URL:
        print("Error: DATABASE_URL not found in .env configuration.")
        sys.exit(1)

    print("Connecting to Supabase Database to seed default target memes...")
    try:
        conn = psycopg2.connect(DB_URL)
        cur = conn.cursor()

        for gif in default_gifs:
            # Check if exists
            cur.execute("SELECT id FROM public.gifs WHERE id = %s", (gif["id"],))
            if cur.fetchone():
                print(f"GIF '{gif['name']}' already seeded. Skipping.")
                continue

            cur.execute(
                "INSERT INTO public.gifs (id, name, storage_path, tags) VALUES (%s, %s, %s, %s)",
                (gif["id"], gif["name"], gif["storage_path"], json.dumps(gif["tags"]))
            )
            print(f"Seeded '{gif['name']}' into 'public.gifs'.")

        conn.commit()
        print("Success! All default target memes have been seeded.")
        cur.close()
        conn.close()
    except Exception as e:
        print(f"Seeding failed: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()
