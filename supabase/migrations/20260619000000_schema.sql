-- Schema Setup for MemeAim AI

-- Create profiles table (linked to auth.users if Supabase Auth is used)
CREATE TABLE IF NOT EXISTS public.profiles (
    id UUID PRIMARY KEY,
    username VARCHAR(255) UNIQUE NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Enable Row Level Security (RLS) on profiles
ALTER TABLE public.profiles ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "Allow public read access to profiles" ON public.profiles;
CREATE POLICY "Allow public read access to profiles" ON public.profiles
    FOR SELECT USING (true);

DROP POLICY IF EXISTS "Allow users to update their own profile" ON public.profiles;
CREATE POLICY "Allow users to update their own profile" ON public.profiles
    FOR UPDATE USING (auth.uid() = id);

-- Create GIFs table
CREATE TABLE IF NOT EXISTS public.gifs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    storage_path VARCHAR(512) NOT NULL, -- Path or URL to file in Supabase Storage
    tags JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Index tags for fast query execution
CREATE INDEX IF NOT EXISTS idx_gifs_tags ON public.gifs USING gin (tags);

-- Create match history table
CREATE TABLE IF NOT EXISTS public.match_history (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL, -- References profiles(id), can be regular UUID for standalone tests
    score INTEGER NOT NULL,
    accuracy DOUBLE PRECISION NOT NULL,
    reaction_time_ms INTEGER NOT NULL,
    headshot_count INTEGER NOT NULL,
    miss_count INTEGER NOT NULL,
    civilian_hits INTEGER NOT NULL,
    mode VARCHAR(100) NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_match_history_user ON public.match_history(user_id);

-- Create telemetry events table (granular tracking)
CREATE TABLE IF NOT EXISTS public.telemetry_events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    match_id UUID NOT NULL REFERENCES public.match_history(id) ON DELETE CASCADE,
    timestamp_ms BIGINT NOT NULL,
    is_hit BOOLEAN NOT NULL,
    coordinate_x DOUBLE PRECISION NOT NULL,
    coordinate_y DOUBLE PRECISION NOT NULL,
    target_gif_id UUID REFERENCES public.gifs(id) ON DELETE SET NULL,
    target_speed DOUBLE PRECISION NOT NULL,
    target_direction_x DOUBLE PRECISION NOT NULL,
    target_direction_y DOUBLE PRECISION NOT NULL,
    active_rule VARCHAR(255) NOT NULL,
    is_civilian BOOLEAN NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_telemetry_match ON public.telemetry_events(match_id);
