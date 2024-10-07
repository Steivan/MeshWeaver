CREATE EXTENSION IF NOT EXISTS vector;
CREATE TABLE IF NOT EXISTS embeddings (
    id SERIAL PRIMARY KEY,
    embedding vector(1536) -- assuming the embedding size is 1536
);
CREATE OR REPLACE FUNCTION insert_embedding(text_input TEXT) RETURNS VOID AS $$
DECLARE
    embedding_vector vector(1536);
BEGIN
    -- Call your application code to get the embedding
    embedding_vector := get_embedding(text_input);
    -- Insert the embedding into the table
    INSERT INTO embeddings (embedding) VALUES (embedding_vector);
END;
$$ LANGUAGE plpgsql;
CREATE INDEX ON embeddings USING ivfflat (embedding);