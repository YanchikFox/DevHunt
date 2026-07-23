# Chat System - Technical Documentation

## Overview

Real-time chat system with SignalR WebSocket integration for direct and group conversations.

## Backend (ASP.NET Core)

### Models (Existing)

- `Conversation` - Chat container (Direct/Group types)
- `ConversationParticipant` - User participation with read tracking
- `Message` - Chat messages with reply/edit support

### SignalR Hub: ChatHub

**Endpoint:** `wss://api.devhunt.com/chatHub`

**Methods:**

- `JoinConversation(Guid conversationId)` - Subscribe to chat updates
- `LeaveConversation(Guid conversationId)` - Unsubscribe from chat
- `SendMessage(Guid conversationId, string content)` - Send message (broadcasts to group)
- `MarkAsRead(Guid conversationId)` - Update LastReadAt timestamp
- `Typing(Guid conversationId, bool isTyping)` - Send typing indicator

**Events (Server → Client):**

- `ReceiveMessage(ChatMessage)` - New message received
- `MessageRead(userId, readAt)` - User read messages
- `UserTyping(userId, isTyping)` - User typing indicator
- `UserJoined(userId, username)` - User joined chat
- `UserLeft(userId)` - User left chat

### REST API: ChatController

| Метод  | URL                                                              | Описание                                                                        |
| ------ | ---------------------------------------------------------------- | ------------------------------------------------------------------------------- |
| GET    | `/api/chat/conversations`                                        | Перечисляет все чаты пользователя с флагами unread и информацией об участниках. |
| GET    | `/api/chat/conversations/{conversationId}/messages`              | Возвращает историю сообщений с пагинацией (page, pageSize, max 100).            |
| POST   | `/api/chat/conversations/direct/{otherUserId}`                   | Создаёт или возвращает существующий личный чат (с учетом privacy).              |
| POST   | `/api/chat/conversations/project/{projectId}`                    | Получает или создаёт проектный чат (только участникам проекта).                 |
| POST   | `/api/chat/conversations/group`                                  | Создаёт групповый чат с заголовком и списком членов.                            |
| POST   | `/api/chat/conversations/{conversationId}/messages`              | Отправляет сообщение (шифруется, модерация + SignalR).                          |
| PUT    | `/api/chat/messages/{messageId}`                                 | Редактирует собственное сообщение (повторное шифрование).                       |
| DELETE | `/api/chat/messages/{messageId}`                                 | Soft delete сообщения + рассылка события `MessageDeleted`.                      |
| GET    | `/api/chat/conversations/{conversationId}/participants`          | Список участников с LastRead/mute.                                              |
| POST   | `/api/chat/conversations/{conversationId}/participants/{userId}` | Добавление участника в групповой чат.                                           |
| DELETE | `/api/chat/conversations/{conversationId}/participants/{userId}` | Удаление участника или самоуход.                                                |
| POST   | `/api/chat/conversations/{conversationId}/leave`                 | Выход из группового чата (оставшийся участник).                                 |
| PUT    | `/api/chat/conversations/{conversationId}/mute`                  | Переключение mute-флага для текущего пользователя.                              |

### Automation

**On Project Creation:**

```csharp
// Auto-creates group chat with ID = ProjectId
var groupChat = new Conversation {
    Id = project.Id,
    Type = ConversationType.Group,
    Title = project.Title
};
```

**On Invitation Sent:**

```csharp
// Creates direct chat with system message
var systemMessage = new Message {
    Content = "📩 Приглашение в проект...",
    ReplyToId = invitation.Id // Links to invitation
};
```

**On Invitation Accepted:**

```csharp
// Auto-adds user to project group chat
_db.ConversationParticipants.Add(new ConversationParticipant {
    ConversationId = project.Id,
    UserId = acceptedUserId
});
```

## Frontend (Next.js + React)

### Files Structure

```
src/
├── lib/
│   └── signalr.ts              # SignalR connection & types
├── hooks/
│   └── useChat.ts              # React hook for chat functionality
├── components/
│   └── chat/
│       ├── ChatWindow.tsx      # Main chat UI
│       ├── ConversationList.tsx # Chat list sidebar
│       └── SendMessageButton.tsx # Profile "Write Message" button
└── app/
    └── [locale]/
        └── dashboard/
            └── chats/
                └── page.tsx    # Chat page (/dashboard/chats)
```

### SignalR Integration

**Connection:**

