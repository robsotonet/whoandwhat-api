-- Development-only database initialization
-- This script runs only in development environment

-- Enable query logging for development
ALTER SYSTEM SET log_statement = 'all';
ALTER SYSTEM SET log_min_duration_statement = 0;
SELECT pg_reload_conf();

-- Create development user (in addition to default postgres user)
DO $$ 
BEGIN
   IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'devuser') THEN
      CREATE ROLE devuser WITH LOGIN PASSWORD 'devpass';
      GRANT CONNECT ON DATABASE "WhoAndWhat" TO devuser;
   END IF;
END
$$;