using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static class StationNameFormatter
{
    private static readonly Dictionary<char, string> Alphabet = new()
    {
        ['A'] = "Альфа",
        ['B'] = "Браво",
        ['C'] = "Чарли",
        ['D'] = "Дельта",
        ['E'] = "Эхо",
        ['F'] = "Фокстрот",
        ['G'] = "Гольф",
        ['H'] = "Хотел",
        ['I'] = "Индия",
        ['J'] = "Джульетт",
        ['K'] = "Кило",
        ['L'] = "Лима",
        ['M'] = "Майк",
        ['N'] = "Новембер",
        ['O'] = "Оскар",
        ['P'] = "Папа",
        ['Q'] = "Кебек",
        ['R'] = "Ромео",
        ['S'] = "Сьерра",
        ['T'] = "Танго",
        ['U'] = "Юниформ",
        ['V'] = "Виктор",
        ['W'] = "Виски",
        ['X'] = "Иксрей",
        ['Y'] = "Янки",
        ['Z'] = "Зулу"
    };

    private static readonly string[] Digits =
    {
        "Ноль", "Один", "Два", "Три", "Четыре",
        "Пять", "Шесть", "Семь", "Восемь", "Девять"
    };

    public static string Format(string stationName)
    {
        var match = Regex.Match(
            stationName,
            @"([A-Za-z]{2})-(\d{3})\s*$"
        );

        if (!match.Success)
            throw new ArgumentException(
                "Station name must end with two letters and three digits, e.g. NX-981.",
                nameof(stationName));

        string letters = match.Groups[1].Value.ToUpperInvariant();
        string numbers = match.Groups[2].Value;

        string code = string.Join("-",
            letters.Select(c => Alphabet[c]));

        string digits = string.Join("-",
            numbers.Select(c => Digits[c - '0']));

        return $"{code} {digits}";
    }
}