```typescript
import { startChatConnection, sendMessage } from "@/lib/signalr";

// Auto-connects with JWT
const conn = await startChatConnection(session.accessToken);

// Send message
await sendMessage(conversationId, "Hello!");
```

**Events:**

```typescript
import { onReceiveMessage, onUserTyping } from "@/lib/signalr";

onReceiveMessage((msg) => {
  setMessages((prev) => [...prev, msg]);
});

onUserTyping((userId, isTyping) => {
  // Show "User is typing..."
});
```

### React Hook: useChat

```typescript
const {
  connected, // SignalR connection status
  messages, // Real-time messages
  typingUsers, // Set of typing user IDs
  sendMessage, // Send message function
  markMessagesAsRead, // Mark as read
  setTyping, // Send typing indicator
} = useChat(conversationId);
```

**Auto-features:**

- Connects to SignalR on mount
- Joins conversation automatically
- Merges history + live messages
- Cleans up on unmount

### Components

**ChatWindow:**

- Message history with infinite scroll
- Real-time message delivery
- Typing indicators
- System messages with Accept/Decline buttons (via `replyToId`)
- Avatar display
- Timestamp formatting

**ConversationList:**

- Shows all user chats
- Unread count badges
- Last message preview
- Direct chat: Shows other user's name/avatar
- Group chat: Shows project title

**SendMessageButton:**

- Creates direct chat with user
- Privacy check (403 if restricted)
- Opens ChatWindow in dialog
- Redirects to /dashboard/chats if chat exists

### Pages

**/dashboard/chats**

- Grid layout: ConversationList (1/3) + ChatWindow (2/3)
- Mobile: Stack vertically
- URL param: `?conversation={id}` for direct link

## Privacy Settings

**UserPrivacySettings.ProfileVisibility:**

- `public` - Anyone can message
- `friends_only` - Only friends (future)
- `project_members` - Only shared project members

Backend enforces in `POST /api/chat/conversations/direct/{otherUserId}`

## System Messages

Messages with `ReplyToId = InvitationId` are rendered as system messages with action buttons:

```tsx
{
  isInvitation && (
    <div className="mt-2 flex gap-2">
      <Button onClick={() => acceptInvitation(msg.replyToId)}>Принять</Button>
      <Button
        variant="outline"
        onClick={() => declineInvitation(msg.replyToId)}
      >
        Отклонить
      </Button>
    </div>
  );
}
```

Frontend can call `POST /api/invitations/respond` with `InvitationId` from `replyToId`.

## Environment Variables

**Backend (.env):**

```env
ASPNETCORE_URLS=http://+:5000
ConnectionStrings__DefaultConnection=...
JWT__Secret=...
```

**Frontend (.env.local):**

```env
NEXT_PUBLIC_API_URL=http://localhost:5000
NEXTAUTH_SECRET=...
```

## Docker

**Backend:**

```bash
docker-compose build core-api
docker-compose up -d core-api
```

**Frontend:**

```bash
docker-compose build frontend
docker-compose up -d frontend
```

## Testing

**SignalR Connection:**

```bash
# Check hub endpoint
curl -i http://localhost:5000/chatHub
# Should return 400 (WebSocket upgrade required)
```

**REST API:**

```bash
# Get conversations (polling at /api/chat/conversations)
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/chat/conversations

# Create direct chat
curl -X POST -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/chat/conversations/direct/<otherUserId>
```

## Troubleshooting

**SignalR не подключается:**

- Проверь JWT токен: должен быть свежий
- CORS: убедись что `AllowCredentials()` включен
- WebSocket: nginx должен проксировать wss://

**Сообщения не приходят:**

- Проверь что пользователь вызвал `JoinConversation(id)`
- Проверь что группа называется `conversation:{id}`
- Проверь логи SignalR: `LogLevel.Debug`

**Непрочитанные не обновляются:**

- Фронтенд должен вызывать `markMessagesAsRead()` при открытии чата
- Backend обновляет `ConversationParticipant.LastReadAt`
- Подсчет: `messages.Count(m => m.CreatedAt > LastReadAt)`

## Future Improvements

- [ ] Message editing (IsEdited flag exists)
- [ ] Message deletion (soft delete via IsDeleted)
- [ ] File attachments (add AttachmentUrl to Message)
- [ ] Voice messages
- [ ] Read receipts (show checkmarks)
- [ ] Message reactions
- [ ] Search in chat history
- [ ] Archive chats
- [ ] Mute notifications (IsMuted exists)
- [ ] Push notifications via browser API
