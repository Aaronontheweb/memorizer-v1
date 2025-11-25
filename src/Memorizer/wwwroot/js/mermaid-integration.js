/**
 * Mermaid.js Integration Module
 * Handles initialization and theme-aware rendering of Mermaid diagrams in markdown content.
 */
const MermaidIntegration = (function() {
    let initialized = false;

    /**
     * Get the current Mermaid theme based on document theme
     */
    function getMermaidTheme() {
        const theme = document.documentElement.getAttribute('data-theme');
        return theme === 'dark' ? 'dark' : 'default';
    }

    /**
     * Initialize Mermaid with the current theme
     */
    function init(theme) {
        if (typeof mermaid === 'undefined') {
            console.warn('Mermaid.js not loaded');
            return false;
        }

        const mermaidTheme = theme ? (theme === 'dark' ? 'dark' : 'default') : getMermaidTheme();

        mermaid.initialize({
            startOnLoad: false,
            theme: mermaidTheme,
            securityLevel: 'loose',
            flowchart: {
                useMaxWidth: true,
                htmlLabels: true
            },
            sequence: {
                useMaxWidth: true
            }
        });

        initialized = true;
        return true;
    }

    /**
     * Find and render all Mermaid diagrams in the document
     * Call this after markdown content has been rendered
     */
    function render() {
        if (!initialized) {
            if (!init()) {
                return;
            }
        }

        // Find code blocks with language-mermaid class (created by marked.js)
        // marked.js creates: <pre><code class="language-mermaid">...</code></pre>
        const mermaidBlocks = document.querySelectorAll('pre code.language-mermaid');

        mermaidBlocks.forEach((block, index) => {
            const pre = block.parentElement;
            const code = block.textContent;

            // Create a mermaid container div
            const container = document.createElement('div');
            container.className = 'mermaid';
            container.id = `mermaid-diagram-${index}`;
            container.setAttribute('data-original', code);
            container.textContent = code;

            // Replace the pre element with the mermaid container
            pre.parentNode.replaceChild(container, pre);
        });

        // Run mermaid to render all diagrams
        if (typeof mermaid !== 'undefined' && mermaidBlocks.length > 0) {
            try {
                mermaid.run();
            } catch (err) {
                console.warn('Mermaid render error:', err);
            }
        }
    }

    /**
     * Update the Mermaid theme and re-render all diagrams
     * Called by ThemeSwitcher when the theme changes
     */
    function updateTheme(theme) {
        if (typeof mermaid === 'undefined') {
            return;
        }

        // Re-initialize with new theme
        init(theme);

        // Find all existing mermaid diagrams and re-render them
        const diagrams = document.querySelectorAll('.mermaid[data-original]');

        if (diagrams.length === 0) {
            return;
        }

        diagrams.forEach(el => {
            // Remove the processed attribute so mermaid will re-render
            el.removeAttribute('data-processed');
            // Restore the original code
            const original = el.getAttribute('data-original');
            if (original) {
                el.innerHTML = original;
            }
        });

        // Re-run mermaid
        try {
            mermaid.run();
        } catch (err) {
            console.warn('Mermaid re-render error:', err);
        }
    }

    // Public API
    return {
        init: init,
        render: render,
        updateTheme: updateTheme
    };
})();
