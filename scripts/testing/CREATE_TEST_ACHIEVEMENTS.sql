-- Скрипт для создания тестовых достижений в БД
-- Выполните через psql или через docker-compose exec

INSERT INTO "Achievements" ("Id", "Code", "Title", "Description", "IconUrl", "Category", "Points", "IsActive")
VALUES
    (gen_random_uuid(), 'first_project', 'Первый проект', 'Создал свой первый проект', NULL, 'projects', 10, true),
    (gen_random_uuid(), 'team_player', 'Командный игрок', 'Присоединился к проекту в качестве участника', NULL, 'teamwork', 15, true),
    (gen_random_uuid(), 'showcase_star', 'Звезда Showcase', 'Опубликовал проект в Showcase', NULL, 'projects', 25, true),
    (gen_random_uuid(), 'reviewer', 'Рецензент', 'Оставил свой первый отзыв', NULL, 'community', 10, true),
    (gen_random_uuid(), 'skill_master', 'Мастер навыков', 'Добавил 5 навыков в профиль', NULL, 'profile', 20, true),
    (gen_random_uuid(), 'mentor', 'Наставник', 'Помог новичку с первым проектом', NULL, 'community', 30, true),
    (gen_random_uuid(), 'active_member', 'Активный участник', 'Участвовал в 3+ проектах', NULL, 'activity', 15, true),
    (gen_random_uuid(), 'completer', 'Завершитель', 'Завершил проект', NULL, 'projects', 20, true)
ON CONFLICT ("Code") DO NOTHING;

-- Проверка
SELECT "Code", "Title", "Points" FROM "Achievements" WHERE "IsActive" = true;

