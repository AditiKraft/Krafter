window.isMobile = function () {
    return window.innerWidth <= 768;
};
window.addEventListener('resize', function () {
    DotNet.invokeMethodAsync('Krafter.UI.Web.Client', 'UpdateIsMobileDevice', window.isMobile());
});

window.deleteElementById = function (id) {
    let element = document.getElementById(id);
    if (element && element.parentNode) {
        element.parentNode.removeChild(element);
    }
};

// Detect the current system theme (dark or light)
window.detectSystemTheme = function () {
    const isDarkMode = window.matchMedia('(prefers-color-scheme: dark)').matches;
    return isDarkMode ? 'dark' : 'light'; // Use 'light' instead of 'standard' for clarity
};

// Listen for system theme changes
window.listenForSystemThemeChanges = function (dotNetReference) {
    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');

    // Initial detection
    const initialTheme = mediaQuery.matches ? 'dark' : 'light';

    // Pass the detected system theme to Blazor
    dotNetReference.invokeMethodAsync('OnSystemThemeChanged', initialTheme);

    // Set up listener for changes
    mediaQuery.addEventListener('change', (e) => {
        const newTheme = e.matches ? 'dark' : 'light';
        dotNetReference.invokeMethodAsync('OnSystemThemeChanged', newTheme);
    });
};

// Helper function to get stored theme preference
window.getStoredThemePreference = function () {
    return localStorage.getItem('themePreference') || 'auto'; // Default to 'auto'
};

// Helper function to store theme preference
window.setStoredThemePreference = function (theme) {
    localStorage.setItem('themePreference', theme);
};

// Apply theme based on preference
window.applyThemePreference = function () {
    const preference = window.getStoredThemePreference();

    if (preference === 'auto') {
        return window.detectSystemTheme();
    }

    return preference; // 'dark' or 'light'
};