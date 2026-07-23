/**
 * Script to generate pl.json from en.json with Polish translations.
 * Uses en.json structure as base and applies Polish translations.
 */
import { readFileSync, writeFileSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const messagesDir = join(__dirname, '..', 'frontend', 'messages');

const en = JSON.parse(readFileSync(join(messagesDir, 'en.json'), 'utf-8'));

// Deep clone
const pl = JSON.parse(JSON.stringify(en));

// Translation map: English -> Polish
const translations = {
  // common
  "Loading...": "Ładowanie...",
  "Error": "Błąd",
  "Success": "Sukces",
  "Cancel": "Anuluj",
  "Save": "Zapisz",
  "Delete": "Usuń",
  "Edit": "Edytuj",
  "Create": "Utwórz",
  "Submit": "Wyślij",
  "Close": "Zamknij",
  "Generate": "Generuj",
  "Search": "Szukaj",
  "Actions": "Akcje",
  "Filter": "Filtr",
  "Apply": "Zastosuj",
  "Clear": "Wyczyść",
  "Change language": "Zmień język",
  "Toggle theme": "Przełącz motyw",
  "Confirm": "Potwierdź",
  "Updating...": "Aktualizowanie...",
  "Processing...": "Przetwarzanie...",
  "Creating...": "Tworzenie...",
  "Show more": "Pokaż więcej",
  "Unknown user": "Nieznany użytkownik",
  "N/A": "N/D",
  "Cancelling...": "Anulowanie...",
  "Something went wrong!": "Coś poszło nie tak!",
  "Stack trace": "Stos wywołań",
  "Try again": "Spróbuj ponownie",
  "Go back": "Wróć",
  "Go home": "Strona główna",
  "An unexpected error occurred. Please try refreshing the page.": "Wystąpił nieoczekiwany błąd. Spróbuj odświeżyć stronę.",
  "View all": "Pokaż wszystko",
  "No results found": "Nie znaleziono wyników",
  "Failed to load": "Nie udało się załadować",
  "Loading data...": "Ładowanie danych...",
  "Redirecting to login...": "Przekierowanie do logowania...",
  "View": "Widok",
  "Details": "Szczegóły",
  "No data yet": "Brak danych",
  "Profile error": "Błąd profilu",
  "Add column": "Dodaj kolumnę",
  "Add Bio": "Dodaj opis",
  "Add Skills": "Dodaj umiejętności",
  "View Project": "Zobacz projekt",
  "View Stack Trace": "Pokaż stos wywołań",
  "Error Message": "Komunikat błędu",
  "Error ID": "ID błędu",
  "Searching…": "Szukanie…",
  "Confirm Block": "Potwierdź blokadę",
  "Confirm Password": "Potwierdź hasło",
  "Download SVG": "Pobierz SVG",
  "Project Demo": "Demo projektu",
  "AI Architect Assistant": "Asystent AI Architekt",
  "Previous": "Poprzedni",
  "Next": "Następny",
  "Clear filters": "Wyczyść filtry",
  "Retry": "Ponów",
  "Upload": "Prześlij",
  "Uploading...": "Przesyłanie...",
  "Accept": "Akceptuj",
  "Decline": "Odrzuć",
  
  // loading
  "Loading chat...": "Ładowanie czatu...",
  "Loading recommendations.": "Ładowanie rekomendacji.",
  "Loading invitations...": "Ładowanie zaproszeń...",
  "Loading diff viewer...": "Ładowanie podglądu zmian...",
  "Loading GitHub integration...": "Ładowanie integracji GitHub...",
  "Loading Kanban board...": "Ładowanie tablicy Kanban...",
  "Loading activity heatmap...": "Ładowanie mapy aktywności...",
  "Loading code quality insights...": "Ładowanie analizy jakości kodu...",
  "Loading attachments...": "Ładowanie załączników...",
  
  // emptyStates
  "No columns yet": "Brak kolumn",
  "No badges earned yet": "Brak zdobytych odznak",
  "No activity to show yet.": "Brak aktywności do wyświetlenia.",
  "No completed projects": "Brak ukończonych projektów",
  "No attachments yet.": "Brak załączników.",
  "No bio added yet": "Opis nie został jeszcze dodany",
  "No skills added yet": "Umiejętności nie zostały jeszcze dodane",
  "No incoming invitations or join requests yet.": "Brak przychodzących zaproszeń lub wniosków o dołączenie.",
  "No sent invitations or join requests yet.": "Brak wysłanych zaproszeń lub wniosków o dołączenie.",
  "No achievements yet": "Brak osiągnięć",
  "No showcase projects yet": "Brak projektów w witrynie",
  
  // placeholders
  "Select category": "Wybierz kategorię",
  "Describe this file...": "Opisz ten plik...",
  "Search tasks... (press /)": "Szukaj zadań... (naciśnij /)",
  "Write your documentation here (Markdown supported)...": "Napisz dokumentację tutaj (obsługa Markdown)...",
  "Document title": "Tytuł dokumentu",
  "Initial content...": "Początkowa treść...",
  "Frontend Developer, Backend Developer": "Programista Frontend, Programista Backend",
  "Select a column": "Wybierz kolumnę",
  "All": "Wszystko",
  "Choose visibility": "Wybierz widoczność",
  "John Doe": "Jan Kowalski",
  "Tell us about yourself...": "Opowiedz o sobie...",
  "e.g., Europe/London, America/New_York": "np. Europe/Warsaw, America/New_York",
  "e.g., English, Spanish": "np. polski, angielski",
  "https://github.com/username": "https://github.com/nazwa-użytkownika",
  "https://linkedin.com/in/username": "https://linkedin.com/in/nazwa-użytkownika",
  "https://yourwebsite.com": "https://twoja-strona.com",
  "e.g., 5": "np. 5",
  "e.g., first_project": "np. first_project",
  "e.g., projects": "np. projects",
  "Filter by Role": "Filtruj wg roli",
  "Filter by Status": "Filtruj wg statusu",
  "Reason for blocking": "Powód blokady",
  "Select Badge": "Wybierz odznakę",
  "Badge Title": "Nazwa odznaki",
  "Badge Description": "Opis odznaki",
  "Type a skill (e.g., React, C#, .NET)": "Wpisz umiejętność (np. React, C#, .NET)",
  "Experience Level": "Poziom doświadczenia",
  "All Status": "Wszystkie statusy",
  "My Awesome Project": "Mój super projekt",
  "Describe your project...": "Opisz swój projekt...",
  "Search projects...": "Szukaj projektów...",
  "Enter the 6-digit code": "Wprowadź 6-cyfrowy kod",
  "Tell us about yourself, your interests, and what you're passionate about...": "Opowiedz o sobie, swoich zainteresowaniach i pasjach...",
  
  // dialogs
  "Delete Document?": "Usunąć dokument?",
  "Delete Task?": "Usunąć zadanie?",
  "Delete Column": "Usuń kolumnę",
  "Create Document": "Utwórz dokument",
  "New Project": "Nowy projekt",
  "Create a new project and start building with your team": "Utwórz nowy projekt i zacznij budować ze swoim zespołem",
  
  // privacy
  "Profile visibility": "Widoczność profilu",
  "Show email": "Pokaż e-mail",
  "Display your email on your public profile.": "Wyświetl swój e-mail na publicznym profilu.",
  "Show social links": "Pokaż linki społecznościowe",
  "Allow others to see your GitHub, LinkedIn and website.": "Pozwól innym zobaczyć Twój GitHub, LinkedIn i stronę.",
  "Show skills": "Pokaż umiejętności",
  "Show your listed skills to visitors.": "Pokaż swoje umiejętności odwiedzającym.",
  "Show experience": "Pokaż doświadczenie",
  "Display your experience and seniority.": "Wyświetl swoje doświadczenie i staż.",
  "Show projects": "Pokaż projekty",
  "Display your public projects on your profile.": "Wyświetl publiczne projekty na swoim profilu.",
  "Show achievements": "Pokaż osiągnięcia",
  "Show achievements and badges on your profile.": "Pokaż osiągnięcia i odznaki na swoim profilu.",
  "Privacy Settings": "Ustawienia prywatności",
  "Control who can see your profile and activity.": "Kontroluj, kto może zobaczyć Twój profil i aktywność.",
  "Activity visibility": "Widoczność aktywności",
  "Followers": "Obserwujący",
  "Settings saved": "Ustawienia zapisane",
  "Your privacy settings have been updated.": "Twoje ustawienia prywatności zostały zaktualizowane.",
  "Failed to save settings": "Nie udało się zapisać ustawień",
  "Manage the visibility of your profile, activity, and data.": "Zarządzaj widocznością swojego profilu, aktywności i danych.",
  "Saving...": "Zapisywanie...",
  "Changes apply immediately after saving. Use settings to control access to your data and activity.": "Zmiany obowiązują natychmiast po zapisaniu. Użyj ustawień, aby kontrolować dostęp do swoich danych i aktywności.",
  
  // stats
  "Following": "Obserwowani",
  "Projects": "Projekty",
  "Activity (30d)": "Aktywność (30d)",
  "User stats": "Statystyki użytkownika",
  "Contribution": "Wkład",
  "Community": "Społeczność",
  "Consistency (14d)": "Konsekwencja (14d)",
  "days": "dni",
  
  // profile
  "Edit Profile": "Edytuj profil",
  "Update your name, avatar, and profile details.": "Zaktualizuj swoje imię, awatar i dane profilu.",
  "Profile Details": "Szczegóły profilu",
  "Profile Picture": "Zdjęcie profilowe",
  "Profile is private": "Profil jest prywatny",
  "Skills": "Umiejętności",
  "Create and manage your development projects": "Twórz i zarządzaj swoimi projektami",
  "Basic Information": "Podstawowe informacje",
  "Full Name": "Imię i nazwisko",
  "Bio": "Opis",
  "Maximum {count} characters": "Maksymalnie {count} znaków",
  "Timezone": "Strefa czasowa",
  "Preferred Language": "Preferowany język",
  "Social Links": "Linki społecznościowe",
  "GitHub": "GitHub",
  "LinkedIn": "LinkedIn",
  "Website": "Strona internetowa",
  "Skills & Experience": "Umiejętności i doświadczenie",
  "Years of Experience": "Lata doświadczenia",
  "Joined": "Dołączył",
  "Verified": "Zweryfikowany",
  "Inactive": "Nieaktywny",
  "Privacy": "Prywatność",
  "profileNotFound": "Profil nie został znaleziony",
  "profileNotFoundDesc": "Użytkownik nie istnieje lub został usunięty.",
  "Online": "Online",
  "Offline": "Offline",
  
  // auth
  "Sign In": "Zaloguj się",
  "Sign Up": "Zarejestruj się",
  "Sign Out": "Wyloguj się",
  "Log In": "Zaloguj się",
  "Register": "Rejestracja",
  "Email": "E-mail",
  "Password": "Hasło",
  "Forgot Password?": "Zapomniałeś hasła?",
  "Don't have an account?": "Nie masz konta?",
  "Already have an account?": "Masz już konto?",
  "Remember me": "Zapamiętaj mnie",
  "Email address": "Adres e-mail",
  "Enter your email": "Wprowadź swój e-mail",
  "Enter your password": "Wprowadź swoje hasło",
  "Enter your name": "Wprowadź swoje imię",
  "Sign in with Google": "Zaloguj się przez Google",
  "Sign in with GitHub": "Zaloguj się przez GitHub",
  "or continue with": "lub kontynuuj przez",
  "Signing in...": "Logowanie...",
  "Registering...": "Rejestracja...",
  "Platform for developers": "Platforma dla programistów",
  "Log in to your account to access all features": "Zaloguj się na swoje konto, aby uzyskać dostęp do wszystkich funkcji",
  "Create an account to start collaborating": "Utwórz konto, aby zacząć współpracować",
  "Find and join projects from the developer community": "Znajdź i dołącz do projektów ze społeczności programistów",
  "Verification": "Weryfikacja",
  "Enter the verification code sent to your email": "Wprowadź kod weryfikacyjny wysłany na Twój e-mail",
  "Verify": "Zweryfikuj",
  "Verifying...": "Weryfikacja...",
  "Resend code": "Wyślij ponownie kod",
  "Code resent": "Kod wysłany ponownie",
  "Invalid verification code": "Nieprawidłowy kod weryfikacyjny",
  
  // navigation
  "Dashboard": "Panel",
  "My Projects": "Moje projekty",
  "Explore": "Odkrywaj",
  "Settings": "Ustawienia",
  "Profile": "Profil",
  "Notifications": "Powiadomienia",
  "Messages": "Wiadomości",
  "Help": "Pomoc",
  "Showcase": "Witryna",
  "Community": "Społeczność",
  "Internships": "Staże",
  
  // projects
  "Project Name": "Nazwa projektu",
  "Description": "Opis",
  "Tech Stack": "Stos technologiczny",
  "Status": "Status",
  "Visibility": "Widoczność",
  "Members": "Członkowie",
  "Tasks": "Zadania",
  "Owner": "Właściciel",
  "Created": "Utworzony",
  "Updated": "Zaktualizowany",
  "active": "aktywny",
  "completed": "ukończony",
  "archived": "zarchiwizowany",
  "planning": "planowanie",
  "public": "publiczny",
  "private": "prywatny",
  "Join Project": "Dołącz do projektu",
  "Leave Project": "Opuść projekt",
  "Delete Project": "Usuń projekt",
  "Project Settings": "Ustawienia projektu",
  "Are you sure you want to delete this project?": "Czy na pewno chcesz usunąć ten projekt?",
  "Project created successfully": "Projekt utworzony pomyślnie",
  "Project updated successfully": "Projekt zaktualizowany pomyślnie",
  "Project deleted successfully": "Projekt usunięty pomyślnie",
  "Open": "Otwarte",
  "In Progress": "W trakcie",
  "Done": "Gotowe",
  "Backlog": "Backlog",
  "Team": "Zespół",
  "Activity": "Aktywność",
  "Overview": "Przegląd",
  "News": "Aktualności",
  "Artifacts": "Artefakty",
  
  // tasks
  "Task Name": "Nazwa zadania",
  "Priority": "Priorytet",
  "Assignee": "Przypisany",
  "Due Date": "Termin",
  "high": "wysoki",
  "medium": "średni",
  "low": "niski",
  "urgent": "pilny",
  "Add Task": "Dodaj zadanie",
  "Create Task": "Utwórz zadanie",
  "Edit Task": "Edytuj zadanie",
  "Task created successfully": "Zadanie utworzone pomyślnie",
  "Task updated successfully": "Zadanie zaktualizowane pomyślnie",
  "Task deleted successfully": "Zadanie usunięte pomyślnie",
  "No tasks yet": "Brak zadań",
  "Estimated Hours": "Szacowane godziny",
  "Actual Hours": "Rzeczywiste godziny",
  "Tags": "Tagi",
  "Deadline": "Termin",
  
  // team
  "Team Members": "Członkowie zespołu",
  "Invite Member": "Zaproś członka",
  "Remove Member": "Usuń członka",
  "Change Role": "Zmień rolę",
  "developer": "programista",
  "designer": "projektant",
  "leader": "lider",
  "mentor": "mentor",
  "tester": "tester",
  "analyst": "analityk",
  "devops": "devops",
  "Role": "Rola",
  
  // notifications
  "Mark as read": "Oznacz jako przeczytane",
  "Mark all as read": "Oznacz wszystko jako przeczytane",
  "No notifications": "Brak powiadomień",
  "New notification": "Nowe powiadomienie",
  
  // chat/messages
  "Type a message...": "Wpisz wiadomość...",
  "Send": "Wyślij",
  "No messages yet": "Brak wiadomości",
  "Start a conversation": "Rozpocznij rozmowę",
  
  // dates
  "Today": "Dzisiaj",
  "Yesterday": "Wczoraj",
  "No date": "Brak daty",
  "Just now": "Właśnie",
  "minutes ago": "minut temu",
  "hours ago": "godzin temu",
  "days ago": "dni temu",
  
  // admin
  "Admin Panel": "Panel administracyjny",
  "Users": "Użytkownicy",
  "Reports": "Raporty",
  "Block User": "Zablokuj użytkownika",
  "Unblock User": "Odblokuj użytkownika",
  "User Management": "Zarządzanie użytkownikami",
  
  // misc
  "Welcome": "Witaj",
  "Welcome to DevHunt": "Witaj w DevHunt",
  "Get started": "Rozpocznij",
  "Learn more": "Dowiedz się więcej",
  "Back to projects": "Powrót do projektów",
  "Loading project...": "Ładowanie projektu...",
  "Are you sure?": "Czy jesteś pewien?",
  "This action cannot be undone.": "Tej akcji nie można cofnąć.",
  "Yes": "Tak",
  "No": "Nie",
  "OK": "OK",
  "Save Changes": "Zapisz zmiany",
  "Discard Changes": "Odrzuć zmiany",
  "Unsaved changes": "Niezapisane zmiany",
  "You have unsaved changes. Do you want to leave without saving?": "Masz niezapisane zmiany. Czy chcesz wyjść bez zapisywania?",
};

/**
 * Recursively translate values in a JSON object.
 */
function translateObj(obj) {
  if (typeof obj === 'string') {
    // Check exact match first
    if (translations[obj]) return translations[obj];
    
    // Check for format strings with {variables}
    for (const [en, pl] of Object.entries(translations)) {
      if (obj === en) return pl;
    }
    
    return obj; // Keep English if no translation found
  }
  
  if (Array.isArray(obj)) {
    return obj.map(item => translateObj(item));
  }
  
  if (typeof obj === 'object' && obj !== null) {
    const result = {};
    for (const [key, value] of Object.entries(obj)) {
      result[key] = translateObj(value);
    }
    return result;
  }
  
  return obj;
}

// Apply translations
const translated = translateObj(pl);

// Fix specific keys that need special handling
// Set locale to "pl"
if (translated.common) {
  translated.common.locale = "pl";
}

// Fix timezone placeholder
if (translated.placeholders) {
  translated.placeholders.timezonePlaceholder = "np. Europe/Warsaw, America/New_York";
}

writeFileSync(
  join(messagesDir, 'pl.json'),
  JSON.stringify(translated, null, 2) + '\n',
  'utf-8'
);

console.log('Successfully created pl.json');
