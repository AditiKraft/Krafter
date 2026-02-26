namespace AditiKraft.Krafter.UI.Web.Client.Infrastructure.Services;

public class ThemeManager(ThemeService themeService, IJSRuntime jsRuntime)
{
    public event Func<string, Task>? ThemeChangeRequested;

    public enum ThemePreference
    {
        Auto,
        Dark,
        Light
    }

    public ThemePreference CurrentPreference { get; set; } = ThemePreference.Auto;

    public ThemePreference CurrentActive { get; set; } =
        themeService.Theme?.Contains("dark") == true ? ThemePreference.Dark : ThemePreference.Light;

    public async Task SetThemePreference(ThemePreference preference)
    {
        CurrentPreference = preference;

        string preferenceValue = preference switch
        {
            ThemePreference.Auto => "auto",
            ThemePreference.Dark => "dark",
            ThemePreference.Light => "light",
            _ => "auto"
        };
        await jsRuntime.InvokeVoidAsync("setStoredThemePreference", preferenceValue);
        string apptheme = string.Empty;
        if (preference == ThemePreference.Auto)
        {
            string systemTheme = await jsRuntime.InvokeAsync<string>("detectSystemTheme");
            if (systemTheme == "dark")
            {
                CurrentActive = ThemePreference.Dark;
            }
            else if (systemTheme == "light")
            {
                CurrentActive = ThemePreference.Light;
            }

            apptheme = MapSystemThemeToAppTheme(systemTheme);
        }
        else
        {
            CurrentActive = preference;
            apptheme = await MapPreferenceToTheme(preference);
        }

        themeService.SetTheme(apptheme);
        await (ThemeChangeRequested?.Invoke("") ?? Task.CompletedTask);
    }


    public async Task OnSystemThemeChanged(string systemTheme)
    {
        string value = await jsRuntime.InvokeAsync<string>("getStoredThemePreference");
        if (value == "auto")
        {
            CurrentPreference = ThemePreference.Auto;

            if (systemTheme == "dark")
            {
                if (CurrentActive != ThemePreference.Dark)
                {
                    CurrentActive = ThemePreference.Dark;
                    await (ThemeChangeRequested?.Invoke("") ?? Task.CompletedTask);
                }
            }
            else if (systemTheme == "light")
            {
                if (CurrentActive != ThemePreference.Light)
                {
                    CurrentActive = ThemePreference.Light;
                    await (ThemeChangeRequested?.Invoke("") ?? Task.CompletedTask);
                }
            }

            string appTheme = MapSystemThemeToAppTheme(systemTheme);
            if (appTheme != themeService.Theme)
            {
                themeService.SetTheme(appTheme);
            }
        }
        else if (value == "dark")
        {
            CurrentPreference = ThemePreference.Dark;
            if (CurrentActive != ThemePreference.Dark)
            {
                CurrentActive = ThemePreference.Dark;
                await (ThemeChangeRequested?.Invoke("") ?? Task.CompletedTask);
            }
        }
        else if (value == "light")
        {
            CurrentPreference = ThemePreference.Light;
            if (CurrentActive != ThemePreference.Light)
            {
                CurrentActive = ThemePreference.Light;
                await (ThemeChangeRequested?.Invoke("") ?? Task.CompletedTask);
            }
        }
    }

    private string MapSystemThemeToAppTheme(string systemTheme)
    {
        if (systemTheme == "dark")
        {
            return CurrentDarkTheme;
        }
        else if (systemTheme == "light")
        {
            return CurrentLightTheme;
        }
        else
        {
            return themeService.Theme;
        }
    }

    private async Task<string> MapPreferenceToTheme(ThemePreference preference)
    {
        if (preference == ThemePreference.Auto)
        {
            string systemTheme = await jsRuntime.InvokeAsync<string>("detectSystemTheme");
            return MapSystemThemeToAppTheme(systemTheme);
        }

        if (preference == ThemePreference.Dark)
        {
            return CurrentDarkTheme;
        }
        else if (preference == ThemePreference.Light)
        {
            return CurrentLightTheme;
        }
        else
        {
            return themeService.Theme;
        }
    }


    private string CurrentLightTheme => themeService.Theme.ToLowerInvariant() switch
    {
        "dark" => "default",
        "material-dark" => "material",
        "fluent-dark" => "fluent",
        "material3-dark" => "material3",
        "software-dark" => "software",
        "humanistic-dark" => "humanistic",
        "standard-dark" => "standard",
        _ => themeService.Theme
    };

    private string CurrentDarkTheme => themeService.Theme.ToLowerInvariant() switch
    {
        "default" => "dark",
        "material" => "material-dark",
        "fluent" => "fluent-dark",
        "material3" => "material3-dark",
        "software" => "software-dark",
        "humanistic" => "humanistic-dark",
        "standard" => "standard-dark",
        _ => themeService.Theme
    };


    public async Task SetDifferentThemeAsync(string apptheme)
    {
        string SystemTheme = "";
        if (apptheme.Contains("dark"))
        {
            CurrentPreference = ThemePreference.Dark;
            if (CurrentActive != ThemePreference.Dark)
            {
                CurrentActive = ThemePreference.Dark;
                await (ThemeChangeRequested?.Invoke("") ?? Task.CompletedTask);
            }

            SystemTheme = "dark";
        }
        else
        {
            CurrentPreference = ThemePreference.Light;
            if (CurrentActive != ThemePreference.Light)
            {
                CurrentActive = ThemePreference.Light;
                await (ThemeChangeRequested?.Invoke("") ?? Task.CompletedTask);
            }

            SystemTheme = "light";
        }

        await jsRuntime.InvokeVoidAsync("setStoredThemePreference", SystemTheme);
        themeService.SetTheme(apptheme);
    }
}
