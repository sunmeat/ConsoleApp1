using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace FileSearcher
{
    /// <summary>
    /// Базовий абстрактний клас для всіх пошукових агентів, що виконують безпечне рекурсивне сканування файлової системи.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Клас інкапсулює алгоритм обходу директорій без ризику переривання через <see cref="UnauthorizedAccessException"/>
    /// або зациклення на символічних посиланнях (<see cref="FileAttributes.ReparsePoint"/>).
    /// </para>
    /// <para>
    /// Для створення власного агента успадкуйте цей клас і реалізуйте абстрактний метод <see cref="GetRoleTitle"/>.
    /// </para>
    /// <list type="table">
    /// <listheader>
    /// <term>Модифікатор доступу</term>
    /// <description>Призначення в базовому класі</description>
    /// </listheader>
    /// <item>
    /// <term><see langword="public"/></term>
    /// <description>Публічний контракт для запуску пошуку та читання результатів.</description>
    /// </item>
    /// <item>
    /// <term><see langword="protected"/></term>
    /// <description>Точки розширення та налаштування для похідних класів.</description>
    /// </item>
    /// <item>
    /// <term><see langword="private"/></term>
    /// <description>Внутрішня логіка безпечної ітерації директорій та форматування підсумків.</description>
    /// </item>
    /// </list>
    /// </remarks>
    /// <seealso cref="CatScanner"/>
    /// <seealso cref="DogTracker"/>
    /// <seealso cref="OwlWatcher"/>
    /// <seealso href="https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/recommended-tags">Документація XML-тегів C#</seealso>
    public abstract class SearchAgent
    {
        /// <summary>
        /// Внутрішній лічильник знайдених файлів за поточну сесію пошуку.
        /// </summary>
        private int _foundCount;

        /// <summary>
        /// Отримує позивний (ім'я) пошукового агента.
        /// </summary>
        /// <value>Рядок з іменем агента, заданий під час ініціалізації.</value>
        public string Name { get; }

        /// <summary>
        /// Отримує або задає маску пошуку файлів (наприклад, <c>*.dll</c> або <c>*.so</c>).
        /// </summary>
        /// <value>Шаблон пошуку підтримуваний методом <see cref="Directory.EnumerateFiles(string, string, EnumerationOptions)"/>.</value>
        public string FileMask { get; protected set; }

        /// <summary>
        /// Отримує загальну кількість файлів, знайдених агентом під час останнього запуску <see cref="ExecuteSearch(string)"/>.
        /// </summary>
        /// <value>Ціле невід'ємне число знайдених збігів.</value>
        public int FoundCount => _foundCount;

        /// <summary>
        /// Отримує або задає максимальну кількість шляхів до файлів, що виводяться в консоль для попереднього перегляду.
        /// </summary>
        /// <value>Значення за замовчуванням становить <c>5</c>.</value>
        protected int MaxPreviewItems { get; set; } = 5;

        /// <summary>
        /// Ініціалізує новий екземпляр класу <see cref="SearchAgent"/> із заданим іменем та маскою файлів.
        /// </summary>
        /// <param name="name">Ім'я пошукового агента.</param>
        /// <param name="fileMask">Маска пошуку файлів (наприклад, <c>*.dll</c>).</param>
        /// <exception cref="ArgumentException">Виникає, якщо <paramref name="name"/> або <paramref name="fileMask"/> є порожніми або складаються лише з пробілів.</exception>
        protected SearchAgent(string name, string fileMask)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Ім'я агента не може бути порожнім.", nameof(name));
            if (string.IsNullOrWhiteSpace(fileMask))
                throw new ArgumentException("Маска файлів не може бути порожньою.", nameof(fileMask));

            Name = name;
            FileMask = fileMask;
        }

        /// <summary>
        /// Запускає процес рекурсивного пошуку файлів, починаючи з вказаної кореневої директорії.
        /// </summary>
        /// <param name="rootDirectory">Повний шлях до початкової директорії сканування.</param>
        /// <remarks>
        /// Якщо директорія <paramref name="rootDirectory"/> не існує, метод виводить попередження в консоль
        /// і завершує роботу без генерації винятку, залишаючи <see cref="FoundCount"/> рівним <c>0</c>.
        /// </remarks>
        /// <example>
        /// Приклад запуску пошуку для конкретного агента:
        /// <code>
        /// SearchAgent agent = new CatScanner("Локі");
        /// agent.ExecuteSearch(@"C:\Windows\System32");
        /// Console.WriteLine($"Всього знайдено: {agent.FoundCount}");
        /// </code>
        /// </example>
        public void ExecuteSearch(string rootDirectory)
        {
            _foundCount = 0;
            Console.WriteLine($"\n[{GetRoleTitle()}] {Name} починає пошук за маскою '{FileMask}' у {rootDirectory}...");

            if (!Directory.Exists(rootDirectory))
            {
                Console.WriteLine($"  -> Директорія не існує: {rootDirectory}");
                return;
            }

            foreach (var file in SafeEnumerateFiles(rootDirectory, FileMask))
            {
                _foundCount++;
                OnFileFound(file, _foundCount);
            }

            PrintSummary();
        }

        /// <summary>
        /// Обробляє кожен знайдений файл під час сканування.
        /// </summary>
        /// <param name="fullPath">Повний шлях до знайденого файлу.</param>
        /// <param name="currentIndex">Порядковий номер знайденого файлу (починаючи з <c>1</c>).</param>
        /// <remarks>
        /// Перевизначте цей метод у похідному класі, щоб змінити формат виведення або додати власну реакцію на знахідку.
        /// </remarks>
        protected virtual void OnFileFound(string fullPath, int currentIndex)
        {
            if (currentIndex <= MaxPreviewItems)
            {
                Console.WriteLine($"  {currentIndex}. {Path.GetFileName(fullPath)} ({fullPath})");
            }
        }

        /// <summary>
        /// Повертає текстовий опис ролі та спеціалізації поточного агента.
        /// </summary>
        /// <returns>Рядок із назвою ролі для відображення в логах консолі.</returns>
        protected abstract string GetRoleTitle();

        /// <summary>
        /// Безпечно перелічує всі файли в дереві каталогів без переривання роботи при помилках доступу.
        /// </summary>
        /// <param name="root">Кореневий каталог пошуку.</param>
        /// <param name="searchPattern">Шаблон імені файлу для фільтрації.</param>
        /// <returns>Послідовність <see cref="IEnumerable{T}"/> повних шляхів до знайдених файлів.</returns>
        private static IEnumerable<string> SafeEnumerateFiles(string root, string searchPattern)
        {
            var dirs = new Stack<string>();
            dirs.Push(root);

            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            while (dirs.Count > 0)
            {
                string currentDir = dirs.Pop();

                IEnumerable<string> subDirs;
                try
                {
                    subDirs = Directory.EnumerateDirectories(currentDir, "*", options);
                }
                catch
                {
                    continue;
                }

                foreach (var dir in subDirs)
                {
                    dirs.Push(dir);
                }

                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(currentDir, searchPattern, options);
                }
                catch
                {
                    continue;
                }

                foreach (var file in files)
                {
                    yield return file;
                }
            }
        }

        /// <summary>
        /// Виводить у консоль підсумкову статистику роботи поточного агента.
        /// </summary>
        private void PrintSummary()
        {
            if (_foundCount > MaxPreviewItems)
            {
                Console.WriteLine($"  ... та ще {_foundCount - MaxPreviewItems} файлів.");
            }
            Console.WriteLine($"  Підсумок ({Name}): знайдено {_foundCount} файлів.");
        }
    }

    /// <summary>
    /// Спеціалізований агент «Котик-нишпорка» для пошуку динамічних бібліотек поточної операційної системи.
    /// </summary>
    /// <remarks>
    /// Автоматично визначає розширення бібліотек залежно від платформи за допомогою <see cref="RuntimeInformation.IsOSPlatform(OSPlatform)"/>:
    /// <list type="bullet">
    /// <item><description><c>*.dll</c> — для Windows;</description></item>
    /// <item><description><c>*.so</c> — для Linux;</description></item>
    /// <item><description><c>*.dylib</c> — для macOS.</description></item>
    /// </list>
    /// </remarks>
    public class CatScanner : SearchAgent
    {
        /// <summary>
        /// Отримує рівень скритності котика за шкалою від <c>1</c> до <c>10</c>.
        /// </summary>
        /// <value>Ціле число, що характеризує безшумність сканування.</value>
        public int StealthLevel { get; private set; }

        /// <summary>
        /// Ініціалізує новий екземпляр класу <see cref="CatScanner"/>.
        /// </summary>
        /// <param name="name">Кличка котика.</param>
        /// <param name="stealthLevel">Рівень скритності (від <c>1</c> до <c>10</c>). За замовчуванням <c>10</c>.</param>
        /// <exception cref="ArgumentOutOfRangeException">Виникає, якщо <paramref name="stealthLevel"/> виходить за межі діапазону <c>1..10</c>.</exception>
        public CatScanner(string name, int stealthLevel = 10)
            : base(name, GetPlatformLibraryMask())
        {
            if (stealthLevel < 1 || stealthLevel > 10)
                throw new ArgumentOutOfRangeException(nameof(stealthLevel), "Рівень скритності має бути від 1 до 10.");

            StealthLevel = stealthLevel;
            MaxPreviewItems = 4;
        }

        /// <inheritdoc />
        protected override string GetRoleTitle() => $"Котик-нишпорка | Скритність: {StealthLevel}/10";

        /// <inheritdoc />
        /// <remarks>
        /// При виявленні найпершого файлу (<paramref name="currentIndex"/> дорівнює <c>1</c>) виводить характерне вітання кота.
        /// </remarks>
        protected override void OnFileFound(string fullPath, int currentIndex)
        {
            if (currentIndex == 1)
            {
                Console.WriteLine("  *Мяу!* Перша здобич знайдена:");
            }
            base.OnFileFound(fullPath, currentIndex);
        }

        /// <summary>
        /// Визначає маску динамічних бібліотек для поточної ОС.
        /// </summary>
        /// <returns>Рядок маски файлів (<c>*.dll</c>, <c>*.so</c>, <c>*.dylib</c> або <c>*.*</c>).</returns>
        private static string GetPlatformLibraryMask()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "*.dll";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "*.so";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "*.dylib";
            return "*.*";
        }
    }

    /// <summary>
    /// Спеціалізований агент «Пес-слідопит» для пошуку виконуваних файлів та командних скриптів.
    /// </summary>
    public class DogTracker : SearchAgent
    {
        /// <summary>
        /// Отримує породу собаки-слідопита.
        /// </summary>
        /// <value>Назва породи, вказана при створенні екземпляра.</value>
        public string Breed { get; }

        /// <summary>
        /// Отримує значення, що вказує, чи є пес хорошим хлопчиком.
        /// </summary>
        /// <value>Завжди повертає <see langword="true"/>.</value>
        public bool IsGoodBoy { get; private set; } = true;

        /// <summary>
        /// Ініціалізує новий екземпляр класу <see cref="DogTracker"/>.
        /// </summary>
        /// <param name="name">Кличка собаки.</param>
        /// <param name="breed">Порода собаки.</param>
        public DogTracker(string name, string breed)
            : base(name, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "*.exe" : "*.sh")
        {
            Breed = breed;
            MaxPreviewItems = 3;
        }

        /// <summary>
        /// Заохочує собаку смаколиком за успішно виконаний пошук.
        /// </summary>
        public void GiveTreat()
        {
            IsGoodBoy = true;
            Console.WriteLine($"  [Бонус] {Name} ({Breed}) отримав смаколик і радісно виляє хвостом!");
        }

        /// <inheritdoc />
        protected override string GetRoleTitle() => $"Пес-слідопит ({Breed})";

        /// <inheritdoc />
        protected override void OnFileFound(string fullPath, int currentIndex)
        {
            if (currentIndex <= MaxPreviewItems)
            {
                Console.WriteLine($"  [Гав! Слід #{currentIndex}] {fullPath}");
            }
        }
    }

    /// <summary>
    /// Внутрішній агент «Сова-аналітик», призначений для пошуку конфігураційних файлів (<c>*.ini</c> або <c>*.conf</c>).
    /// </summary>
    /// <remarks>
    /// Клас має модифікатор доступу <see langword="internal"/>, тому доступний лише в межах поточної збірки.
    /// </remarks>
    internal class OwlWatcher : SearchAgent
    {
        /// <summary>
        /// Прапорець активації режиму нічного зору.
        /// </summary>
        private readonly bool _nightVisionEnabled;

        /// <summary>
        /// Ініціалізує новий екземпляр класу <see cref="OwlWatcher"/>.
        /// </summary>
        /// <param name="name">Ім'я сови.</param>
        /// <param name="nightVisionEnabled">Якщо <see langword="true"/>, активується режим нічного зору.</param>
        public OwlWatcher(string name, bool nightVisionEnabled = true)
            : base(name, "*.ini")
        {
            _nightVisionEnabled = nightVisionEnabled;
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                FileMask = "*.conf";
            }
        }

        /// <inheritdoc />
        protected override string GetRoleTitle() =>
            _nightVisionEnabled ? "Сова-аналітик (Нічний зір)" : "Сова-аналітик";
    }

    /// <summary>
    /// Координатор пошукового загону, який об'єднує агентів різних типів та агрегує результати їхньої роботи.
    /// </summary>
    /// <example>
    /// Повний цикл створення та запуску загону:
    /// <code>
    /// var squad = new SearchSquad("Нічна варта");
    /// squad.AddAgent(new CatScanner("Мурчик"));
    /// squad.AddAgent(new DogTracker("Байкал", "Лабрадор"));
    /// squad.RunMission();
    /// </code>
    /// </example>
    public class SearchSquad
    {
        /// <summary>
        /// Колекція зареєстрованих агентів у загоні.
        /// </summary>
        private readonly List<SearchAgent> _agents = new();

        /// <summary>
        /// Системна директорія, визначена автоматично для поточної ОС.
        /// </summary>
        private readonly string _targetDirectory;

        /// <summary>
        /// Отримує офіційну назву пошукового загону.
        /// </summary>
        /// <value>Назва загону.</value>
        public string SquadName { get; }

        /// <summary>
        /// Отримує сумарну кількість файлів, знайдених усіма агентами загону.
        /// </summary>
        /// <value>Сума значень <see cref="SearchAgent.FoundCount"/> по всіх елементах колекції.</value>
        public int TotalFilesFound
        {
            get
            {
                int sum = 0;
                foreach (var agent in _agents)
                {
                    sum += agent.FoundCount;
                }
                return sum;
            }
        }

        /// <summary>
        /// Ініціалізує новий екземпляр класу <see cref="SearchSquad"/> та визначає цільову директорію ОС.
        /// </summary>
        /// <param name="squadName">Назва загону.</param>
        public SearchSquad(string squadName)
        {
            SquadName = squadName;
            _targetDirectory = ResolveDefaultDirectory();
        }

        /// <summary>
        /// Додає нового пошукового агента до складу загону.
        /// </summary>
        /// <typeparam name="TAgent">Тип агента, що успадковує <see cref="SearchAgent"/>.</typeparam>
        /// <param name="agent">Екземпляр пошукового агента.</param>
        /// <exception cref="ArgumentNullException">Виникає, якщо <paramref name="agent"/> дорівнює <see langword="null"/>.</exception>
        public void AddAgent<TAgent>(TAgent agent) where TAgent : SearchAgent
        {
            if (agent is null)
                throw new ArgumentNullException(nameof(agent));

            _agents.Add(agent);
        }

        /// <summary>
        /// Послідовно запускає місію пошуку для кожного агента в загоні та виводить фінальний звіт.
        /// </summary>
        /// <remarks>
        /// Порядок виконання місії:
        /// <list type="number">
        /// <item><description>Виведення заголовка місії та цільової директорії.</description></item>
        /// <item><description>Запуск <see cref="SearchAgent.ExecuteSearch(string)"/> для кожного агента.</description></item>
        /// <item><description>Заохочення агентів типу <see cref="DogTracker"/> за допомогою <see cref="DogTracker.GiveTreat"/>.</description></item>
        /// <item><description>Підрахунок та виведення загальної кількості знайдених файлів (<see cref="TotalFilesFound"/>).</description></item>
        /// </list>
        /// </remarks>
        public void RunMission()
        {
            Console.WriteLine($"=== Місія загону «{SquadName}» ===");
            Console.WriteLine($"Цільова директорія ОС: {_targetDirectory}");

            foreach (var agent in _agents)
            {
                agent.ExecuteSearch(_targetDirectory);

                if (agent is DogTracker dog && dog.FoundCount > 0)
                {
                    dog.GiveTreat();
                }
            }

            Console.WriteLine("\n==========================================");
            Console.WriteLine($"МІСІЮ ЗАВЕРШЕНО. Всього знайдено файлів: {TotalFilesFound}");
            Console.WriteLine("==========================================");
        }

        /// <summary>
        /// Визначає стандартну системну директорію для сканування залежно від операційної системи.
        /// </summary>
        /// <returns>Шлях до системної директорії Windows, Linux, macOS або поточної робочої директорії.</returns>
        private static string ResolveDefaultDirectory()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Environment.GetFolderPath(Environment.SpecialFolder.System);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "/usr/lib/";

            return Directory.GetCurrentDirectory();
        }
    }

    /// <summary>
    /// Головний клас програми, що містить точку входу.
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// Точка входу в застосунок. Налаштовує кодування консолі, формує загін і запускає пошукову місію.
        /// </summary>
        private static void Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var squad = new SearchSquad("Хвостатий Кіберпатруль");

            squad.AddAgent(new CatScanner("Локі", stealthLevel: 9));
            squad.AddAgent(new DogTracker("Рекс", "Німецька вівчарка"));
            squad.AddAgent(new OwlWatcher("Букля"));

            squad.RunMission();
        }
    }
}