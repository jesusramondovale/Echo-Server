namespace Echo.Application.Services;

/// <summary>
/// Push titles and bodies. Keep in sync with the app arb files
/// (pushBoostTitle/Body, pushNewPostTitle/Body).
/// </summary>
public static class PushCopy
{
    private static readonly Dictionary<string, (string Title, string Body)> Boost = new(StringComparer.Ordinal)
    {
        ["en"] = ("Your echo travels farther", "Someone boosted your message's reach."),
        ["es"] = ("Tu eco llega más lejos", "Alguien amplió el alcance de tu mensaje."),
        ["de"] = ("Dein Echo reicht weiter", "Jemand hat die Reichweite deiner Nachricht erweitert."),
        ["fr"] = ("Votre écho va plus loin", "Quelqu'un a élargi la portée de votre message."),
        ["it"] = ("Il tuo eco arriva più lontano", "Qualcuno ha ampliato la portata del tuo messaggio."),
        ["pt"] = ("O teu eco chega mais longe", "Alguém ampliou o alcance da tua mensagem.")
    };

    private static readonly Dictionary<string, (string Title, string Body)> NewPost = new(StringComparer.Ordinal)
    {
        ["en"] = ("New echo nearby", "Someone posted in your zone."),
        ["es"] = ("Nuevo eco cerca", "Alguien escribió en tu zona."),
        ["de"] = ("Neues Echo in der Nähe", "Jemand hat in deiner Zone geschrieben."),
        ["fr"] = ("Nouvel écho proche", "Quelqu'un a écrit dans votre zone."),
        ["it"] = ("Nuovo eco vicino", "Qualcuno ha scritto nella tua zona."),
        ["pt"] = ("Novo eco perto", "Alguém escreveu na tua zona.")
    };

    public static string Normalize(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return "en";
        }

        var lang = locale.Trim().ToLowerInvariant();
        var split = lang.IndexOfAny(['-', '_']);
        if (split > 0)
        {
            lang = lang[..split];
        }

        return Boost.ContainsKey(lang) ? lang : "en";
    }

    public static (string Title, string Body) ForBoost(string? locale)
        => Boost[Normalize(locale)];

    public static (string Title, string Body) ForNewPost(string? locale)
        => NewPost[Normalize(locale)];
}
