import { readFileSync, writeFileSync } from 'fs';

const replacements = [
  // ====== docker-compose.yml ======
  {
    file: 'docker-compose.yml',
    pairs: [
      ['(можно изменить через переименование сервиса и обновление зависимостей)', '(can be changed by renaming the service and updating dependencies)'],
      ['(можно изменить через переименование сервиса)', '(can be changed by renaming the service)'],
    ]
  },
  // ====== CoreApi Dockerfile ======
  {
    file: 'DevHunt.CoreApi/Dockerfile',
    pairs: [
      ['# Copy .slnx И ВСЕ .csproj СРАЗУ для оптимизации кэша слоев', '# Copy .slnx and ALL .csproj at once to optimize layer caching'],
      ['# Restore зависимости ДЛЯ ВСЕГО РЕШЕНИЯ', '# Restore dependencies for the ENTIRE solution'],
      ['# Используем кэш NuGet для ускорения сборки', '# Use NuGet cache to speed up the build'],
      ['# Copy ВЕСЬ исходный код', '# Copy ALL source code'],
      ['# Publish (dotnet publish сам сделает restore если нужно)', '# Publish (dotnet publish will restore if needed)'],
      ['# Убираем --no-restore чтобы избежать проблем с отсутствующими пакетами', '# Removed --no-restore to avoid issues with missing packages'],
      ['# ФИНАЛЬНЫЙ ЭТАП', '# FINAL STAGE'],
      ['# Объединяем команды для уменьшения количества слоев', '# Combine commands to reduce number of layers'],
      ['# Copy собранное приложение', '# Copy published application'],
    ]
  },
  // ====== AuthService Dockerfile ======
  {
    file: 'DevHunt.AuthService/Dockerfile',
    pairs: [
      ['# Copy .slnx И ВСЕ .csproj СРАЗУ для оптимизации кэша слоев', '# Copy .slnx and ALL .csproj at once to optimize layer caching'],
      ['# Restore зависимости ДЛЯ ВСЕГО РЕШЕНИЯ', '# Restore dependencies for the ENTIRE solution'],
      ['# Copy ВЕСЬ исходный код', '# Copy ALL source code'],
      ['# Publish (dotnet publish сам сделает restore если нужно)', '# Publish (dotnet publish will restore if needed)'],
      ['# Убираем --no-restore чтобы избежать проблем с отсутствующими пакетами', '# Removed --no-restore to avoid issues with missing packages'],
      ['# ФИНАЛЬНЫЙ ЭТАП', '# FINAL STAGE'],
      ['# Объединяем команды для уменьшения количества слоев', '# Combine commands to reduce number of layers'],
      ['# Copy собранное приложение', '# Copy published application'],
    ]
  },
  // ====== Test files ======
  {
    file: 'DevHunt.CoreApi.Tests/Controllers/ProfileControllerTests.cs',
    pairs: [
      ['/// Unit тесты для ProfileController', '/// Unit tests for ProfileController'],
      ['// Настройка User из Claims', '// Setup User from Claims'],
      ['// Проверка сохранения в БД', '// Verify saving to DB'],
    ]
  },
  {
    file: 'DevHunt.CoreApi.Tests/Controllers/InvitationsControllerTests.cs',
    pairs: [
      ['/// Unit тесты для InvitationsController', '/// Unit tests for InvitationsController'],
      ['// Настройка User из Claims', '// Setup User from Claims'],
      ['// Проверка сохранения в БД', '// Verify saving to DB'],
    ]
  },
  {
    file: 'DevHunt.CoreApi.Tests/Unit/ChatControllerTests.cs',
    pairs: [
      ['// Setup SignalR Hub mocks для SendMessage', '// Setup SignalR Hub mocks for SendMessage'],
      ['// Настраиваем цепочку: Clients -> Group() -> SendAsync()', '// Setup chain: Clients -> Group() -> SendAsync()'],
      ['// Act - используем существующий метод GetOrCreateDirectConversation', '// Act - use existing method GetOrCreateDirectConversation'],
      ['// Используем правильный DTO из ChatController', '// Use the correct DTO from ChatController'],
      ['// Проверяем что контент был зашифрован', '// Verify that content was encrypted'],
      ['// Проверяем что SignalR был вызван', '// Verify that SignalR was called'],
    ]
  },
  {
    file: 'DevHunt.CoreApi.Tests/Services/EventBusServiceTests.cs',
    pairs: [
      ['/// Unit тесты для EventBusService', '/// Unit tests for EventBusService'],
    ]
  },
  {
    file: 'DevHunt.CoreApi.Tests/Services/CacheServiceTests.cs',
    pairs: [
      ['/// Unit тесты для CacheService', '/// Unit tests for CacheService'],
    ]
  },
  {
    file: 'DevHunt.CoreApi.Tests/Integration/ProjectsControllerIntegrationTests.cs',
    pairs: [
      ['/// Integration тесты для ProjectsController.', '/// Integration tests for ProjectsController.'],
      ['/// Тестируют реальные HTTP запросы к API с InMemory базой данных.', '/// Tests real HTTP requests to API with InMemory database.'],
    ]
  },
  {
    file: 'DevHunt.AuthService.Tests/AuthControllerTests.cs',
    pairs: [
      ['/// Тесты для AuthController, включая новый функционал refresh tokens.', '/// Tests for AuthController, including refresh tokens functionality.'],
    ]
  },
  // ====== notification-service smsService ======
  {
    file: 'notification-service/src/services/smsService.js',
    pairs: [
      ['* Send SMS notifications via Twilio или другой провайдер', '* Send SMS notifications via Twilio or another provider'],
      ['* Отправить SMS', '* Send SMS'],
      ['// Валидация номера телефона (базовая)', '// Phone number validation (basic)'],
      ['* Отправка via Twilio', '* Send via Twilio'],
      ['* Отправка через AWS SNS', '* Send via AWS SNS'],
      ['* Mock отправка (для разработки)', '* Mock send (for development)'],
      ['// В development mode просто логируем', '// In development mode just log it'],
    ]
  },
  // ====== integration-gateway syncService ======
  {
    file: 'integration-gateway/src/services/syncService.js',
    pairs: [
      ['* Data synchronization между DevHunt и внешними сервисами (GitHub, GitLab)', '* Data synchronization between DevHunt and external services (GitHub, GitLab)'],
      ['* Synchronize data из внешнего сервиса', '* Synchronize data from an external service'],
      ['* @param {string} integrationId - ID интеграции в DevHunt', '* @param {string} integrationId - Integration ID in DevHunt'],
      ['* @param {string} serviceType - Тип сервиса (github, gitlab)', '* @param {string} serviceType - Service type (github, gitlab)'],
      ['* @param {object} config - Конфигурация интеграции (из Integration.Config)', '* @param {object} config - Integration configuration (from Integration.Config)'],
      ['* @param {string} accessToken - Access token для внешнего API', '* @param {string} accessToken - Access token for external API'],
    ]
  },
  // ====== CSS ======
  {
    file: 'frontend/src/app/[locale]/dashboard/chats/chat.module.css',
    pairs: [
      ['/* Хотбар слева */', '/* Left hotbar */'],
      ['/* Боковая панель со списком чатов */', '/* Sidebar with chat list */'],
      ['/* Основное окно чатов */', '/* Main chat window */'],
    ]
  },
];

let totalReplacements = 0;
let totalFiles = 0;

for (const { file, pairs } of replacements) {
  try {
    let content = readFileSync(file, 'utf-8');
    let fileReplacements = 0;
    for (const [from, to] of pairs) {
      const count = content.split(from).length - 1;
      if (count > 0) {
        content = content.replaceAll(from, to);
        fileReplacements += count;
      }
    }
    if (fileReplacements > 0) {
      writeFileSync(file, content, 'utf-8');
      console.log(`✅ ${file}: ${fileReplacements} replacements`);
      totalReplacements += fileReplacements;
      totalFiles++;
    } else {
      console.log(`⚠️  ${file}: NOCHANGE`);
    }
  } catch (e) {
    console.log(`❌ ${file}: ${e.message}`);
  }
}

console.log(`\nDone: ${totalReplacements} replacements in ${totalFiles} files`);
