const colors = require("tailwindcss/colors");

const phosphor = {
  50: "#f0ffed",
  100: "#dcffd4",
  200: "#baffaa",
  300: "#8cff72",
  400: "#5cff3f",
  500: "#39ff14",
  600: "#25d607",
  700: "#1b9f08",
  800: "#176f0d",
  900: "#155512",
  950: "#062d04"
};

module.exports = {
  content: [
    "./Views/**/*.cshtml",
    "./wwwroot/js/**/*.js"
  ],
  theme: {
    colors: {
      inherit: "inherit",
      current: "currentColor",
      transparent: "transparent",
      black: "#000000",
      white: "#f4fff1",
      slate: colors.slate,
      gray: colors.gray,
      cyan: phosphor,
      green: phosphor,
      emerald: phosphor,
      fuchsia: colors.amber,
      amber: colors.amber,
      orange: colors.orange,
      red: colors.red,
      rose: colors.rose,
      indigo: colors.indigo
    },
    extend: {
      fontFamily: {
        sans: [
          "ui-monospace",
          "SFMono-Regular",
          "Menlo",
          "Monaco",
          "Consolas",
          "Liberation Mono",
          "Courier New",
          "monospace"
        ],
        mono: [
          "ui-monospace",
          "SFMono-Regular",
          "Menlo",
          "Monaco",
          "Consolas",
          "Liberation Mono",
          "Courier New",
          "monospace"
        ],
        display: [
          "ui-monospace",
          "SFMono-Regular",
          "Menlo",
          "Monaco",
          "Consolas",
          "Liberation Mono",
          "Courier New",
          "monospace"
        ]
      },
      boxShadow: {
        phosphor: "0 0 8px rgba(57,255,20,.32), 0 0 28px rgba(57,255,20,.12)",
        "phosphor-strong": "0 0 12px rgba(57,255,20,.48), 0 0 42px rgba(57,255,20,.18)",
        amber: "0 0 10px rgba(245,158,11,.28), 0 0 32px rgba(245,158,11,.10)"
      },
      keyframes: {
        "crt-flicker": {
          "0%, 18%, 22%, 25%, 53%, 57%, 100%": { opacity: "1" },
          "20%, 24%, 55%": { opacity: ".965" }
        },
        "signal-pulse": {
          "0%, 100%": { opacity: ".45", transform: "scale(.92)" },
          "50%": { opacity: "1", transform: "scale(1)" }
        }
      },
      animation: {
        "crt-flicker": "crt-flicker 7s linear infinite",
        "signal-pulse": "signal-pulse 1.8s ease-in-out infinite"
      }
    }
  },
  plugins: []
};
