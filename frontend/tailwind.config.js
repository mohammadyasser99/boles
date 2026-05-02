/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./src/**/*.{html,ts}"],
  theme: {
    extend: {
      fontFamily: {
        display: ["'Syne'", "sans-serif"],
        body: ["'DM Sans'", "sans-serif"],
        mono: ["'JetBrains Mono'", "monospace"],
      },
      colors: {
        brand: {
          50:  "#f0f4fe",
          100: "#dde6fd",
          200: "#c3d3fb",
          300: "#9ab4f8",
          400: "#6a8df2",
          500: "#4666eb",
          600: "#3047df",
          700: "#2837cc",
          800: "#272ea5",
          900: "#252c82",
          950: "#1a1d50",
        },
        surface: {
          0:   "#ffffff",
          50:  "#f8f9fc",
          100: "#f0f2f8",
          200: "#e2e6f0",
          800: "#1e2235",
          900: "#141827",
          950: "#0d1020",
        }
      },
      boxShadow: {
        "card": "0 1px 3px 0 rgb(0 0 0 / 0.04), 0 4px 16px -4px rgb(0 0 0 / 0.08)",
        "card-hover": "0 4px 6px -1px rgb(0 0 0 / 0.06), 0 12px 32px -8px rgb(0 0 0 / 0.14)",
        "glow": "0 0 32px -4px rgb(70 102 235 / 0.35)",
      }
    },
  },
  plugins: [],
};
