INSERT INTO universes ("Id", "Slug", "Name", "Description", "WikiUrl", "IsActive", "CreatedAt")
VALUES (gen_random_uuid(), 'persona', 'Persona 5',
        'Lore of Persona 5', 'https://megamitensei.fandom.com/wiki', TRUE, NOW())
ON CONFLICT ("Slug") DO NOTHING;
