using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace NetworkAnalyzer
{
    public partial class MainWindow : Window
    {
        // Список для хранения истории проверенных URL
        private readonly List<string> _urlHistory = new List<string>();

        public MainWindow()
        {
            InitializeComponent();
            // При запуске сразу загружаем список интерфейсов
            LoadNetworkInterfaces();
        }

        // ─────────────────────────────────────────────────────────────────────
        // БЛОК 1: СЕТЕВЫЕ ИНТЕРФЕЙСЫ
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Загружает список всех сетевых интерфейсов в ListBox.
        /// Используется System.Net.NetworkInformation.NetworkInterface.
        /// </summary>
        private void LoadNetworkInterfaces()
        {
            InterfacesList.Items.Clear();

            // Получаем все сетевые интерфейсы, доступные на компьютере
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface ni in interfaces)
            {
                InterfacesList.Items.Add(ni); // добавляем объект целиком
            }

            // Если есть хоть один — выбираем первый
            if (InterfacesList.Items.Count > 0)
                InterfacesList.SelectedIndex = 0;
        }

        /// <summary>
        /// Обработчик кнопки "Обновить список".
        /// </summary>
        private void RefreshInterfaces_Click(object sender, RoutedEventArgs e)
        {
            LoadNetworkInterfaces();
        }

        /// <summary>
        /// При выборе интерфейса в списке — отображаем подробную информацию.
        /// </summary>
        private void InterfacesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InterfacesList.SelectedItem is NetworkInterface ni)
            {
                InterfaceInfoBox.Text = GetInterfaceInfo(ni);
            }
        }

        /// <summary>
        /// Формирует строку с подробной информацией о выбранном интерфейсе.
        /// </summary>
        private string GetInterfaceInfo(NetworkInterface ni)
        {
            StringBuilder sb = new StringBuilder();

            // Основные сведения
            sb.AppendLine($"Имя:           {ni.Name}");
            sb.AppendLine($"Описание:      {ni.Description}");

            // Тип интерфейса (Ethernet, Wireless, Loopback, ...)
            sb.AppendLine($"Тип:           {ni.NetworkInterfaceType}");

            // Состояние: Up (активен) / Down (неактивен) / ...
            sb.AppendLine($"Состояние:     {ni.OperationalStatus}");

            // Скорость в Мбит/с; делим на 1_000_000
            long speedMbps = ni.Speed / 1_000_000;
            sb.AppendLine($"Скорость:      {(ni.Speed < 0 ? "н/д" : speedMbps + " Мбит/с")}");

            // MAC-адрес (физический адрес)
            PhysicalAddress mac = ni.GetPhysicalAddress();
            string macStr = string.Join(":", mac.GetAddressBytes().Select(b => b.ToString("X2")));
            sb.AppendLine($"MAC-адрес:     {(string.IsNullOrEmpty(macStr) ? "н/д" : macStr)}");

            // IP-адреса и маски подсети
            IPInterfaceProperties props = ni.GetIPProperties();
            sb.AppendLine();
            sb.AppendLine("IP-адреса:");

            foreach (UnicastIPAddressInformation addr in props.UnicastAddresses)
            {
                // Фильтруем: IPv4 (InterNetwork) и IPv6 (InterNetworkV6)
                string family = addr.Address.AddressFamily == AddressFamily.InterNetwork
                    ? "IPv4" : "IPv6";

                sb.AppendLine($"  [{family}] {addr.Address}");

                // Маска подсети есть только у IPv4
                if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    sb.AppendLine($"  Маска:  {addr.IPv4Mask}");
            }

            // DNS-серверы, настроенные для интерфейса
            sb.AppendLine();
            sb.AppendLine("DNS-серверы:");
            foreach (IPAddress dns in props.DnsAddresses)
                sb.AppendLine($"  {dns}");

            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────────
        // БЛОК 2: АНАЛИЗ URL
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Парсит введённый URL с помощью класса Uri и показывает все компоненты.
        /// </summary>
        private void AnalyzeUrl_Click(object sender, RoutedEventArgs e)
        {
            string input = UrlInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                ResultsBox.Text = "Введите URL для анализа.";
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("═══ АНАЛИЗ URL ═══");
            sb.AppendLine($"Введено: {input}");
            sb.AppendLine();

            // Пытаемся создать объект Uri
            // Uri.IsWellFormedUriString — проверяет корректность формата
            if (!Uri.TryCreate(input, UriKind.Absolute, out Uri uri))
            {
                sb.AppendLine("❌ Некорректный URL. Убедитесь, что указана схема (https://, http://, ftp://).");
                ResultsBox.Text = sb.ToString();
                return;
            }

            // Компоненты URI согласно RFC 3986:
            // схема://[пользователь@]хост[:порт]/путь[?запрос][#фрагмент]

            sb.AppendLine($"✔ Схема (протокол): {uri.Scheme}");
            sb.AppendLine($"✔ Хост:             {uri.Host}");

            // uri.Port возвращает явно указанный порт или порт по умолчанию (80/443/21...)
            sb.AppendLine($"✔ Порт:             {uri.Port} {(uri.IsDefaultPort ? "(по умолчанию)" : "(явно задан)")}");

            // AbsolutePath — часть после хоста до знака '?'
            sb.AppendLine($"✔ Путь:             {uri.AbsolutePath}");

            // Query — строка параметров вместе со знаком '?'
            sb.AppendLine($"✔ Строка запроса:   {(string.IsNullOrEmpty(uri.Query) ? "(нет)" : uri.Query)}");

            // Fragment — часть после '#'
            sb.AppendLine($"✔ Фрагмент (#):     {(string.IsNullOrEmpty(uri.Fragment) ? "(нет)" : uri.Fragment)}");

            sb.AppendLine();

            // Парсим параметры запроса вручную, разбивая строку
            if (!string.IsNullOrEmpty(uri.Query))
            {
                sb.AppendLine("Параметры запроса:");
                // Убираем ведущий '?' и разбиваем по '&'
                string queryString = uri.Query.TrimStart('?');
                string[] pairs = queryString.Split('&');
                foreach (string pair in pairs)
                {
                    string[] kv = pair.Split('=');
                    string key = Uri.UnescapeDataString(kv[0]);
                    string value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
                    sb.AppendLine($"  {key} = {value}");
                }
                sb.AppendLine();
            }

            // Определяем тип адреса хоста
            sb.AppendLine($"Тип адреса: {ClassifyHost(uri.Host)}");

            // Добавляем в историю
            AddToHistory(input);

            ResultsBox.Text = sb.ToString();
        }

        /// <summary>
        /// Определяет тип хоста: loopback, локальный, публичный.
        /// </summary>
        private string ClassifyHost(string host)
        {
            // Пытаемся распарсить как IP
            if (IPAddress.TryParse(host, out IPAddress ip))
            {
                if (IPAddress.IsLoopback(ip))
                    return "Loopback (петлевой адрес, localhost)";

                // Проверяем диапазоны RFC 1918 (приватные сети)
                // 10.x.x.x | 172.16-31.x.x | 192.168.x.x
                byte[] bytes = ip.GetAddressBytes();
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    if (bytes[0] == 10)
                        return "Локальный (приватный, диапазон 10.0.0.0/8)";
                    if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                        return "Локальный (приватный, диапазон 172.16.0.0/12)";
                    if (bytes[0] == 192 && bytes[1] == 168)
                        return "Локальный (приватный, диапазон 192.168.0.0/16)";
                }
                return "Публичный IP-адрес";
            }

            // Если это имя хоста (не IP)
            if (host == "localhost")
                return "Loopback (localhost)";

            return "Публичное доменное имя";
        }

        // ─────────────────────────────────────────────────────────────────────
        // БЛОК 3: PING + DNS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Выполняет Ping хоста и DNS-запрос асинхронно, чтобы не замораживать UI.
        /// </summary>
        private async void PingHost_Click(object sender, RoutedEventArgs e)
        {
            string host = PingHostInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(host))
            {
                ResultsBox.Text = "Введите хост для проверки.";
                return;
            }

            ResultsBox.Text = $"Выполняю Ping и DNS-запрос для '{host}'...\n";

            // Запускаем асинхронно, чтобы окно не «зависало»
            string result = await Task.Run(() => PerformPingAndDns(host));

            ResultsBox.Text = result;

            // Добавляем в историю как ping:<хост>
            AddToHistory("ping:" + host);
        }

        /// <summary>
        /// Синхронный метод: выполняет Ping и DNS-запрос, возвращает отчёт.
        /// Запускается в фоновом потоке через Task.Run.
        /// </summary>
        private string PerformPingAndDns(string host)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"═══ PING + DNS: {host} ═══");
            sb.AppendLine();

            // ── Ping ──────────────────────────────────────────────────────────
            // Класс Ping из System.Net.NetworkInformation
            // Отправляем ICMP Echo Request
            using (Ping ping = new Ping())
            {
                sb.AppendLine("── Результаты Ping (4 попытки) ──");
                for (int i = 1; i <= 4; i++)
                {
                    try
                    {
                        // Таймаут 3000 мс
                        PingReply reply = ping.Send(host, 3000);

                        if (reply.Status == IPStatus.Success)
                        {
                            // RoundtripTime — время туда-обратно в мс
                            sb.AppendLine($"  [{i}] ✔ Ответ от {reply.Address} : {reply.RoundtripTime} мс  TTL={reply.Options?.Ttl}");
                        }
                        else
                        {
                            // Хост не отвечает или недоступен
                            sb.AppendLine($"  [{i}] ✘ {reply.Status}");
                        }
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"  [{i}] ✘ Ошибка: {ex.Message}");
                    }
                }
            }

            sb.AppendLine();

            // ── DNS ───────────────────────────────────────────────────────────
            // Dns.GetHostEntry — выполняет прямой DNS-запрос (имя → IP)
            // и обратный (IP → имя)
            sb.AppendLine("── DNS-информация ──");
            try
            {
                IPHostEntry entry = Dns.GetHostEntry(host);

                sb.AppendLine($"Hostname: {entry.HostName}");

                // Все IP-адреса, возвращённые DNS
                sb.AppendLine("IP-адреса (A/AAAA записи):");
                foreach (IPAddress addr in entry.AddressList)
                {
                    string family = addr.AddressFamily == AddressFamily.InterNetwork ? "IPv4" : "IPv6";
                    sb.AppendLine($"  [{family}] {addr}  → тип: {ClassifyHost(addr.ToString())}");
                }

                // Псевдонимы (CNAME-записи), если есть
                if (entry.Aliases.Length > 0)
                {
                    sb.AppendLine("Псевдонимы (CNAME):");
                    foreach (string alias in entry.Aliases)
                        sb.AppendLine($"  {alias}");
                }
            }
            catch (SocketException sex)
            {
                sb.AppendLine($"✘ DNS-ошибка: {sex.Message}");
                sb.AppendLine($"  Код ошибки: {sex.SocketErrorCode}");
            }

            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────────
        // БЛОК 4: ИСТОРИЯ ПРОВЕРЕННЫХ URL
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Добавляет запись в историю (без дублей подряд).
        /// </summary>
        private void AddToHistory(string entry)
        {
            // Не добавляем дубль, если последняя запись такая же
            if (_urlHistory.Count > 0 && _urlHistory.Last() == entry)
                return;

            _urlHistory.Add(entry);

            // Добавляем в ListBox с номером и временем
            string item = $"{_urlHistory.Count}. [{DateTime.Now:HH:mm:ss}] {entry}";
            HistoryList.Items.Add(item);

            // Прокручиваем к последнему элементу
            HistoryList.ScrollIntoView(item);
        }

        /// <summary>
        /// Клик по элементу истории — вставляет его в поле ввода URL.
        /// </summary>
        private void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HistoryList.SelectedIndex >= 0 && HistoryList.SelectedIndex < _urlHistory.Count)
            {
                string selected = _urlHistory[HistoryList.SelectedIndex];
                // Если это запись ping: — вставляем в поле хоста, иначе в URL
                if (selected.StartsWith("ping:"))
                    PingHostInput.Text = selected.Replace("ping:", "");
                else
                    UrlInput.Text = selected;
            }
        }

        /// <summary>
        /// Очищает историю.
        /// </summary>
        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            _urlHistory.Clear();
            HistoryList.Items.Clear();
        }
    }
}