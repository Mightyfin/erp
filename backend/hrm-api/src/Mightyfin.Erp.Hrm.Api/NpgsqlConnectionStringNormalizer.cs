namespace Mightyfin.Erp.Hrm.Api.Routing;

/// <summary>Converts a libpq-style PostgreSQL URI connection string
/// (e.g. postgresql://user:pass@host:port/db?sslmode=disable) into the
/// keyword/value format that Npgsql requires, since Npgsql's
/// NpgsqlConnectionStringBuilder does not accept query-string parameters.</summary>
internal static class NpgsqlConnectionStringNormalizer
{
    public static string Normalize(string connectionString)
    {
        var uri = new Uri(connectionString);
        var sb = new System.Text.StringBuilder();
        void Add(string key, string? value)
        {
            if (value is not null)
            {
                if (sb.Length > 0) sb.Append(';');
                sb.Append(key).Append('=').Append(value);
            }
        }

        Add("Host", uri.Host);
        if (uri.Port > 0) Add("Port", uri.Port.ToString());
        Add("Database", uri.AbsolutePath.TrimStart('/'));
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var parts = uri.UserInfo.Split(':', 2);
            Add("Username", Uri.UnescapeDataString(parts[0]));
            if (parts.Length > 1)
                Add("Password", Uri.UnescapeDataString(parts[1]));
        }

        // Map libpq query parameters to Npgsql keywords where they differ.
        var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
        foreach (var originalKey in queryParams.AllKeys)
        {
            if (originalKey is null) continue;
            var value = queryParams[originalKey];
            if (value is null) continue;
            var mappedKey = originalKey.Equals("sslmode", StringComparison.OrdinalIgnoreCase)
                ? "SslMode" : originalKey;
            Add(mappedKey, value);
        }

        return sb.ToString();
    }
}
