INSERT INTO universes ("Id", "Slug", "Name", "Description", "WikiUrl", "IsActive", "CreatedAt")
VALUES (gen_random_uuid(), 'chainsaw_man', 'Chainsaw Man',
        'Lore of Chainsaw Man manga by Tatsuki Fujimoto', 'https://chainsaw-man.fandom.com/wiki', TRUE, NOW())
ON CONFLICT ("Slug") DO NOTHING;
