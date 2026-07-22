# DotNetCommander — актуальний стан проєкту

> Цей файл — єдине джерело правди про **поточний** стан коду для агентів.
> Плани на майбутнє й етапи розвитку — виключно в `ROADMAP.md`, тут їх не дублюємо.

## Опис проєкту
**DotNetCommander** — двопанельний файловий менеджер у стилі Total Commander / Norton Commander / Double Commander на Windows Forms.
Namespace: `DotNetCommander`

Поточна активна збірка:
- `Commander.NET.csproj` — `net8.0-windows10.0.22621.0`
- Версія: `1.6.0`

Історична збірка:
- `DotNetCommander.csproj` — оригінальний .NET Framework-проєкт

## Точка входу
- `Program.cs` — `Main()` запускає `AppForm`

## Основні форми та вікна
- `Application/AppForm.cs` — головне вікно, меню (включно з `Help` → `README.md`, `CHANGE.md`, `ROADMAP.md`, `About`), панель дисків, дві файлові панелі, нижня смуга F-кнопок
- `Dialogs/Operations/FormCopy.cs` — асинхронний діалог копіювання/переміщення з прогресом і скасуванням
- `Dialogs/Operations/FormDelete.cs` — асинхронний діалог видалення з прогресом і скасуванням
- `Dialogs/Operations/FormFileConflict.cs` — діалог конфлікту файлів для `copy/move`
- `Dialogs/Operations/FormArchiveOperation.cs` — асинхронний діалог створення й розпакування архівів з прогресом і скасуванням
- `Dialogs/Common/FormSettings.cs` — commander-style `Options` з категоріями `View`, `Editor`, `Rich Text`, `Operations`, `Performance`
- `Dialogs/Common/FormAbout.cs` — `About` з версією, описом і шляхом до `user.config`
- `Dialogs/Common/FormNewFile.cs` — commander-style створення нового файла з вибором редактора
- `Dialogs/Common/FormNewFolder.cs` — commander-style створення каталогів і підкаталогів
- `Viewers/Images/ImageView.cs` — перегляд зображень і EXIF; EXIF-залежності лежать у цій самій гілці
- `Editors/Text/TextEdit.cs` — редактор текстових і Markdown-файлів
- `Editors/RichText/RtfEdit.cs` — редактор RTF
- `Viewers/Csv/CsvView.cs` — окремий переглядач CSV-подібних файлів разом із CSV loader
- `Viewers/Compare/FileCompareForm.cs` — side-by-side порівняння (`Shift+F3`): text diff, CSV diff, image compare, binary fallback
- `Dialogs/Legacy/` — legacy допоміжні діалоги (`InputBox`, `frmOptions`, `frmWait`) зі збереженим старим API, але вже зі спільною типографікою та візуальним стилем актуальних службових форм

## Ключові компоненти
- `Browsers/FileSystem/FileBrowser.cs` — основна файлова панель на `ListView`
- `Browsers/BrowserPanelBase.cs` — базовий контракт для фізичних і майбутніх віртуальних панелей: location, items, selection, capabilities, navigation, refresh
- `Browsers/Archives/` — read-only archive panel, читання entries і матеріалізація окремих файлів для Quick View / `F3`
- `Browsers/Gedcom/` — GEDCOM-панель, каталог, Quick View родинного графа та векторні піктограми статі
- `Controls/Preview/` — загальний Quick View та інтерактивний перегляд зображень
- `Controls/Navigation/` — breadcrumb-навігація та її сегментні кнопки
- `Services/Operations/FileOperationService.cs` — планування та виконання `copy/move/delete`
- `Services/Archives/ArchiveService.cs` — виконання створення й безпечного розпакування архівів на стандартних API .NET 8; визначення типу делеговано `FileTypeClassifier`
- `Application/CommandService.cs` — виконання основних commander-команд і відкриття внутрішніх viewer/editor форм
- `Services/Files/` — доступ до файлової системи, класифікація типів і process-wide кеш shell-іконок
- `Services/UI/DialogStyleService.cs` — типографіка та спільний стиль службових діалогів
- `Infrastructure/Platform/Windows/WinContextMenu.cs` — shell-дії `Open`, `Open with...`, `Properties`, запуск у persistent console
- `Infrastructure/Settings/SettingsStorage.cs` — допоміжний доступ до шляху `user.config`
- `Infrastructure/Diagnostics/PerfTrace.cs` — вимкнена за замовчуванням допоміжна трасировка продуктивності
- `LogService` — централізоване логування (застосовується поступово в критичних сценаріях)

