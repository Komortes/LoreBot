INSERT INTO universes (id, slug, name, description, wiki_url, is_active, created_at)
VALUES (gen_random_uuid(), 'persona', 'Persona 5',
        'Lore of Persona 5', 'https://megamitensei.fandom.com/wiki', TRUE, NOW())
ON CONFLICT (slug) DO NOTHING;
