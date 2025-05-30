using System.Text.Json;

namespace ThingsGateway.Admin.Application;

public static class OAuthUserExtensions
{
    public static GiteeOAuthUser ToAuthUser(this JsonElement element)
    {
        GiteeOAuthUser authUser = new GiteeOAuthUser();
        JsonElement.ObjectEnumerator target = element.EnumerateObject();
        authUser.Id = target.TryGetValue("id");
        authUser.Login = target.TryGetValue("login");
        authUser.Name = target.TryGetValue("name");
        authUser.Avatar_Url = target.TryGetValue("avatar_url");
        return authUser;
    }

    public static string TryGetValue(this JsonElement.ObjectEnumerator target, string propertyName)
    {
        return target.FirstOrDefault<JsonProperty>((Func<JsonProperty, bool>)(t => t.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))).Value.ToString() ?? string.Empty;
    }
}
