using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Xml.Linq;
using System.Net;
using Newtonsoft.Json.Linq;

namespace TelegramWeatherBot
{
    class Program
    {
        static void Main(string[] args)
        {
            int update_id = 0;
            string messageFromId = "";
            string messageText = "";
            string first_name = "";
            string token = "someToken"; // токен моего погодного бота
            string startUrl = String.Format("https://api.telegram.org/bot{0}", token);

            WebClient webClient = new WebClient();
            //WebProxy wp = new WebProxy("http://ip:port");
            //webClient.Proxy = wp;

            while (true) // Сверка с чатом на предмет новых сообщений и реагирование на них с методом GetWeather(city)
            {
                string url = String.Format("{0}/getUpdates?offset={1}", startUrl, update_id + 1);
                string response = webClient.DownloadString(url);
                var array = JObject.Parse(response)["result"].ToArray();

                foreach (var msg in array)
                {
                    update_id = Convert.ToInt32(msg["update_id"]);
                    try
                    {
                        first_name = msg["message"]["from"]["first_name"].ToString();
                        messageFromId = msg["message"]["from"]["id"].ToString();
                        messageText = msg["message"]["text"].ToString();

                        Console.WriteLine(String.Format("{0} {1} {2}", first_name, messageFromId, messageText));

                        messageText = GetWeather(messageText); // Вызов метода (см. ниже), который возвращает погоду по городу/региону

                        url = String.Format("{0}/sendMessage?chat_id={1}&text={2}", startUrl, messageFromId, messageText);
                        webClient.DownloadString(url);
                    }
                    catch { }
                }
                Thread.Sleep(250); // Запрос Телеграму не чаще 4 раз в секунду, чтоб не забанили
            }
        }

        private static string GetWeather(string city)
        {
            string[] yearMonths = new string[12]
            {"января", "февраля", "марта", "апреля", "мая", "июня", 
                "июля", "августа", "сентября", "октября", "ноября", "декабря"}; // месяц в цифровом выражении заменим позже на словесное название

            string text = string.Empty; // Это будет сообщение о погоде в чат

            Dictionary<string, Tuple<string, string>> cities = new Dictionary<string, Tuple<string, string>>(); // Словарь с кортежем - вместо лапши из if-ов
            cities.Add("/omsk", new Tuple<string, string>("Погода в Омске\n\n", @"http://www.eurometeo.ru/russia/omskaya-oblast/omsk/export/xml/data/"));
            cities.Add("/nsk", new Tuple<string, string>("Погода в Новосибирске\n\n", @"http://www.eurometeo.ru/russia/novosibirskaya-oblast/novosibirsk/export/xml/data/"));
            cities.Add("/tobol", new Tuple<string, string>("Погода в Тобольске\n\n", @"http://www.eurometeo.ru/russia/tumenskaya-oblast/tobolsk/export/xml/data/"));
            cities.Add("/hmao", new Tuple<string, string>("Погода в Ханты-Мансийске\n\n", @"http://www.eurometeo.ru/russia/hantyi-mansiyskiy/hantyi-mansiysk/export/xml/data/"));
            cities.Add("/noyab", new Tuple<string, string>("Погода в Ноябрьске\n\n", @"http://www.eurometeo.ru/russia/yamalo-neneckiy-avtonomnyiy-okrug/noyabrsk/export/xml/data/"));
            cities.Add("/sevas", new Tuple<string, string>("Погода в Севастополе\n\n", @"http://www.eurometeo.ru/russia/sevastopol/export/xml/data/"));           

            if (cities.Keys.Contains(city))
            {
                foreach (var town in cities)
                {
                    if (city == town.Key)
                    {
                        text += town.Value.Item1;
                        city = town.Value.Item2;
                        break;
                    }
                }
            }
            else
            {
                return "Вы можете узнать погоду на завтра в Омске,\n/omsk\n" +
                "в Ханты-Мансийске,\n/hmao\nв Тобольске,\n/tobol\nв Ноябрьске,\n/noyab\nв Севастополе\n/sevas\n" +
                "и в Новосибирске.\n/nsk\n";
            }           

            string xmlData = new WebClient().DownloadString(city);
            var xmlColItem = XDocument.Parse(xmlData).Descendants("weather")
                .Descendants("city")
                .Descendants("step")
                .ToArray();

            int step = 0;

            foreach (var item in xmlColItem.Skip(4).Take(8)) // Данные по погоде "сегодня" отбрасываем, нужна погода на завтра
            {
                string[] dayOfYear = item.Element("datetime").Value.Substring(0, 10).Split('-');
                string date = String.Format("{0} {1} {2} года", dayOfYear[2].TrimStart('0'), yearMonths[Convert.ToInt32(dayOfYear[1]) - 1], dayOfYear[0]);
                string timeOfDay = item.Element("datetime").Value.Substring(11, 8)
                    .Replace("04:00:00", " - ночь:")
                    .Replace("10:00:00", " - утро:")
                    .Replace("16:00:00", " - день:")
                    .Replace("22:00:00", " - вечер:");

                text += String.Format("{0} \nДавление: {1} мм р.ст.\nТемпература: {2} градусов\n\n",
                    date + " " + timeOfDay,
                    item.Element("pressure").Value,
                    item.Element("temperature").Value);
                if (step++ > 2) // step++ > 3 даёт 5 полей "дата/давление/температура", step++ > 4 - даёт 6 полей
                    break;
            }
            return text;
        }
    }
}
