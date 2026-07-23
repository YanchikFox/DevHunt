// Bulk replace Russian text with Polish/English across all remaining files
import { readFileSync, writeFileSync, existsSync } from 'fs';
import { fileURLToPath } from 'url';
import { dirname, resolve } from 'path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(__dirname, '..');

const replacements = [
  // AdminSupportController.cs
  { file: 'DevHunt.CoreApi/Controllers/AdminSupportController.cs', pairs: [
    [`Тикет '{ticket.Subject}' назначен на админа`, `Zgłoszenie '{ticket.Subject}' przypisane do administratora`],
    [`Ваш тикет '{ticket.Subject}' назначен на администратора. Ожидайте ответа.`, `Twoje zgłoszenie '{ticket.Subject}' zostało przypisane do administratora. Oczekuj odpowiedzi.`],
    [`Тикет '{ticket.Subject}' назначен на вас`, `Zgłoszenie '{ticket.Subject}' przypisane do Ciebie`],
    [`Тикет '{ticket.Subject}' передан вам от другого администратора.`, `Zgłoszenie '{ticket.Subject}' przekazane Ci od innego administratora.`],
    [`Тикет '{ticket.Subject}' передан другому админу`, `Zgłoszenie '{ticket.Subject}' przekazane innemu administratorowi`],
    [`Тикет '{ticket.Subject}' передан другому администратору.`, `Zgłoszenie '{ticket.Subject}' zostało przekazane innemu administratorowi.`],
    [`Приоритет тикета '{ticket.Subject}' повышен`, `Priorytet zgłoszenia '{ticket.Subject}' został podwyższony`],
    [`Приоритет вашего тикета '{ticket.Subject}' повышен до '{request.Priority}'.`, `Priorytet Twojego zgłoszenia '{ticket.Subject}' został podwyższony do '{request.Priority}'.`],
    [`Тикет '{ticket.Subject}' эскалирован`, `Zgłoszenie '{ticket.Subject}' eskalowane`],
    [`Тикет '{ticket.Subject}' эскалирован до urgent. Требуется немедленное внимание.`, `Zgłoszenie '{ticket.Subject}' eskalowane do urgent. Wymagana natychmiastowa uwaga.`],
  ]},

  // InvitationsController.cs
  { file: 'DevHunt.CoreApi/Controllers/InvitationsController.cs', pairs: [
    [`📝 Запрос присоединиться к проекту`, `📝 Prośba o dołączenie do projektu`],
    [` на роль: `, ` na rolę: `],
    [`📩 Приглашение в проект`, `📩 Zaproszenie do projektu`],
    [`"Новый запрос"`, `"Nowe zapytanie"`],
    [`"Новое приглашение"`, `"Nowe zaproszenie"`],
    [`Пользователь запросил присоединение к проекту на роль:`, `Użytkownik poprosił o dołączenie do projektu na rolę:`],
    [`Проект приглашает вас на роль:`, `Projekt zaprasza Cię na rolę:`],
  ]},

  // ModerationController.cs
  { file: 'DevHunt.CoreApi/Controllers/ModerationController.cs', pairs: [
    [`Про этот объект уже заведено 3+ жалобы. Ожидается ручная модерация.`, `Ten obiekt ma już 3 lub więcej zgłoszeń. Oczekiwana jest ręczna moderacja.`],
  ]},

  // SupportController.cs
  { file: 'DevHunt.CoreApi/Controllers/SupportController.cs', pairs: [
    [`Новый тикет поддержки: `, `Nowe zgłoszenie wsparcia: `],
    [`Пользователь создал тикет категории`, `Użytkownik utworzył zgłoszenie w kategorii`],
    [`. Приоритет: `, `. Priorytet: `],
    [`"Пользователь"`, `"Użytkownik"`],
    [`Новое сообщение в тикете: `, `Nowa wiadomość w zgłoszeniu: `],
    [` добавил сообщение в тикет '`, ` dodał wiadomość do zgłoszenia '`],
  ]},

  // ProfanityFilter.cs
  { file: 'DevHunt.CoreApi/Filters/ProfanityFilter.cs', pairs: [
    [`Недопустимое содержимое обнаружено (стоп-слова или спам)`, `Prohibited content detected (blocked words or spam)`],
  ]},

  // EncryptionExample.cs
  { file: 'DevHunt.CoreApi/Security/EncryptionExample.cs', pairs: [
    [` * ВАЖНО:`, ` * IMPORTANT:`],
    [` * - Никогда не логируйте расшифрованные токены`, ` * - Never log decrypted tokens`],
    [` * - Храните encryption keys в переменных окружения/KeyVault`, ` * - Store encryption keys in environment variables/KeyVault`],
    [` * - Используйте разные ключи для разных окружений`, ` * - Use different keys for different environments`],
  ]},

  // ValidationAttributes.cs
  { file: 'DevHunt.CoreApi/Security/ValidationAttributes.cs', pairs: [
    [`Содержит недопустимый контент.`, `Zawiera niedozwoloną treść.`],
    [`Длина не должна превышать {_maxLength} символов.`, `Długość nie może przekraczać {_maxLength} znaków.`],
    [`Некорректный URL.`, `Nieprawidłowy URL.`],
  ]},

  // EmailService.cs
  { file: 'DevHunt.CoreApi/Services/EmailService.cs', pairs: [
    [`Привет, {userName}!`, `Cześć, {userName}!`],
    [`Новый проект опубликован: `, `Nowy projekt opublikowany: `],
    [`Открыть проект`, `Otwórz projekt`],
    [`Приглашение в проект`, `Zaproszenie do projektu`],
    [` пригласил(а) вас присоединиться к проекту `, ` zaprasza Cię do projektu `],
    [`Принять приглашение`, `Przyjmij zaproszenie`],
    [`Дайджест уведомлений`, `Podsumowanie powiadomień`],
    [`Привет!`, `Cześć!`],
    [`Ваши непрочитанные уведомления:`, `Twoje nieprzeczytane powiadomienia:`],
    [`Перейти в личный кабинет`, `Przejdź do panelu`],
    [`Добро пожаловать в DevHunt!`, `Witamy w DevHunt!`],
    [`Вы успешно зарегистрировались.`, `Rejestracja przebiegła pomyślnie.`],
    [`Начать работу`, `Rozpocznij`],
    [`DevHunt — Уведомление`, `DevHunt — Powiadomienie`],
    [`Уважаемый пользователь`, `Szanowny użytkowniku`],
  ]},

  // ShowcaseProject.cs
  { file: 'DevHunt.Infrastructure/ShowcaseProject.cs', pairs: [
    [`URL демо-версии проекта.`, `Project demo URL.`],
    [`URL демо-видео проекта.`, `Project demo video URL.`],
    [`URL репозитория проекта (GitHub, GitLab и т.д.).`, `Project repository URL (GitHub, GitLab, etc.).`],
  ]},

  // Skill.cs
  { file: 'DevHunt.Infrastructure/Skill.cs', pairs: [
    [`URL иконки навыка.`, `Skill icon URL.`],
  ]},

  // Dockerfiles
  { file: 'DevHunt.CoreApi/Dockerfile', pairs: [
    [`# ЭТАП СБОРКИ`, `# BUILD STAGE`],
    [`# Копируем файлы проектов`, `# Copy project files`],
    [`# Копируем`, `# Copy`],
    [`# Восстанавливаем`, `# Restore`],
    [`# Собираем`, `# Build`],
    [`# Публикуем`, `# Publish`],
    [`# ЭТАП ВЫПОЛНЕНИЯ`, `# RUNTIME STAGE`],
    [`# Создаём непривилегированного пользователя`, `# Create non-privileged user`],
    [`# Копируем опубликованное приложение`, `# Copy published application`],
    [`# Открываем порт`, `# Expose port`],
    [`# Запускаем приложение`, `# Run application`],
    [`# всё остальное`, `# everything else`],
    [`# Копируем всё остальное и собираем`, `# Copy everything else and build`],
  ]},

  { file: 'DevHunt.AuthService/Dockerfile', pairs: [
    [`# ЭТАП СБОРКИ`, `# BUILD STAGE`],
    [`# Копируем файлы проектов`, `# Copy project files`],
    [`# Копируем`, `# Copy`],
    [`# Восстанавливаем`, `# Restore`],
    [`# Собираем`, `# Build`],
    [`# Публикуем`, `# Publish`],
    [`# ЭТАП ВЫПОЛНЕНИЯ`, `# RUNTIME STAGE`],
    [`# Создаём непривилегированного пользователя`, `# Create non-privileged user`],
    [`# Копируем опубликованное приложение`, `# Copy published application`],
    [`# Открываем порт`, `# Expose port`],
    [`# Запускаем приложение`, `# Run application`],
    [`# всё остальное`, `# everything else`],
    [`# Копируем всё остальное и собираем`, `# Copy everything else and build`],
  ]},

  // ML service
  { file: 'ml-service/requirements.txt', pairs: [
    [`# для будущего расширения ML алгоритмов`, `# for future ML algorithm extensions`],
  ]},

  { file: 'ml-service/routers/ai.py', pairs: [
    [`Отвечай на русском языке`, `Odpowiadaj po polsku`],
    [`на русском`, `po polsku`],
    [`русском языке`, `polsku`],
  ]},

  { file: 'ml-service/services/ai_service.py', pairs: [
    [`без бюджета`, `bez budżetu`],
    [`бесплатн`, `bezpłatn`],
    [`мобильн`, `mobiln`],
    [`телефон`, `telefon`],
    [`планшет`, `tablet`],
    [`андроид`, `android`],
    [`игр`, `gra`],
    [`геймд`, `gamedev`],
    [`нейросет`, `sieć neuronowa`],
    [`машинн`, `uczenie maszynowe`],
    [`десктоп`, `desktopow`],
    [`настольн`, `stacjonarn`],
    [`микроконтроллер`, `mikrokontroler`],
    [`ардуино`, `arduino`],
    [`встроенн`, `wbudowan`],
    [`блокчейн`, `blockchain`],
    [`крипто`, `krypto`],
    [`консольн`, `konsolow`],
    [`терминал`, `terminal`],
    [`команд`, `poleceni`],
  ]},

  { file: 'ml-service/tests/test_ai_service.py', pairs: [
    [`Сделай приложение без бюджета`, `Zrób aplikację bez budżetu`],
    [`Создать задачу`, `Utwórz zadanie`],
    [`Сделай мобильное приложение`, `Zrób aplikację mobilną`],
  ]},

  // notification-service
  { file: 'notification-service/src/index.js', pairs: [
    [`// CORS configuration — ограничить origins`, `// CORS configuration — restrict origins`],
    [`// Ограничить origins из переменных окружения`, `// Restrict origins from environment variables`],
  ]},

  { file: 'notification-service/src/services/emailService.js', pairs: [
    [`// Поддержка SendGrid и SMTP`, `// SendGrid and SMTP support`],
  ]},

  { file: 'notification-service/src/services/smsService.js', pairs: [
    [`Отправка SMS уведомлений`, `Send SMS notifications`],
    [`через Twilio`, `via Twilio`],
    [`Инициализация Twilio клиента`, `Initialize Twilio client`],
    [`Отправить SMS уведомление`, `Send SMS notification`],
    [`Номер телефона получателя`, `Recipient phone number`],
    [`Текст сообщения`, `Message text`],
    [`Результат отправки`, `Send result`],
  ]},

  { file: 'notification-service/src/services/pushService.js', pairs: [
    [`Отправка push уведомлений`, `Send push notifications`],
    [`через Firebase Cloud Messaging`, `via Firebase Cloud Messaging`],
    [`Инициализация Firebase Admin SDK`, `Initialize Firebase Admin SDK`],
    [`Отправить push уведомление`, `Send push notification`],
    [`Токен устройства`, `Device token`],
    [`Заголовок уведомления`, `Notification title`],
    [`Текст уведомления`, `Notification text`],
    [`Дополнительные данные`, `Additional data`],
    [`Результат отправки`, `Send result`],
  ]},

  { file: 'notification-service/src/services/rabbitmqConsumer.js', pairs: [
    [`Подписывается на события`, `Subscribes to events`],
    [`из RabbitMQ`, `from RabbitMQ`],
    [`Обработка входящего события`, `Process incoming event`],
    [`Подключение к RabbitMQ`, `Connect to RabbitMQ`],
    [`Успешно подключено к RabbitMQ`, `Successfully connected to RabbitMQ`],
    [`Ошибка подключения к RabbitMQ`, `RabbitMQ connection error`],
  ]},

  // integration-gateway
  { file: 'integration-gateway/src/index.js', pairs: [
    [`// Node.js сервис для OAuth, Webhooks`, `// Node.js service for OAuth, Webhooks`],
    [`и интеграций`, `and integrations`],
  ]},

  { file: 'integration-gateway/src/routes/oauth.js', pairs: [
    [`для обратной совместимости`, `for backward compatibility`],
  ]},

  { file: 'integration-gateway/src/routes/webhooks.js', pairs: [
    [`Signature verification для GitHub`, `Signature verification for GitHub`],
  ]},

  { file: 'integration-gateway/src/services/syncService.js', pairs: [
    [`Синхронизация данных`, `Data synchronization`],
    [`между DevHunt и внешними платформами`, `between DevHunt and external platforms`],
    [`Синхронизировать данные`, `Synchronize data`],
    [`Идентификатор интеграции`, `Integration identifier`],
    [`Тип синхронизации`, `Synchronization type`],
    [`Результат синхронизации`, `Synchronization result`],
  ]},

  { file: 'integration-gateway/src/services/eventConsumer.js', pairs: [
    [`Подписывается на события из RabbitMQ`, `Subscribes to events from RabbitMQ`],
    [`Обработка входящего события`, `Process incoming event`],
    [`Подключение к RabbitMQ`, `Connect to RabbitMQ`],
  ]},

  // nginx
  { file: 'nginx/nginx.conf', pairs: [
    [`# DNS resolver для динамического разрешения`, `# DNS resolver for dynamic resolution`],
    [`# имён сервисов Docker`, `# of Docker service names`],
  ]},

  // docker-compose.yml
  { file: 'docker-compose.yml', pairs: [
    [`# Основная база данных`, `# Main database`],
    [`# Redis для кэширования`, `# Redis for caching`],
    [`# Брокер сообщений`, `# Message broker`],
    [`# Объектное хранилище`, `# Object storage`],
  ]},

  // monitoring
  { file: 'monitoring/prometheus.yml', pairs: [
    [`# AlertManager закомментирован`, `# AlertManager commented out`],
  ]},

  { file: 'monitoring/openobserve/scrape.yml', pairs: [
    [`# Этот файл определяет откуда собирать метрики`, `# This file defines where to collect metrics from`],
  ]},

  { file: 'monitoring/openobserve/init-alerts.sh', pairs: [
    [`# Запускается после старта OpenObserve`, `# Runs after OpenObserve starts`],
  ]},

  // frontend remaining
  { file: 'frontend/tests/e2e/auth.spec.ts', pairs: [
    [`Пароли не совпадают`, `Hasła nie są identyczne`],
  ]},

  { file: 'frontend/src/components/profile/activity-card/news-helpers.ts', pairs: [
    [`Опубликована новость`, `Opublikowano wiadomość`],
  ]},

  // scripts
  { file: 'scripts/setup-buildkit.ps1', pairs: [
    [`Настройка BuildKit глобально`, `Setting up BuildKit globally`],
    [`BuildKit уже включен`, `BuildKit is already enabled`],
    [`BuildKit успешно настроен`, `BuildKit configured successfully`],
  ]},
];

let totalReplacements = 0;
let filesProcessed = 0;

for (const { file, pairs } of replacements) {
  const fullPath = resolve(REPO_ROOT, file);
  if (!existsSync(fullPath)) {
    console.log(`SKIP: ${file} (not found)`);
    continue;
  }
  let content = readFileSync(fullPath, 'utf-8');
  let fileReplacements = 0;
  for (const [oldStr, newStr] of pairs) {
    const count = content.split(oldStr).length - 1;
    if (count > 0) {
      content = content.replaceAll(oldStr, newStr);
      fileReplacements += count;
    }
  }
  if (fileReplacements > 0) {
    writeFileSync(fullPath, content, 'utf-8');
    totalReplacements += fileReplacements;
    filesProcessed++;
    console.log(`OK: ${file} (${fileReplacements} replacements)`);
  } else {
    console.log(`NOCHANGE: ${file}`);
  }
}

console.log(`\nTotal: ${totalReplacements} replacements in ${filesProcessed} files`);
