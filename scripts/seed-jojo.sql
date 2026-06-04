INSERT INTO universes (id, slug, name, description, wiki_url, is_active, created_at)
VALUES (gen_random_uuid(), 'jojo', 'JoJo''s Bizarre Adventure',
        'Lore of JoJo''s Bizarre Adventure', 'https://jojo.fandom.com/wiki', TRUE, NOW())
ON CONFLICT (slug) DO NOTHING;
