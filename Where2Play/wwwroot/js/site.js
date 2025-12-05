(function () {
    const root = document.documentElement;
    const STORAGE_KEY = 'theme';

    // 1. Function to update ALL toggle buttons on the screen
    function updateUI(theme) {
        const isDark = theme === 'dark';
        const buttons = document.querySelectorAll('.theme-toggle-btn');

        buttons.forEach(btn => {
            const icon = btn.querySelector('i');
            const text = btn.querySelector('span');

            if (isDark) {
                if (icon) icon.className = 'fas fa-sun';
                if (text) text.textContent = ' Light Mode';
                btn.setAttribute('aria-pressed', 'true');
            } else {
                if (icon) icon.className = 'fas fa-moon';
                if (text) text.textContent = ' Dark Mode';
                btn.setAttribute('aria-pressed', 'false');
            }
        });
    }

    // 2. Function to apply the theme to the HTML tag
    function applyTheme(theme) {
        if (theme === 'dark') {
            root.setAttribute('data-theme', 'dark');
        } else {
            root.removeAttribute('data-theme');
        }
        updateUI(theme);
    }

    // 3. Toggle Logic
    function toggleTheme() {
        const currentTheme = root.getAttribute('data-theme') === 'dark' ? 'dark' : 'light';
        const newTheme = currentTheme === 'dark' ? 'light' : 'dark';

        applyTheme(newTheme);
        localStorage.setItem(STORAGE_KEY, newTheme);
    }

    // 4. Initialization (Run on load)
    const savedTheme = localStorage.getItem(STORAGE_KEY);
    const systemPrefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;

    if (savedTheme) {
        applyTheme(savedTheme);
    } else if (systemPrefersDark) {
        applyTheme('dark');
    } else {
        applyTheme('light');
    }

    // 5. Event Listeners
    document.addEventListener('DOMContentLoaded', () => {
        const buttons = document.querySelectorAll('.theme-toggle-btn');
        buttons.forEach(btn => {
            btn.addEventListener('click', (e) => {
                toggleTheme();

                // If on mobile, close the navbar after clicking
                const nav = document.getElementById('mainNav');
                if (nav && nav.classList.contains('show')) {
                    const bsCollapse = bootstrap.Collapse.getInstance(nav);
                    if (bsCollapse) bsCollapse.hide();
                }
            });
        });
    });
})();