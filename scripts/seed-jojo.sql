INSERT INTO universes ("Id", "Slug", "Name", "Description", "WikiUrl", "IsActive", "CreatedAt")
VALUES (gen_random_uuid(), 'jojo', 'JoJo''s Bizarre Adventure',
        'Lore of JoJo''s Bizarre Adventure', 'https://jojo.fandom.com/wiki', TRUE, NOW())
ON CONFLICT ("Slug") DO NOTHING;
