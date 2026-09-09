# DotNetCommander — актуальний стан проєкту

> Цей файл — єдине джерело правди про **поточний** стан коду для агентів.
> Плани на майбутнє й етапи розвитку — виключно в `ROADMAP.md`, тут їх не дублюємо.

## Опис проєкту
**DotNetCommander** — двопанельний файловий менеджер у стилі Total Commander / Norton Commander / Double Commander на Windows Forms.
Namespace: `DotNetCommander`

Поточна активна збірка:
- `Commander.NET.csproj` — `net8.0-windows10.0.22621.0`
- Версія: `1.8.2`

Історична збірка:
- `DotNetCommander.csproj` — оригінальний .NET Framework-проєкт

## Точка входу
- `Program.cs` — `Main()` запускає `AppForm`

## Основні форми та вікна
- `Application/AppForm.cs` — головне вікно, меню (включно з `Help` → `README.md`, `CHANGE.md`, `ROADMAP.md`, `About`), панель дисків, дві файлові панелі, командний рядок та нижня смуга F-кнопок
- `Dialogs/Operations/FormCopy.cs` — асинхронний діалог копіювання/переміщення з прогресом і скасуванням
- `Dialogs/Operations/FormDelete.cs` — асинхронний діалог видалення з прогресом і скасуванням
- `Dialogs/Operations/FormFileConflict.cs` — діалог конфлікту файлів для `copy/move`
- `Dialogs/Operations/FormArchiveOperation.cs` — асинхронний діалог створення й розпакування архівів з прогресом і скасуванням
- `Dialogs/Common/FormSettings.cs` — commander-style `Options` з категоріями `View`, `Editor`, `Rich Text`, `Operations`, `Performance`
- `Dialogs/Common/FormAbout.cs` — `About` з версією, описом і шляхом до `user.config`
- `Dialogs/Common/FormKeyboardHelp.cs` — спільне commander-style вікно клавіатурної довідки з локалізованою таблицею shortcut/action
- `Dialogs/Common/FormNewFile.cs` — commander-style створення нового файла з вибором редактора
- `Dialogs/Common/FormNewFolder.cs` — commander-style створення каталогів і підкаталогів
- `Viewers/Images/ImageView.cs` — інтерактивний перегляд зображень із zoom/pan/status bar та EXIF; EXIF-залежності лежать у цій самій гілці
- `Editors/Text/TextEdit.cs` — редактор текстових і Markdown-файлів
- `Editors/RichText/RtfEdit.cs` — редактор RTF і фоновий Markdown-to-RTF renderer з таблицями, списками, цитатами, links, локальними images та вкладеним inline-форматуванням
- `Viewers/Csv/CsvView.cs` — окремий переглядач CSV-подібних файлів разом із CSV loader
- `Viewers/Compare/FileCompareForm.cs` — side-by-side порівняння (`Shift+F3`): text diff, CSV diff, image compare, binary fallback
- `Viewers/Raw/RawFileView.cs` — потоковий RAW/Hex viewer для довільних файлів із block navigation, hexadecimal offsets та ASCII column
- `Dialogs/Legacy/` — legacy допоміжні діалоги (`InputBox`, `frmOptions`, `frmWait`) зі збереженим старим API, але вже зі спільною типографікою та візуальним стилем актуальних службових форм

## Ключові компоненти
- `Browsers/FileSystem/FileBrowser.cs` — основна файлова панель на `ListView`
- `Browsers/BrowserPanelBase.cs` — базовий контракт для фізичних і майбутніх віртуальних панелей: location, items, selection, capabilities, navigation, refresh
- `Browsers/Archives/` — read-only archive panel, читання entries і матеріалізація окремих файлів для Quick View / `F3`
- `Browsers/Gedcom/` — ієрархічна GEDCOM-панель для всіх level-0 tags (`INDI`, `FAM`, `NOTE` та інші), типізовані каталоги персон/сімей, generic records, Quick View родинного графа та векторні піктограми статі
- `Browsers/DataSets/` — read-only DataSet-панель для `.dsx`: таблиці, рядки, schema relations і безпечне XML-читання зі strict-first fallback для невалідних XML 1.0 символів
- `Browsers/Search/` — панель результатів асинхронного пошуку за маскою/regex та вмістом із прогресом і скасуванням
- `Browsers/Compound/` — read-only CFBF/OLE2 provider: storages, streams, класифікація документа та безпечна матеріалізація окремих потоків
- `ThirdParty/OpenMcdf/` — локальний source snapshot OpenMcdf, який компілюється безпосередньо в `DotNetCommander.dll`; upstream commit зафіксований, MPL-2.0 збережено, окрема DLL і NuGet не використовуються
- `Controls/Preview/` — загальний Quick View та інтерактивний перегляд зображень
- `Controls/Navigation/` — breadcrumb-навігація та її сегментні кнопки
- `Services/Operations/FileOperationService.cs` — планування та виконання `copy/move/delete`
- `Services/Archives/ArchiveService.cs` — виконання створення й безпечного розпакування архівів на стандартних API .NET 8; визначення типу делеговано `FileTypeClassifier`
- `Application/CommandService.cs` — виконання основних commander-команд і відкриття внутрішніх viewer/editor форм
- `Services/Files/` — доступ до файлової системи, класифікація типів, XML-aware визначення кодування тексту та process-wide кеш shell-іконок
- `Services/UI/DialogStyleService.cs` — типографіка та спільний стиль службових діалогів
- `Infrastructure/Platform/Windows/WinContextMenu.cs` — shell-дії `Open`, `Open with...`, `Properties`, запуск у persistent console
- `Infrastructure/Platform/Windows/WinCommandLine.cs` — виконання введених команд через Windows command processor у каталозі активної панелі
- `Infrastructure/Settings/SettingsStorage.cs` — допоміжний доступ до шляху `user.config`
- `Infrastructure/Diagnostics/PerfTrace.cs` — вимкнена за замовчуванням допоміжна трасировка продуктивності
- `LogService` — централізоване логування (застосовується поступово в критичних сценаріях)