## Поточні можливості
- двопанельна навігація по файловій системі з адресним рядком і панеллю дисків;
- `Quick View` для тексту, зображень і невеликих CSV-подібних файлів;
- `Quick View` у GEDCOM-панелі показує інтерактивний родинний граф вибраної особи з pan/zoom;
- вбудоване порівняння файлів (`Shift+F3`): text diff, CSV diff, image compare, binary fallback;
- окремі вікна для `ImageView`, `TextEdit`, `RtfEdit`, `CsvView`;
- `TextEdit` і `RtfEdit` мають `F1`-help, `Save As...`, status bar і окремі налаштування editor/viewer UX;
- drag-and-drop з `Explorer`, інших файлових менеджерів і між панелями;
- конфлікт-орієнтовані `copy/move` з `overwrite`, `skip`, `rename` і масовими рішеннями (`apply to all`);
- створення архівів через `Alt+F5` і розпакування через `Alt+F9`; підтримуються `ZIP`, `TAR`, `TAR.GZ` / `TGZ`, path traversal блокується;
- вхід у підтримуваний архів подвійним кліком або `Enter` як у каталог; відкритий архів тимчасово відображається як логічний пристрій у drive toolbar і зникає після виходу;
- `Backspace` відновлює попередній список поточної панелі навіть між різними фізичними/архівними пристроями; `Alt+Up` завжди переходить лише до батьківського каталогу;
- `Ctrl+PgDn` відкриває `.ged` як GEDCOM-панель або перевіряє сигнатуру вибраного файла й відкриває ZIP/TAR/TAR.GZ як каталог незалежно від розширення;
- Quick View і `F3` матеріалізують лише вибраний файл архіву в тимчасовий session-каталог; `F5` копіює вибрані записи до пасивної файлової панелі;
- сортування за колонками у `FileBrowser`;
- перемикання режимів перегляду: `Details`, `List`, `Small Icons`, `Large Icons`, `Tiles`;
- shell-дії для файлів: `Open`, `Open with...`, `Properties` (через `WinContextMenu`);
- `Shift+Enter` для запуску `.bat/.cmd/.exe/.com/.ps1` у консолі, що не закривається;
- `Ctrl+R` для ручного refresh активної панелі, плюс опційне автооновлення каталогу при зовнішніх змінах;
- `Ctrl+N` для створення нового файла через системний `SaveFileDialog`;
- `Shift+F4`, `Shift+F5`, `Shift+F6`, `F7` для commander-style створення файла, копії з новим іменем, перейменування та створення каталогу;
- жива нижня F-панель з модифікованими підписами для `Shift` / `Alt`;
- меню `Help` з `About` і переглядом `README.md`, `CHANGE.md`, `ROADMAP.md`;
- збереження геометрії головного вікна з відновленням maximized-стану і перевіркою монітора;
- налаштування шрифтів, ширин колонок, CSV preview, directory watching, мови інтерфейсу, окремо `RtfEdit` / Markdown preview і типографіки службових діалогів;
- запам'ятовування останнього шляху для кожного диска окремо по кожній панелі;
- локалізація `EN`, `DE`, `UK`;
- Windows-специфіка: іконки, `.lnk`, список дисків, безпечніший shell API без `x86-only` interop-залежності.

## Архітектурні орієнтири
- UI-логіка поки що зосереджена переважно в `AppForm` і `FileBrowser`.
- Файлові операції та частина команд уже винесені в `FileOperationService`, `CommandService`, `FileSystemService`.
- Локалізація централізована через `Language.cs` і ресурси `Resources/Language*.resx`.
- `OS.cs` лишається фасадом між Windows- і Unix-специфічною логікою, хоча практичний фокус проєкту зараз — Windows.
- `Dialogs/Common/` і `Dialogs/Operations/` — актуальні службові діалоги; `Dialogs/Legacy/` — сумісний legacy-шар, візуально вирівняний через `DialogStyleService`.
- `FileBrowser`, `ArchiveBrowser` і `GedcomBrowser` наслідують `BrowserPanelBase`; `FileBrowser` лишається сумісним host-контролом для `AppForm` і перемикає фізичний/віртуальні режими без заміни панелі у layout.

