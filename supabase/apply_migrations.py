import os
import sys
import psycopg2
from dotenv import load_dotenv

# Load environment variables
dotenv_path = os.path.join(os.path.dirname(__file__), "../backend-go/.env")
load_dotenv(dotenv_path=dotenv_path)

DB_URL = os.getenv("DATABASE_URL")

def main():
    if not DB_URL:
        print("Error: DATABASE_URL not found in .env configuration file.")
        sys.exit(1)

    print(f"Connecting to database: {DB_URL.split('@')[-1]}")
    try:
        conn = psycopg2.connect(DB_URL)
        cur = conn.cursor()
        
        # Read the migration schema
        schema_path = os.path.join(os.path.dirname(__file__), "migrations/20260619000000_schema.sql")
        print(f"Reading migrations from {schema_path}...")
        with open(schema_path, "r") as f:
            sql = f.read()

        print("Executing migrations...")
        cur.execute(sql)
        conn.commit()
        
        print("Success! Schema applied to Supabase database successfully.")
        
        cur.close()
        conn.close()
    except Exception as e:
        print(f"Failed to apply database migrations: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()
