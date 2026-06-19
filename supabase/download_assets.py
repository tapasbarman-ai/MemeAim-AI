import os
import urllib.request

# Define targets and their custom robohash URLs
assets = {
    "angry_businessman.png": "https://robohash.org/angry_businessman.png?set=set1&size=256x256",
    "evil_clown.png": "https://robohash.org/evil_clown.png?set=set2&size=256x256",
    "dancing_skeleton.png": "https://robohash.org/dancing_skeleton.png?set=set2&bgset=bg2&size=256x256",
    "zombie.png": "https://robohash.org/zombie.png?set=set2&size=256x256",
    "banana_gun.png": "https://robohash.org/banana_gun.png?set=set1&size=256x256",
    "evil_duck.png": "https://robohash.org/evil_duck.png?set=set1&size=256x256",
    "cat.png": "https://robohash.org/cat.png?set=set4&size=256x256",
    "dog.png": "https://robohash.org/dog.png?set=set4&size=256x256",
    "grandma.png": "https://robohash.org/grandma.png?set=set3&size=256x256",
    "penguin.png": "https://robohash.org/penguin.png?set=set4&size=256x256",
    "sleeping_capybara.png": "https://robohash.org/sleeping_capybara.png?set=set4&size=256x256"
}

output_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "../frontend-godot/gifs"))

def download_assets():
    if not os.path.exists(output_dir):
        os.makedirs(output_dir)
        print(f"Created directory: {output_dir}")

    print("Downloading meme character sprites from Robohash API...")
    for name, url in assets.items():
        dest = os.path.join(output_dir, name)
        try:
            print(f"Downloading {name}...")
            # Set a user-agent to avoid HTTP 403 Forbidden issues
            req = urllib.request.Request(
                url, 
                headers={'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)'}
            )
            with urllib.request.urlopen(req) as response:
                with open(dest, 'wb') as f:
                    f.write(response.read())
            print(f"Saved to {dest}")
        except Exception as e:
            print(f"Failed to download {name}: {e}")

if __name__ == "__main__":
    download_assets()