## Структура проєкту
```text
DotNetCMD/
├── Program.cs
├── Application/
│   ├── AppForm.cs / .Designer.cs / .resx
│   └── CommandService.cs
├── Browsers/
│   ├── BrowserPanelBase.cs
│   ├── FileSystem/       # FileBrowser + designer/resources
│   ├── Archives/         # ArchiveBrowser + catalog
│   └── Gedcom/           # browser, catalog, graph, icons
├── Controls/
│   ├── Navigation/       # AddressBar controls
│   └── Preview/          # Quick View controls
├── Dialogs/
│   ├── Common/           # About, Settings, New File/Folder
│   ├── Operations/       # Copy, Delete, Conflict, Archive
│   └── Legacy/           # InputBox, frmOptions, frmWait
├── Editors/
│   ├── Text/
│   └── RichText/
├── Viewers/
│   ├── Csv/              # viewer + loader
│   ├── Compare/
│   └── Images/           # ImageView + EXIF dependencies
├── Services/
│   ├── Archives/
│   ├── Files/
│   ├── Operations/
│   └── UI/
├── Infrastructure/
│   ├── Diagnostics/
│   ├── Localization/
│   ├── Platform/
│   └── Settings/
├── Resources/
│   ├── Language.resx
│   ├── Language.de-DE.resx
│   ├── Language.uk.resx
│   └── icon.ico / icon512.ico
└── Properties/
    ├── Settings.settings
    ├── Settings.Designer.cs
    ├── Resources.resx
    └── Resources.Designer.cs
```

## Збірка
- Базова команда: `dotnet build Commander.NET.csproj`
- Платформа: Windows / WinForms
- Активна збірка працює в `AnyCPU/x64` режимі без обов'язкової `x86-only` shell-залежності

## Відомі поточні обмеження

- не закриті всі підсумки операцій, retry/recovery і partial failure сценарії для файлових операцій;
- немає long-path hardening;
- немає автоматичних тестів;
- немає CI;
- drag-and-drop shell integration ще не доведений до повного Explorer-like рівня;
- автооновлення каталогу ще не перевірене на edge-case shell-сценаріях і масових серіях подій;
- UI-консистентність між viewer/editor формами ще не доведена до спільного стандарту help/hotkeys/menus;
- `Dialogs/Legacy/` зберігає legacy API і структуру, хоча його візуальний стиль уже узгоджено з актуальними діалогами.
- архівна панель поки read-only: доступні навігація, preview/open і copy-out через `F5`; rename/delete/create/paste всередині архіву не підтримуються, reparse points під час пакування навмисно пропускаються.

## Практичні поради для агентів
- Якщо змінюєш текст інтерфейсу, перевіряй `Resources/Language.resx` і принаймні fallback в `EN`.
- Якщо змінюєш гарячі клавіші або поведінку панелі, перевіряй `AppForm` і `FileBrowser` разом.
- Якщо змінюєш файлові операції, не розмазуй логіку назад у форми — краще розвивати `FileOperationService`.
- Якщо змінюєш архівні формати або сигнатури, роби це у `FileTypeClassifier`; виконання pack/unpack і перевірки extraction paths лишаються в `ArchiveService`, захист від виходу за каталог призначення не послаблюй.
- Нові типи панелей будуй від `BrowserPanelBase` і описуй дозволені операції через `BrowserPanelCapabilities`, а не через перевірки конкретного UI-класу.
- Якщо змінюєш shell-інтеграцію, тестуй окремо файли, папки, `.lnk`, `.bat/.cmd` і сценарії без файлової асоціації.
- Для форм типу `Viewer` і `Editor` бажано підтримувати `F1` як коротку довідку про доступні дії та їхні гарячі клавіші саме в цій формі.
- Зараз проєкт переважно відповідає цим орієнтирам, але в коді ще залишилися структурні legacy-острівці: API класів у `Dialogs/Legacy/`, старий EXIF-код у `Viewers/Images/` і частина UI-рядків без повної локалізації.
- Плани, етапи й майбутні фічі — дивись `ROADMAP.md`, а не сюди; цей файл описує лише те, що вже реально є в коді.