## Поточні можливості
- двопанельна навігація по файловій системі з адресним рядком і панеллю дисків;
- `Alt+F7` запускає фоновий пошук у каталозі за маскою/regex та текстом; результати відображаються як `SearchBrowser` і підтримують preview, copy/move/delete;
- Файлова, GEDCOM та DataSet-панелі використовують `AddressBar` із компактними breadcrumb-сегментами та mouse-friendly кнопками `Back`, `Up` і `Refresh`, що викликають ті самі команди, що `Backspace`, `Ctrl+PgUp` і `Ctrl+R`;
- commander-style командний рядок під панелями: запуск програм і команд із параметрами в каталозі активної панелі, історія через `Up/Down`, фокус через `Ctrl+L`, `Ctrl+Enter` вставляє назву поточного елемента, `Shift+Enter` залишає консоль відкритою;
- `Quick View` для тексту, зображень і невеликих CSV-подібних файлів;
- Quick View асинхронно показує text, formatted RTF/Markdown, image, CSV, GEDCOM graph і Word Binary plain text; text/RTF fonts та size limits беруться з `Options`, для plain text видно визначене кодування;
- Quick View і `ImageView` по `F3` спільно використовують `InteractiveImageViewControl`: zoom-at-cursor колесом, `+`/`-`, pan, reset-to-fit; масштаб і розміри показуються в status рядку без накладки поверх зображення;
- для image Quick View і `ImageView` status рядок доповнюється описом із `descript.ion` у каталозі зображення; parser підтримує quoted filenames, UTF-8/BOM та системне ANSI-кодування;
- `Quick View` у GEDCOM-панелі показує інтерактивний родинний граф вибраної особи з pan/zoom;
- GEDCOM-панель відкривається кореневим каталогом типів: `Persons (INDI)`, `Families (FAM)`, `Notes (NOTE)` і динамічними розділами для решти тегів нульового рівня;
- вбудоване порівняння файлів (`Shift+F3`): text diff, CSV diff, image compare, binary fallback;
- `F3` відкриває binary/unknown/archive файли у потоковому RAW/Hex viewer, але XML declaration `<?xml` у binary/unknown файла переводить їх у текстовий preview; `Ctrl+F3` примусово показує RAW для будь-якого вибраного файла незалежно від розширення;
- `F3` для Word Binary `.doc`/CFBF витягує main story як plain text через `FIB`, `0Table`/`1Table` і `CLX/Piece Table`; те саме працює для потоку `WordDocument` усередині `CompoundBrowser` та в Quick View, а `Ctrl+F3` лишається RAW;
- текстовий viewer, Quick View і text compare враховують BOM та `encoding="..."` з XML declaration (зокрема `windows-1251` у `.fb2`), а `TextEdit` зберігає виявлене кодування;
- великі RTF, text та image файли не блокуються загальним preview-size limit; ліміт `9 MiB` застосовується лише до Markdown-to-RTF render, після нього `F3` переходить у RAW;
- окремі вікна для `ImageView`, `TextEdit`, `RtfEdit`, `CsvView`;
- `TextEdit`, `RtfEdit`, `ImageView` та `CsvView` мають `F1`-help; `RtfEdit`, `ImageView` і `CsvView` використовують структуроване commander-style вікно довідки замість `MessageBox`;
- `RtfEdit` локалізовано для `EN/UK/DE`; plain text і Markdown зберігають визначене кодування, а Markdown preview підтримує fenced code, списки, цитати, links, локальні images, emoji та вкладені styles;
- drag-and-drop з `Explorer`, інших файлових менеджерів і між панелями;
- конфлікт-орієнтовані `copy/move` з `overwrite`, `skip`, `rename` і масовими рішеннями (`apply to all`);
- стійкі пакетні `copy/move/delete`: помилка окремого елемента підтримує retry/skip/cancel, завершення показує структурований підсумок, а операції виконуються через неблокуючу послідовну чергу;
- створення архівів через `Alt+F5` і розпакування через `Alt+F9`; підтримуються `ZIP`, `TAR`, `TAR.GZ` / `TGZ`, path traversal блокується;
- вхід у підтримуваний архів подвійним кліком або `Enter` як у каталог; відкритий архів тимчасово відображається як логічний пристрій у drive toolbar і зникає після виходу;
- `Backspace` відновлює попередній список поточної панелі навіть між різними фізичними/архівними пристроями; `Ctrl+PgUp` завжди переходить лише до батьківського каталогу;
- `Ctrl+PgDn` відкриває `.ged` як GEDCOM-панель, `.dsx` як DataSet-панель, перевіряє archive signature, а потім тихо пробує прочитати інші файли як DataSet XML незалежно від розширення;
- `Ctrl+PgDn` також розпізнає CFBF/OLE2 signature незалежно від розширення та відкриває `CompoundBrowser`; звичайний `Enter` для Office-документів лишається системним відкриттям;
- DataSet-панель показує каталоги `Tables` і `Relations`, довільні колонки/рядки таблиць та parent/child columns зв’язків; режим read-only, читання виконується у фоні, а після секундної затримки показується неблокувальний `Wait`;
- Quick View і `F3` матеріалізують лише вибраний файл архіву в тимчасовий session-каталог; `F5` копіює вибрані записи до пасивної файлової панелі;
- сортування за колонками у `FileBrowser`: `..` завжди перший, а опційне `Options -> View` групування окремо сортує каталоги перед файлами;
- перемикання режимів перегляду: `Details`, `List`, `Small Icons`, `Large Icons`, `Tiles`;
- shell-дії для файлів: `Open`, `Open with...`, `Properties` (через `WinContextMenu`);
- `Shift+Enter` для запуску `.bat/.cmd/.exe/.com/.ps1` у консолі, що не закривається;
- `Ctrl+R` для ручного refresh активної панелі, плюс опційне автооновлення каталогу при зовнішніх змінах;
- `Ctrl+N` для створення нового файла через системний `SaveFileDialog`;
- `Shift+F4`, `Shift+F5`, `Shift+F6`, `F7` для commander-style створення файла, копії з новим іменем, перейменування та створення каталогу; після `F7` новий каталог типово відкривається, це можна змінити в `Options`;
- жива нижня F-панель показує `Shift` / `Alt` / `Ctrl` в окремій крайній комірці, залишає непідтримувані модифіковані кнопки порожніми й відображає `Ctrl+F3` RAW/Hex;
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
- `FileBrowser`, `ArchiveBrowser`, `GedcomBrowser`, `DataSetBrowser`, `SearchBrowser` і `CompoundBrowser` наслідують `BrowserPanelBase`; `FileBrowser` лишається сумісним host-контролом для `AppForm` і перемикає фізичний/віртуальні режими без заміни панелі у layout.

## Спільний контракт viewer-форм
- Viewer-форми мають `KeyPreview = true` і обробляють глобальні клавіші через `ProcessCmdKey`, щоб shortcut працював незалежно від дочірнього control у фокусі.
- `Esc` завжди закриває viewer. Якщо триває фонова операція, закриття спочатку сигналізує її cancellation; завершення async-коду не повинно оновлювати вже закриті або disposed controls.
- `F1` відкриває спільний локалізований `FormKeyboardHelp` із фактичним переліком команд саме цієї форми; довгий неструктурований `MessageBox` для нових або оновлених viewer-форм не використовуємо.
- Повторне читання поточного файла, якщо воно підтримується viewer-ом, доступне через `F5`; `Ctrl+R` може бути рівнозначним shortcut для узгодженості з файловими панелями. Toolbar-команда і shortcut мають викликати один метод.
- Viewer показує поточний файл/режим і корисний підсумок у title, toolbar або status bar; тривалі операції показують progress і дають явне скасування.
- Геометрія окремого viewer-вікна відновлюється між відкриттями; координати та розмір запам'ятовуються лише у `FormWindowState.Normal`, щоб не зберігати maximized/minimized bounds як звичайний розмір.
- Видимі підписи, help-тексти, status/error повідомлення додаються до `Resources/Language.resx`, `Language.uk.resx` і `Language.de-DE.resx`; винятки проходять через `LogService` перед показом локалізованої помилки.
- Специфічні можливості залишаються локальними для viewer-а: zoom/pan для images, block navigation для RAW, table selection/copy для CSV тощо. Спільний контракт не повинен стирати корисну спеціалізацію.

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
│   ├── Gedcom/           # browser, catalog, graph, icons
│   └── DataSets/         # DataSet XML loader, tables and relations browser
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
│   ├── Raw/              # streaming RAW/Hex viewer
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
